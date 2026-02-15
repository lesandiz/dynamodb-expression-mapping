using System;
using System.Linq.Expressions;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Intermediate builder after partition key is specified.
/// Provides sort key condition methods or builds a partition-only expression.
/// </summary>
/// <typeparam name="TSource">The entity type to build key conditions for.</typeparam>
public interface ISortKeyConditionBuilder<TSource>
{
    /// <summary>Builds a partition-key-only expression (no sort key condition).</summary>
    KeyConditionExpressionResult Build();

    /// <summary>Sort key equals value.</summary>
    /// <typeparam name="TValue">The type of the sort key property.</typeparam>
    /// <param name="property">Expression selecting the sort key property.</param>
    /// <param name="value">The value to compare the sort key against.</param>
    KeyConditionExpressionResult WithSortKeyEquals<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>Sort key less than value.</summary>
    /// <typeparam name="TValue">The type of the sort key property.</typeparam>
    /// <param name="property">Expression selecting the sort key property.</param>
    /// <param name="value">The value to compare the sort key against.</param>
    KeyConditionExpressionResult WithSortKeyLessThan<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>Sort key less than or equal to value.</summary>
    /// <typeparam name="TValue">The type of the sort key property.</typeparam>
    /// <param name="property">Expression selecting the sort key property.</param>
    /// <param name="value">The value to compare the sort key against.</param>
    KeyConditionExpressionResult WithSortKeyLessThanOrEqual<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>Sort key greater than value.</summary>
    /// <typeparam name="TValue">The type of the sort key property.</typeparam>
    /// <param name="property">Expression selecting the sort key property.</param>
    /// <param name="value">The value to compare the sort key against.</param>
    KeyConditionExpressionResult WithSortKeyGreaterThan<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>Sort key greater than or equal to value.</summary>
    /// <typeparam name="TValue">The type of the sort key property.</typeparam>
    /// <param name="property">Expression selecting the sort key property.</param>
    /// <param name="value">The value to compare the sort key against.</param>
    KeyConditionExpressionResult WithSortKeyGreaterThanOrEqual<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>Sort key between two values (inclusive).</summary>
    /// <typeparam name="TValue">The type of the sort key property.</typeparam>
    /// <param name="property">Expression selecting the sort key property.</param>
    /// <param name="low">The lower bound value (inclusive).</param>
    /// <param name="high">The upper bound value (inclusive).</param>
    KeyConditionExpressionResult WithSortKeyBetween<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue low,
        TValue high);

    /// <summary>Sort key begins with a prefix (string/binary sort keys only).</summary>
    /// <param name="property">Expression selecting the sort key property.</param>
    /// <param name="prefix">The prefix value to match.</param>
    KeyConditionExpressionResult WithSortKeyBeginsWith(
        Expression<Func<TSource, string>> property,
        string prefix);
}
