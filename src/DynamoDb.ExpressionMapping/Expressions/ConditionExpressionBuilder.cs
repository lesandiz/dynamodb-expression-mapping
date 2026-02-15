using System;
using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Builds DynamoDB ConditionExpression strings from C# lambda predicates.
/// Generic over TSource — the entity type whose attribute names are resolved.
/// Uses "cond" alias scope (#cond_0, :cond_v0) to prevent collisions.
/// </summary>
/// <typeparam name="TSource">The entity type to build condition expressions for.</typeparam>
public sealed class ConditionExpressionBuilder<TSource> : IConditionExpressionBuilder<TSource>
{
    private readonly IAttributeNameResolverFactory resolverFactory;
    private readonly IAttributeValueConverterRegistry converterRegistry;

    /// <summary>
    /// Creates a new ConditionExpressionBuilder.
    /// </summary>
    /// <param name="resolverFactory">Factory for resolving attribute names across types.</param>
    /// <param name="converterRegistry">Registry for converting .NET values to AttributeValue.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public ConditionExpressionBuilder(
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converterRegistry)
    {
        this.resolverFactory = resolverFactory ?? throw new ArgumentNullException(nameof(resolverFactory));
        this.converterRegistry = converterRegistry ?? throw new ArgumentNullException(nameof(converterRegistry));
    }

    /// <summary>
    /// Builds a DynamoDB condition expression from a predicate.
    /// </summary>
    /// <param name="predicate">A lambda expression representing a boolean condition.</param>
    /// <returns>A ConditionExpressionResult containing the expression string and attribute mappings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is null.</exception>
    /// <exception cref="UnsupportedExpressionException">Thrown when the predicate contains unsupported operations.</exception>
    /// <exception cref="InvalidConditionException">Thrown when the predicate is invalid (e.g., non-boolean expression, uses ignored properties).</exception>
    public ConditionExpressionResult BuildCondition(Expression<Func<TSource, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        // Create scoped alias generator for condition expressions
        var aliasGen = new AliasGenerator("cond");

        // Create dictionaries for attribute names and values
        var names = new Dictionary<string, string>();
        var values = new Dictionary<string, AttributeValue>();

        // Create StringBuilder for building the expression string
        var result = new StringBuilder();

        // Create ExpressionValueEmitter for value conversion
        var valueEmitter = new ExpressionValueEmitter(converterRegistry);

        // Create FilterExpressionVisitor with all dependencies
        var visitor = new FilterExpressionVisitor(
            resolverFactory,
            valueEmitter,
            aliasGen,
            result,
            names,
            values);

        // Visit the predicate body to build the expression
        visitor.Visit(predicate.Body);

        // Return the result
        return new ConditionExpressionResult(
            result.ToString(),
            names,
            values);
    }
}
