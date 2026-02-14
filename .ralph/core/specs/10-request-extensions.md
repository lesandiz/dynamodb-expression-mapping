# Spec 10: AWS SDK Request Extensions

## Motivation

The library must integrate smoothly with the AWS SDK for .NET's request types. Extension methods provide a fluent API for applying projections, filters, conditions, and updates to DynamoDB requests without requiring consumers to manually manage `ProjectionExpression`, `ExpressionAttributeNames`, and `ExpressionAttributeValues` dictionaries.

## Design

### 1. Projection Extensions

All extensions accept `IProjectionBuilder<TSource>`, with `TSource` inferred from the builder instance.

```csharp
namespace DynamoDb.ExpressionMapping.Extensions;

public static class ProjectionExtensions
{
    public static GetItemRequest WithProjection<TSource, TResult>(
        this GetItemRequest request,
        IProjectionBuilder<TSource> projectionBuilder,
        Expression<Func<TSource, TResult>> selector)
    {
        var result = projectionBuilder.BuildProjection(selector);
        return request.ApplyProjection(result);
    }

    public static QueryRequest WithProjection<TSource, TResult>(
        this QueryRequest request,
        IProjectionBuilder<TSource> projectionBuilder,
        Expression<Func<TSource, TResult>> selector)
    {
        if (selector == null) return request;
        var result = projectionBuilder.BuildProjection(selector);
        return request.ApplyProjection(result);
    }

    public static ScanRequest WithProjection<TSource, TResult>(
        this ScanRequest request,
        IProjectionBuilder<TSource> projectionBuilder,
        Expression<Func<TSource, TResult>> selector)
    {
        if (selector == null) return request;
        var result = projectionBuilder.BuildProjection(selector);
        return request.ApplyProjection(result);
    }

    public static BatchGetItemRequest WithProjection<TSource, TResult>(
        this BatchGetItemRequest request,
        string tableName,
        IProjectionBuilder<TSource> projectionBuilder,
        Expression<Func<TSource, TResult>> selector)
    {
        if (selector == null) return request;

        ArgumentNullException.ThrowIfNull(request.RequestItems);

        if (!request.RequestItems.TryGetValue(tableName, out var keysAndAttributes))
            throw new ArgumentException(
                $"Table '{tableName}' not found in RequestItems.", nameof(tableName));

        var result = projectionBuilder.BuildProjection(selector);
        request.RequestItems[tableName] = keysAndAttributes.ApplyProjection(result);
        return request;
    }
}
```

### 2. Filter Extensions

```csharp
namespace DynamoDb.ExpressionMapping.Extensions;

public static class FilterExtensions
{
    public static QueryRequest WithFilter<TSource>(
        this QueryRequest request,
        IFilterExpressionBuilder<TSource> filterBuilder,
        Expression<Func<TSource, bool>> predicate)
    {
        var result = filterBuilder.BuildFilter(predicate);
        return request.ApplyFilter(result);
    }

    public static ScanRequest WithFilter<TSource>(
        this ScanRequest request,
        IFilterExpressionBuilder<TSource> filterBuilder,
        Expression<Func<TSource, bool>> predicate)
    {
        var result = filterBuilder.BuildFilter(predicate);
        return request.ApplyFilter(result);
    }
}
```

### 3. Condition Extensions

```csharp
namespace DynamoDb.ExpressionMapping.Extensions;

public static class ConditionExtensions
{
    public static PutItemRequest WithCondition<TSource>(
        this PutItemRequest request,
        IConditionExpressionBuilder<TSource> conditionBuilder,
        Expression<Func<TSource, bool>> predicate)
    {
        var result = conditionBuilder.BuildCondition(predicate);
        return request.ApplyCondition(result);
    }

    public static DeleteItemRequest WithCondition<TSource>(
        this DeleteItemRequest request,
        IConditionExpressionBuilder<TSource> conditionBuilder,
        Expression<Func<TSource, bool>> predicate)
    {
        var result = conditionBuilder.BuildCondition(predicate);
        return request.ApplyCondition(result);
    }

    public static UpdateItemRequest WithCondition<TSource>(
        this UpdateItemRequest request,
        IConditionExpressionBuilder<TSource> conditionBuilder,
        Expression<Func<TSource, bool>> predicate)
    {
        var result = conditionBuilder.BuildCondition(predicate);
        return request.ApplyCondition(result);
    }
}
```

### 4. Key Condition Extensions

```csharp
namespace DynamoDb.ExpressionMapping.Extensions;

public static class KeyConditionExtensions
{
    public static QueryRequest WithKeyCondition<TSource>(
        this QueryRequest request,
        IKeyConditionExpressionBuilder<TSource> keyConditionBuilder,
        Func<IKeyConditionExpressionBuilder<TSource>, KeyConditionExpressionResult> configure)
    {
        ArgumentNullException.ThrowIfNull(keyConditionBuilder);
        ArgumentNullException.ThrowIfNull(configure);

        var result = configure(keyConditionBuilder);

        request.KeyConditionExpression = result.Expression;
        request.MergeAttributeNames(result.ExpressionAttributeNames);
        request.MergeAttributeValues(result.ExpressionAttributeValues);

        return request;
    }
}
```

