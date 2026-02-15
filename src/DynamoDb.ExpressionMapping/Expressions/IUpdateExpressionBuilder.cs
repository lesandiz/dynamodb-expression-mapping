using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Builds DynamoDB UpdateExpression strings from a fluent builder API.
/// </summary>
/// <typeparam name="TSource">The entity type to build update expressions for.</typeparam>
public interface IUpdateExpressionBuilder<TSource>
{
    /// <summary>SET attr = value</summary>
    IUpdateExpressionBuilder<TSource> Set<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>SET attr = attr + value (increment)</summary>
    IUpdateExpressionBuilder<TSource> Increment<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue amount) where TValue : struct;

    /// <summary>SET attr = attr - value (decrement)</summary>
    IUpdateExpressionBuilder<TSource> Decrement<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue amount) where TValue : struct;

    /// <summary>SET attr = if_not_exists(attr, value)</summary>
    IUpdateExpressionBuilder<TSource> SetIfNotExists<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>SET attr = list_append(attr, value)</summary>
    IUpdateExpressionBuilder<TSource> AppendToList<TValue>(
        Expression<Func<TSource, List<TValue>>> property,
        List<TValue> values);

    /// <summary>REMOVE attr</summary>
    IUpdateExpressionBuilder<TSource> Remove<TValue>(
        Expression<Func<TSource, TValue>> property);

    /// <summary>ADD value to number or set</summary>
    IUpdateExpressionBuilder<TSource> Add<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>DELETE value from set</summary>
    IUpdateExpressionBuilder<TSource> Delete<TValue>(
        Expression<Func<TSource, HashSet<TValue>>> property,
        HashSet<TValue> values);

    /// <summary>Build the final expression result.</summary>
    UpdateExpressionResult Build();
}
