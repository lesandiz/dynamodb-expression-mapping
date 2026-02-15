using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Builds DynamoDB KeyConditionExpression strings via a staged fluent API.
/// Thread-safe: all state is captured in the returned ISortKeyConditionBuilder.
/// </summary>
/// <typeparam name="TSource">The entity type to build key conditions for.</typeparam>
public sealed class KeyConditionExpressionBuilder<TSource> : IKeyConditionExpressionBuilder<TSource>
{
    private readonly IAttributeNameResolverFactory resolverFactory;
    private readonly IAttributeValueConverterRegistry converters;

    /// <summary>
    /// Creates a new key condition expression builder.
    /// </summary>
    /// <param name="resolverFactory">Factory for resolving attribute names.</param>
    /// <param name="converters">Registry for converting .NET values to AttributeValue.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public KeyConditionExpressionBuilder(
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converters)
    {
        this.resolverFactory = resolverFactory ?? throw new ArgumentNullException(nameof(resolverFactory));
        this.converters = converters ?? throw new ArgumentNullException(nameof(converters));
    }

    /// <summary>
    /// Begins the key condition with a partition key equality check.
    /// </summary>
    /// <typeparam name="TValue">The type of the partition key property.</typeparam>
    /// <param name="property">Expression selecting the partition key property.</param>
    /// <param name="value">The value to compare the partition key against.</param>
    /// <returns>A builder that allows adding an optional sort key condition or building the result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when property or value is null.</exception>
    /// <exception cref="InvalidKeyConditionException">Thrown when property is ignored or nested.</exception>
    public ISortKeyConditionBuilder<TSource> WithPartitionKey<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(value);

        // Extract property path and validate it's a top-level property
        var propertyPath = ExtractPropertyPath(property);

        if (propertyPath.Segments.Count > 1)
        {
            throw new InvalidKeyConditionException(
                $"Key condition properties must be top-level attributes. Property '{propertyPath.FullPath}' is a nested path.",
                propertyPath.FullPath,
                typeof(TSource));
        }

        // Resolve attribute name and check if property is stored
        var propertyInfo = propertyPath.PropertyInfo;
        var resolver = resolverFactory.GetResolver(typeof(TSource));

        if (!resolver.IsStoredAttribute(propertyInfo.Name))
        {
            throw new InvalidKeyConditionException(
                $"Property '{propertyInfo.Name}' is marked with [DynamoDbIgnore] and cannot be used in key conditions.",
                propertyInfo.Name,
                typeof(TSource));
        }

        var attributeName = resolver.GetAttributeName(propertyInfo.Name);

        // Initialize alias generator with "key" scope
        var keywordRegistry = ReservedKeywordRegistry.Default;
        var aliasGen = new AliasGenerator("key");
        var valueEmitter = new ExpressionValueEmitter(converters);

        var names = new Dictionary<string, string>();
        var values = new Dictionary<string, AttributeValue>();

        // Alias if reserved keyword
        string partitionKeyExpr;
        if (keywordRegistry.IsReserved(attributeName))
        {
            var alias = aliasGen.NextName();
            names[alias] = attributeName;
            partitionKeyExpr = alias;
        }
        else
        {
            partitionKeyExpr = attributeName;
        }

        // Convert value
        var valueAlias = aliasGen.NextValue();
        values[valueAlias] = valueEmitter.Emit(value, propertyInfo);

        // Build partition key expression: "#key_0 = :key_v0"
        var expression = $"{partitionKeyExpr} = {valueAlias}";

        return new SortKeyConditionBuilder<TSource>(
            resolverFactory,
            converters,
            keywordRegistry,
            aliasGen,
            valueEmitter,
            names,
            values,
            expression);
    }

    private PropertyPath ExtractPropertyPath<TValue>(Expression<Func<TSource, TValue>> property)
    {
        var segments = new List<string>();
        var properties = new List<PropertyInfo>();

        var current = property.Body;

        // Unwrap Convert expressions
        while (current is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            current = unary.Operand;
        }

        while (current is MemberExpression memberExpr)
        {
            if (memberExpr.Member is PropertyInfo propInfo)
            {
                segments.Insert(0, propInfo.Name);
                properties.Insert(0, propInfo);
                current = memberExpr.Expression;
            }
            else
            {
                throw new UnsupportedExpressionException(memberExpr.NodeType, memberExpr.ToString());
            }
        }

        // Current should be the parameter expression (p)
        if (current is not ParameterExpression)
        {
            throw new UnsupportedExpressionException(current?.NodeType ?? ExpressionType.Extension, current?.ToString() ?? "null");
        }

        if (segments.Count == 0)
        {
            throw new UnsupportedExpressionException(property.Body.NodeType, property.Body.ToString());
        }

        return new PropertyPath(segments, properties.ToArray());
    }
}
