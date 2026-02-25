# Spec 14: Exception Type Hierarchy

## Motivation

Seven custom exceptions are referenced across specs but none are formally defined. Without a defined hierarchy, consumers cannot:

- Catch a single base type at API boundaries
- Distinguish build-time validation errors from merge-time conflicts programmatically
- Access structured error context (property names, types, alias keys) without parsing message strings

## Design Principles

1. **Single catchable base** — `ExpressionMappingException` lets consumers write `catch (ExpressionMappingException)` around any library call
2. **Abstract intermediates** — `ExpressionMappingException` and `InvalidExpressionException` are abstract; they should be caught, never thrown directly
3. **Structured properties** — each exception carries typed context beyond the message string
4. **Fail-fast philosophy** — all exceptions are thrown at expression-build time or merge time, never during query execution

## Hierarchy

```
ExpressionMappingException (abstract)
├── UnsupportedExpressionException
├── MissingConverterException
├── ExpressionAttributeConflictException
└── InvalidExpressionException (abstract)
    ├── InvalidProjectionException
    ├── InvalidFilterException
    ├── InvalidUpdateException
    └── InvalidKeyConditionException
```

## Type Definitions

### 1. ExpressionMappingException (Abstract Base)

```csharp
namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Base class for all exceptions thrown by DynamoDb.ExpressionMapping.
/// Catch this type at API boundaries for blanket handling.
/// </summary>
public abstract class ExpressionMappingException : Exception
{
    protected ExpressionMappingException(string message)
        : base(message) { }

    protected ExpressionMappingException(string message, Exception innerException)
        : base(message, innerException) { }
}
```

No additional properties — it exists purely as a catch target.

### 2. UnsupportedExpressionException

Thrown by `ProjectionExpressionVisitor` (Spec 02 §4) when the expression tree contains a node type the library cannot translate.

```csharp
namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when an expression tree contains a node type that cannot
/// be translated to a DynamoDB expression (e.g. arithmetic,
/// conditional, array indexing).
/// </summary>
public sealed class UnsupportedExpressionException : ExpressionMappingException
{
    /// <summary>
    /// The expression tree node type that was rejected.
    /// E.g. <see cref="System.Linq.Expressions.ExpressionType.Call"/>.
    /// </summary>
    public ExpressionType NodeType { get; }

    /// <summary>
    /// The <c>.ToString()</c> representation of the rejected expression node,
    /// for diagnostic purposes.
    /// </summary>
    public string ExpressionText { get; }

    public UnsupportedExpressionException(ExpressionType nodeType, string expressionText)
        : base($"Expression node type '{nodeType}' is not supported: {expressionText}")
    {
        NodeType = nodeType;
        ExpressionText = expressionText;
    }
}
```

### 3. MissingConverterException

Thrown by `AttributeValueConverterRegistry` (Spec 05 §8) when no converter is found after exhausting the resolution chain.

```csharp
namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when no <see cref="IAttributeValueConverter"/> is registered for a
/// .NET type. Thrown at mapper/builder creation time (fail fast), not during
/// query execution.
/// </summary>
public sealed class MissingConverterException : ExpressionMappingException
{
    /// <summary>
    /// The .NET type for which no converter was found.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// The property that triggered the converter lookup, if known.
    /// Null when the lookup originated from a direct registry call.
    /// </summary>
    public string? PropertyName { get; }

    public MissingConverterException(Type targetType, string? propertyName = null)
        : base(propertyName is not null
            ? $"No converter registered for type '{targetType}' (property '{propertyName}')."
            : $"No converter registered for type '{targetType}'.")
    {
        TargetType = targetType;
        PropertyName = propertyName;
    }
}
```

### 4. ExpressionAttributeConflictException

Thrown by `RequestMergeHelpers` (Spec 10 §6) when merging expression attribute dictionaries from independently built expressions.

```csharp
namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when merging <c>ExpressionAttributeNames</c> or
/// <c>ExpressionAttributeValues</c> from multiple expression results
/// produces a key collision with different values.
/// </summary>
public sealed class ExpressionAttributeConflictException : ExpressionMappingException
{
    /// <summary>
    /// The alias key that conflicted (e.g. "#filt_0" or ":filt_v0").
    /// </summary>
    public string AliasKey { get; }

    /// <summary>
    /// The value already present in the target dictionary.
    /// </summary>
    public string ExistingValue { get; }

    /// <summary>
    /// The incoming value that conflicted, if available.
    /// Null for value placeholder conflicts where deep comparison is impractical.
    /// </summary>
    public string? ConflictingValue { get; }

    public ExpressionAttributeConflictException(
        string aliasKey, string existingValue, string? conflictingValue = null)
        : base(conflictingValue is not null
            ? $"Attribute alias '{aliasKey}' conflicts: existing '{existingValue}', incoming '{conflictingValue}'."
            : $"Attribute alias '{aliasKey}' already exists with value '{existingValue}'.")
    {
        AliasKey = aliasKey;
        ExistingValue = existingValue;
        ConflictingValue = conflictingValue;
    }
}
```

