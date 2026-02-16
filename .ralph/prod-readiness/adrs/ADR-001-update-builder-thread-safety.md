# ADR-001: UpdateExpressionBuilder Thread-Safety Fix

**Status**: APPROVED
**Date**: 2024-02-16
**Updated**: 2026-02-16
**Approved**: 2026-02-16
**Context**: Phase 2 (Soak Testing) - Task 2.11

## Problem

Soak testing discovered a critical thread-safety bug in `UpdateExpressionBuilder<TSource>`:

**Symptom**: `InvalidUpdateException: Property 'Notes' has conflicting update operations` under concurrent load

**Root Cause**: The builder uses mutable instance fields to accumulate operations:
```csharp
// Lines 25-32 of UpdateExpressionBuilder.cs
private readonly Dictionary<string, string> names = new();
private readonly Dictionary<string, AttributeValue> values = new();
private readonly Dictionary<string, UpdateOperation> setOperations = new();
private readonly HashSet<string> removeProperties = new();
// etc.
```

When multiple threads concurrently call `.Set()`, `.Increment()`, etc. on the same singleton builder instance (as registered in DI), operations from different threads mix together in the shared dictionaries.

**Design Intent**: Spec 03 §8 states: `ProjectionBuilder<TSource>` is **thread-safe and designed for singleton/DI registration**. Spec 11 §3 registers all builders as `services.AddSingleton(typeof(IUpdateExpressionBuilder<>), ...)`.

**Issue Scope** (verified via code audit):
- `ProjectionBuilder` — thread-safe (creates local state per call)
- `FilterExpressionBuilder` — thread-safe (creates local state in `BuildFilter`)
- `ConditionExpressionBuilder` — thread-safe (creates local state in `BuildCondition`)
- `KeyConditionExpressionBuilder` — thread-safe (creates local state in `WithPartitionKey`)
- **`UpdateExpressionBuilder`** — **NOT thread-safe** (mutable instance fields)

`UpdateExpressionBuilder` is the **only** affected builder. All other builders already follow the correct pattern: instance fields hold only immutable dependencies (`resolverFactory`, `converterRegistry`), and all mutable state (`AliasGenerator`, dictionaries, `StringBuilder`, `ExpressionValueEmitter`) is created locally within each public method call.

## Root Cause Analysis

The other builders have a single-call API (e.g., `BuildFilter(predicate)`) where all mutable state is naturally method-scoped. `UpdateExpressionBuilder` has a multi-call fluent API (`.Set().Set().Build()`) and was implemented by accumulating state in instance fields — breaking thread-safety when used as a singleton.

## Decision: Local State per Fluent Chain (Clone-on-Use)

Apply the same principle used by the other thread-safe builders — **no mutable instance state** — adapted for the fluent API pattern:

- The singleton instance holds only immutable dependencies (`resolverFactory`, `converterRegistry`, `keywordRegistry`) and acts as a stateless factory/seed
- Each fluent method (`.Set()`, `.Increment()`, etc.) returns a **new instance** carrying its own operation state
- Each fluent chain is fully isolated — no shared mutable state between threads

```csharp
public IUpdateExpressionBuilder<TSource> Set<TValue>(
    Expression<Func<TSource, TValue>> property,
    TValue value)
{
    var clone = new UpdateExpressionBuilder<TSource>(
        this.resolverFactory,
        this.converterRegistry,
        this.keywordRegistry);

    // Copy accumulated state from current instance
    foreach (var kvp in this.setOperations)
        clone.setOperations[kvp.Key] = kvp.Value;
    // ... copy other dictionaries

    // Add new operation to clone
    var propertyPath = ExtractPropertyPath(property);
    var attributeName = clone.ResolveAttributeName(propertyPath);
    var valueAlias = clone.aliasGen.NextValue();
    clone.values[valueAlias] = clone.valueEmitter.Emit(value!, propertyPath.PropertyInfo);
    clone.setOperations[propertyPath.FullPath] = new UpdateOperation(...);

    return clone;
}
```

**Why this approach**:
- Same principle as all other builders: no mutable instance state
- Zero breaking changes to public API — fluent chaining works as before
- Singleton DI registration remains correct (aligns with spec intent)
- Allocation overhead is negligible — update operations are infrequent, and the cloned dictionaries are small (typically <10 entries)

**Why not alternatives**:
- **Transient DI registration**: Breaks spec contract, shifts thread-safety burden to consumers, inconsistent with other builders
- **Locking**: Doesn't solve the logical isolation problem — concurrent `.Set()` calls would still contaminate each other's `Build()` results even if individual mutations are serialized

## Implementation Plan

1. Refactor `UpdateExpressionBuilder` to clone on each fluent method call
   - Instance fields hold only immutable dependencies
   - Each method creates a new instance with copied + extended state
   - `Build()` operates on the final instance's local state
2. Add concurrency unit tests:
   ```csharp
   [Fact]
   public async Task ConcurrentUpdates_DoNotShareState()
   {
       var builder = new UpdateExpressionBuilder<Order>(...);

       var task1 = Task.Run(() => builder.Set(x => x.Name, "Alice").Build());
       var task2 = Task.Run(() => builder.Set(x => x.Name, "Bob").Build());

       var results = await Task.WhenAll(task1, task2);

       results[0].Expression.Should().Contain("Alice");
       results[1].Expression.Should().Contain("Bob");
       results[0].Expression.Should().NotContain("Bob");
       results[1].Expression.Should().NotContain("Alice");
   }
   ```
3. Re-run soak test to verify fix