The `Func<IKeyConditionExpressionBuilder<TSource>, KeyConditionExpressionResult>` delegate accepts the builder and returns a result, allowing the staged fluent API (`WithPartitionKey` → sort key method → result) to be expressed inline:

```csharp
.WithKeyCondition(keyConditionBuilder,
    b => b.WithPartitionKey(o => o.PK, "USER#123")
          .WithSortKeyBeginsWith(o => o.SK, "ORDER#"))
```

### 5. Update Extensions

```csharp
namespace DynamoDb.ExpressionMapping.Extensions;

public static class UpdateExtensions
{
    public static UpdateItemRequest WithUpdate(
        this UpdateItemRequest request,
        UpdateExpressionResult updateResult)
    {
        if (updateResult.IsEmpty) return request;

        request.UpdateExpression = updateResult.Expression;
        request.MergeAttributeNames(updateResult.ExpressionAttributeNames);
        request.MergeAttributeValues(updateResult.ExpressionAttributeValues);

        return request;
    }
}
```

### 6. Shared Merge Logic

Internal helpers eliminate duplication and enforce collision detection:

```csharp
namespace DynamoDb.ExpressionMapping.Extensions;

internal static class RequestMergeHelpers
{
    /// <exception cref="ExpressionAttributeConflictException">
    /// Thrown when a key exists in both dictionaries with different values (Spec 14 §4).
    /// </exception>
    internal static void MergeAttributeNames(
        Dictionary<string, string> target,
        IReadOnlyDictionary<string, string> source)
    {
        foreach (var kvp in source)
        {
            if (target.TryGetValue(kvp.Key, out var existing))
            {
                if (existing != kvp.Value)
                {
                    throw new ExpressionAttributeConflictException(
                        kvp.Key, existing, kvp.Value);
                }
                continue; // Same mapping already exists
            }
            target[kvp.Key] = kvp.Value;
        }
    }

    /// <exception cref="ExpressionAttributeConflictException">
    /// Thrown when a placeholder key already exists in the target (Spec 14 §4).
    /// </exception>
    internal static void MergeAttributeValues(
        Dictionary<string, AttributeValue> target,
        IReadOnlyDictionary<string, AttributeValue> source)
    {
        foreach (var kvp in source)
        {
            if (target.TryGetValue(kvp.Key, out var existing))
            {
                throw new ExpressionAttributeConflictException(
                    kvp.Key, existing.S ?? existing.N ?? "(value)", null);
            }
            target[kvp.Key] = kvp.Value;
        }
    }
}
```

### 7. Fluent Chaining

All extension methods return the request object for fluent chaining:

```csharp
// keyConditionBuilder is IKeyConditionExpressionBuilder<Order>
// projectionBuilder is IProjectionBuilder<Order>
// filterBuilder is IFilterExpressionBuilder<Order>
var request = new QueryRequest
{
    TableName = tableName,
    IndexName = indexName
}
.WithKeyCondition(keyConditionBuilder,
    b => b.WithPartitionKey(o => o.PK, pkValue)
          .WithSortKeyBeginsWith(o => o.SK, "ORDER#"))
.WithProjection(projectionBuilder,
    p => new OrderSummary { Id = p.OrderId, Total = p.Total })
.WithFilter(filterBuilder,
    p => p.Status == OrderStatus.Active && p.Total > 100);

// orderProjectionBuilder is IProjectionBuilder<Order>
// productProjectionBuilder is IProjectionBuilder<Product>
var batchRequest = new BatchGetItemRequest
{
    RequestItems = new Dictionary<string, KeysAndAttributes>
    {
        ["Orders"] = new KeysAndAttributes { Keys = orderKeys },
        ["Products"] = new KeysAndAttributes { Keys = productKeys }
    }
}
.WithProjection("Orders", orderProjectionBuilder,
    o => new { o.OrderId, o.Total })
.WithProjection("Products", productProjectionBuilder,
    p => new { p.Name, p.Price });
```

### 8. Null-Safe Behaviour

All extensions handle null selectors/predicates gracefully:
- Null selector → no projection applied (all attributes returned)
- Null predicate → no filter applied
- Null builder → `ArgumentNullException`

### 9. Combined Extension

For convenience, a combined extension that applies projection + filter in one call:

```csharp
public static QueryRequest WithExpressions<TSource, TResult>(
    this QueryRequest request,
    IProjectionBuilder<TSource> projectionBuilder,
    Expression<Func<TSource, TResult>> selector,
    IFilterExpressionBuilder<TSource> filterBuilder,
    Expression<Func<TSource, bool>> predicate)
{
    return request
        .WithProjection(projectionBuilder, selector)
        .WithFilter(filterBuilder, predicate);
}
```