### 5. InvalidExpressionException (Abstract Intermediate)

Groups the four builder-validation exceptions. Consumers who want to catch any "you built an expression wrong" error can catch this type.

```csharp
namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Base class for exceptions thrown when a builder detects an invalid
/// expression input (e.g. ignored properties, non-boolean filters,
/// conflicting update clauses). Catch this type at builder boundaries.
/// </summary>
public abstract class InvalidExpressionException : ExpressionMappingException
{
    /// <summary>
    /// The C# property name that caused the validation failure, if applicable.
    /// </summary>
    public string? PropertyName { get; }

    /// <summary>
    /// The entity type being processed when the error occurred.
    /// </summary>
    public Type? EntityType { get; }

    /// <summary>
    /// The resolved DynamoDB attribute name, if resolution succeeded before
    /// the error was detected. Null when the error prevented resolution.
    /// </summary>
    public string? AttributeName { get; }

    protected InvalidExpressionException(
        string message,
        string? propertyName = null,
        Type? entityType = null,
        string? attributeName = null)
        : base(message)
    {
        PropertyName = propertyName;
        EntityType = entityType;
        AttributeName = attributeName;
    }
}
```

### 6. InvalidProjectionException

Thrown by `ProjectionBuilder` (Spec 03 §4b, §7) when a projection selector references a property that cannot be projected.

```csharp
namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when a projection expression references a property that cannot
/// be projected (e.g. marked with <c>[DynamoDbIgnore]</c> in strict mode).
/// </summary>
public sealed class InvalidProjectionException : InvalidExpressionException
{
    public InvalidProjectionException(string propertyName, Type entityType)
        : base(
            $"Cannot project property '{propertyName}' on '{entityType.Name}': " +
            "property is marked [DynamoDbIgnore] or is not a stored attribute.",
            propertyName,
            entityType)
    { }
}
```

### 7. InvalidFilterException

Thrown by `FilterExpressionBuilder` / `ConditionExpressionBuilder` (Spec 06 §9) for invalid filter predicates.

```csharp
namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when a filter or condition predicate references an ignored property
/// (in strict mode) or is not a boolean expression.
/// </summary>
public sealed class InvalidFilterException : InvalidExpressionException
{
    public InvalidFilterException(string message, string? propertyName = null, Type? entityType = null)
        : base(message, propertyName, entityType)
    { }
}
```

### 8. InvalidUpdateException

Thrown by `UpdateExpressionBuilder` (Spec 07 §7) for invalid update operations.

```csharp
namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when an update expression targets an ignored property or contains
/// conflicting operations on the same attribute (e.g. SET + REMOVE).
/// </summary>
public sealed class InvalidUpdateException : InvalidExpressionException
{
    public InvalidUpdateException(string message, string? propertyName = null, Type? entityType = null)
        : base(message, propertyName, entityType)
    { }
}
```

### 9. InvalidKeyConditionException

Thrown by `KeyConditionExpressionBuilder` (Spec 13 §8) for invalid key condition inputs.

```csharp
namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when a key condition expression references an ignored property
/// or a nested property path (key attributes must be top-level).
/// </summary>
public sealed class InvalidKeyConditionException : InvalidExpressionException
{
    public InvalidKeyConditionException(string message, string? propertyName = null, Type? entityType = null)
        : base(message, propertyName, entityType)
    { }
}
```

## Namespace

All exception types live in `DynamoDb.ExpressionMapping.Exceptions`.

## Serialization

The library targets .NET 8.0+, where `BinaryFormatter` is obsolete. No `[Serializable]` attribute or `(SerializationInfo, StreamingContext)` constructors are provided.

## Usage Patterns

### Catch-all at API boundary

```csharp
try
{
    var projection = projectionBuilder.BuildProjection(selector);
}
catch (ExpressionMappingException ex)
{
    logger.LogError(ex, "Expression mapping failed");
    throw;
}
```

### Catch specific validation errors

```csharp
try
{
    var filter = filterBuilder.BuildFilter(predicate);
}
catch (InvalidFilterException ex) when (ex.PropertyName is not null)
{
    // Handle ignored-property case with structured context
    logger.LogWarning("Filter references non-stored property {Property} on {Entity}",
        ex.PropertyName, ex.EntityType?.Name);
}
catch (UnsupportedExpressionException ex)
{
    logger.LogError("Unsupported expression node {NodeType}: {Expression}",
        ex.NodeType, ex.ExpressionText);
}
```

### Catch any builder validation error

```csharp
try
{
    // Could be projection, filter, update, or key condition
    var request = new QueryRequest { TableName = "Orders" }
        .WithKeyCondition(keyConditionBuilder, b => b.WithPartitionKey(o => o.PK, pk))
        .WithProjection(projectionBuilder, selector)
        .WithFilter(filterBuilder, predicate);
}
catch (InvalidExpressionException ex)
{
    // Any of: InvalidProjection, InvalidFilter, InvalidUpdate, InvalidKeyCondition
    logger.LogError("Invalid expression for property {Property}: {Message}",
        ex.PropertyName, ex.Message);
}
```
