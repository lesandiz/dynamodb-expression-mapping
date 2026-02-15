using System;
using System.Linq.Expressions;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Entry point for building a DynamoDB KeyConditionExpression.
/// </summary>
/// <typeparam name="TSource">The entity type to build key conditions for.</typeparam>
public interface IKeyConditionExpressionBuilder<TSource>
{
    /// <summary>
    /// Begins the key condition with a partition key equality check.
    /// </summary>
    /// <typeparam name="TValue">The type of the partition key property.</typeparam>
    /// <param name="property">Expression selecting the partition key property.</param>
    /// <param name="value">The value to compare the partition key against.</param>
    /// <returns>A builder that allows adding an optional sort key condition or building the result.</returns>
    ISortKeyConditionBuilder<TSource> WithPartitionKey<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);
}
