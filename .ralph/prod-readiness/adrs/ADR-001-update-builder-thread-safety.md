# ADR-001: UpdateExpressionBuilder Thread-Safety Fix

**Status**: PROPOSED
**Date**: 2024-02-16
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

**Issue Scope**:
- ✅ `ProjectionBuilder` - thread-safe (creates local state per call)
- ❌ `UpdateExpressionBuilder` - NOT thread-safe (mutable instance fields)
- ❌ `FilterExpressionBuilder` - likely same issue (needs verification)
- ❌ `ConditionExpressionBuilder` - likely same issue (needs verification)
- ❌ `KeyConditionExpressionBuilder` - likely same issue (needs verification)

## Options

### Option 1: Clone-on-Use Pattern (Minimal Breaking Change)
Each method call returns a **new instance** with updated state:
```csharp
public IUpdateExpressionBuilder<TSource> Set<TValue>(
    Expression<Func<TSource, TValue>> property,
    TValue value)
{
    var clone = new UpdateExpressionBuilder<TSource>(
        this.resolverFactory,
        this.converterRegistry,
        this.keywordRegistry);

    // Copy all current state
    foreach (var kvp in this.setOperations)
        clone.setOperations[kvp.Key] = kvp.Value;
    // ... copy other dictionaries

    // Add new operation to clone
    var propertyPath = ExtractPropertyPath(property);
    var attributeName = ResolveAttributeName(propertyPath);
    var valueAlias = clone.aliasGen.NextValue();
    clone.values[valueAlias] = clone.valueEmitter.Emit(value!, propertyPath.PropertyInfo);
    clone.setOperations[propertyPath.FullPath] = new UpdateOperation(...);

    return clone;
}
```

**Pros**:
- Zero breaking changes to public API
- Fluent chaining still works
- Automatically thread-safe (each thread gets its own instance chain)

**Cons**:
- Allocation overhead on every method call (but update calls are rare compared to queries)
- Slight mental model shift (each call creates new builder)

### Option 2: Make Instance Per-Operation (BREAKING)
Change DI registration from singleton to transient:
```csharp
services.AddTransient(typeof(IUpdateExpressionBuilder<>), typeof(UpdateExpressionBuilder<>));
```

Users must instantiate builder per update operation:
```csharp
// OLD (broken under concurrency)
builder.Set(x => x.Name, "Alice").Set(x => x.Age, 30).Build();

// NEW (requires injection per operation)
var builder = serviceProvider.GetRequiredService<IUpdateExpressionBuilder<Order>>();
var result = builder.Set(x => x.Name, "Alice").Set(x => x.Age, 30).Build();
```

**Pros**:
- Clear ownership model
- No hidden cloning

**Cons**:
- **BREAKING** change to DI registration guidance
- Violates spec 03 §8 intent (singleton/DI registration)
- Inconsistent with ProjectionBuilder (which IS singleton-safe)

### Option 3: Lock-Based Thread-Safety (NOT RECOMMENDED)
Add locks around state mutations:
```csharp
private readonly object _lock = new();

public IUpdateExpressionBuilder<TSource> Set<TValue>(...)
{
    lock (_lock)
    {
        // ... mutate state
    }
    return this;
}
```

**Pros**:
- No API changes
- No allocations

**Cons**:
- Contention under high concurrency (serializes all calls)
- Fluent chains would hold lock across multiple calls
- `.Build()` call timing matters (when is state captured?)
- Complex reasoning about when state is "finalized"

## Recommendation

**Option 1 (Clone-on-Use)** is the best path forward:

1. **Aligns with spec intent**: Builders remain singleton-safe
2. **Zero breaking changes**: Existing code works as-is
3. **Performance acceptable**: Update operations are infrequent compared to queries
4. **Simple mental model**: Each fluent chain is isolated

## Implementation Plan

If approved:

1. Refactor `UpdateExpressionBuilder` to clone on each method call
2. Audit and fix `FilterExpressionBuilder`, `ConditionExpressionBuilder`, `KeyConditionExpressionBuilder` (likely same issue)
3. Add concurrency unit tests for all builders:
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
4. Update property-based tests to include concurrent builder usage
5. Re-run soak test to verify fix

## Decision Needed

**Does Ralph have authority to make this change, or does this require human approval?**

- This is an implementation fix to match spec intent (thread-safety)
- No public API changes
- Fixes a critical bug discovered during production readiness testing

**Awaiting human decision on:**
1. Approval of Option 1 (clone-on-use pattern)
2. OR: Select different option with rationale
3. OR: Escalate for architectural review
