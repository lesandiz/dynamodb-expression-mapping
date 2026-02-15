using System;
using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Builds DynamoDB FilterExpression strings from C# lambda predicates.
/// Converts expression trees to DynamoDB syntax with proper attribute name aliasing
/// and value placeholder generation.
/// </summary>
/// <typeparam name="TSource">The entity type to build filter expressions for.</typeparam>
public sealed class FilterExpressionBuilder<TSource> : IFilterExpressionBuilder<TSource>
{
    private readonly IAttributeNameResolverFactory resolverFactory;
    private readonly IAttributeValueConverterRegistry converterRegistry;

    /// <summary>
    /// Creates a new filter expression builder.
    /// </summary>
    /// <param name="resolverFactory">Factory for resolving attribute names across types.</param>
    /// <param name="converterRegistry">Registry for converting .NET values to AttributeValue.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public FilterExpressionBuilder(
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converterRegistry)
    {
        this.resolverFactory = resolverFactory ?? throw new ArgumentNullException(nameof(resolverFactory));
        this.converterRegistry = converterRegistry ?? throw new ArgumentNullException(nameof(converterRegistry));
    }

    /// <summary>
    /// Builds a DynamoDB filter expression from a predicate.
    /// </summary>
    /// <param name="predicate">A lambda expression representing a boolean filter condition.</param>
    /// <returns>A FilterExpressionResult containing the expression string and attribute mappings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is null.</exception>
    /// <exception cref="UnsupportedExpressionException">Thrown when the predicate contains unsupported operations.</exception>
    /// <exception cref="InvalidFilterException">Thrown when the predicate is invalid (e.g., non-boolean expression, uses ignored properties).</exception>
    public FilterExpressionResult BuildFilter(Expression<Func<TSource, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        // Create scoped alias generator for filter expressions
        var aliasGen = new AliasGenerator("filt");

        // Create dictionaries for attribute names and values
        var names = new Dictionary<string, string>();
        var values = new Dictionary<string, AttributeValue>();

        // Create StringBuilder for building the expression string
        var expression = new StringBuilder();

        // Create value emitter for converting .NET values to AttributeValue
        var valueEmitter = new ExpressionValueEmitter(converterRegistry);

        // Create visitor with all dependencies
        var visitor = new FilterExpressionVisitor(
            resolverFactory,
            valueEmitter,
            aliasGen,
            expression,
            names,
            values);

        // Visit the predicate body to build the expression
        visitor.Visit(predicate.Body);

        // Return immutable result
        return new FilterExpressionResult(
            expression.ToString(),
            names,
            values);
    }
}
