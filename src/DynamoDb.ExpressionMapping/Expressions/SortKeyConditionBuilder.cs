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
/// Intermediate builder after partition key is specified.
/// Provides sort key condition methods or builds a partition-only expression.
/// </summary>
/// <typeparam name="TSource">The entity type to build key conditions for.</typeparam>
internal sealed class SortKeyConditionBuilder<TSource> : ISortKeyConditionBuilder<TSource>
{
    private readonly IAttributeNameResolverFactory resolverFactory;
    private readonly IAttributeValueConverterRegistry converters;
    private readonly ReservedKeywordRegistry keywordRegistry;
    private readonly AliasGenerator aliasGen;
    private readonly ExpressionValueEmitter valueEmitter;
    private readonly Dictionary<string, string> names;
    private readonly Dictionary<string, AttributeValue> values;
    private readonly string partitionKeyExpression;

    internal SortKeyConditionBuilder(
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converters,
        ReservedKeywordRegistry keywordRegistry,
        AliasGenerator aliasGen,
        ExpressionValueEmitter valueEmitter,
        Dictionary<string, string> names,
        Dictionary<string, AttributeValue> values,
        string partitionKeyExpression)
    {
        this.resolverFactory = resolverFactory;
        this.converters = converters;
        this.keywordRegistry = keywordRegistry;
        this.aliasGen = aliasGen;
        this.valueEmitter = valueEmitter;
        this.names = names;
        this.values = values;
        this.partitionKeyExpression = partitionKeyExpression;
    }

    /// <summary>Builds a partition-key-only expression (no sort key condition).</summary>
    public KeyConditionExpressionResult Build()
    {
        return new KeyConditionExpressionResult(
            partitionKeyExpression,
            names,
            values);
    }

    /// <summary>Sort key equals value.</summary>
    public KeyConditionExpressionResult WithSortKeyEquals<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value)
    {
        return BuildSortKeyCondition(property, value, "=");
    }

    /// <summary>Sort key less than value.</summary>
    public KeyConditionExpressionResult WithSortKeyLessThan<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value)
    {
        return BuildSortKeyCondition(property, value, "<");
    }

    /// <summary>Sort key less than or equal to value.</summary>
    public KeyConditionExpressionResult WithSortKeyLessThanOrEqual<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value)
    {
        return BuildSortKeyCondition(property, value, "<=");
    }

    /// <summary>Sort key greater than value.</summary>
    public KeyConditionExpressionResult WithSortKeyGreaterThan<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value)
    {
        return BuildSortKeyCondition(property, value, ">");
    }

    /// <summary>Sort key greater than or equal to value.</summary>
    public KeyConditionExpressionResult WithSortKeyGreaterThanOrEqual<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value)
    {
        return BuildSortKeyCondition(property, value, ">=");
    }

    /// <summary>Sort key between two values (inclusive).</summary>
    public KeyConditionExpressionResult WithSortKeyBetween<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue low,
        TValue high)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(low);
        ArgumentNullException.ThrowIfNull(high);

        // Validate that low <= high for comparable types
        if (low is IComparable<TValue> comparableLow && comparableLow.CompareTo(high) > 0)
        {
            throw new ArgumentException("Low value must be less than or equal to high value in BETWEEN condition.", nameof(low));
        }

        var propertyPath = ExtractAndValidatePropertyPath(property);
        var (sortKeyExpr, propertyInfo) = ResolveSortKeyAttribute(propertyPath);

        // Convert both values
        var valueAlias1 = aliasGen.NextValue();
        values[valueAlias1] = valueEmitter.Emit(low, propertyInfo);

        var valueAlias2 = aliasGen.NextValue();
        values[valueAlias2] = valueEmitter.Emit(high, propertyInfo);

        // Build expression: "#key_0 = :key_v0 AND #key_1 BETWEEN :key_v1 AND :key_v2"
        var expression = $"{partitionKeyExpression} AND {sortKeyExpr} BETWEEN {valueAlias1} AND {valueAlias2}";

        return new KeyConditionExpressionResult(expression, names, values);
    }

    /// <summary>Sort key begins with a prefix (string/binary sort keys only).</summary>
    public KeyConditionExpressionResult WithSortKeyBeginsWith(
        Expression<Func<TSource, string>> property,
        string prefix)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (prefix == null)
        {
            throw new ArgumentException("Prefix cannot be null for begins_with condition.", nameof(prefix));
        }

        if (string.IsNullOrEmpty(prefix))
        {
            throw new ArgumentException("Prefix cannot be empty for begins_with condition.", nameof(prefix));
        }

        var propertyPath = ExtractAndValidatePropertyPath(property);
        var (sortKeyExpr, propertyInfo) = ResolveSortKeyAttribute(propertyPath);

        // Convert prefix value
        var valueAlias = aliasGen.NextValue();
        values[valueAlias] = valueEmitter.Emit(prefix, propertyInfo);

        // Build expression: "#key_0 = :key_v0 AND begins_with(#key_1, :key_v1)"
        var expression = $"{partitionKeyExpression} AND begins_with({sortKeyExpr}, {valueAlias})";

        return new KeyConditionExpressionResult(expression, names, values);
    }

    private KeyConditionExpressionResult BuildSortKeyCondition<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value,
        string op)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(value);

        var propertyPath = ExtractAndValidatePropertyPath(property);
        var (sortKeyExpr, propertyInfo) = ResolveSortKeyAttribute(propertyPath);

        // Convert value
        var valueAlias = aliasGen.NextValue();
        values[valueAlias] = valueEmitter.Emit(value, propertyInfo);

        // Build expression: "#key_0 = :key_v0 AND #key_1 {op} :key_v1"
        var expression = $"{partitionKeyExpression} AND {sortKeyExpr} {op} {valueAlias}";

        return new KeyConditionExpressionResult(expression, names, values);
    }

    private PropertyPath ExtractAndValidatePropertyPath<TValue>(Expression<Func<TSource, TValue>> property)
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

        var propertyPath = new PropertyPath(segments, properties.ToArray());

        // Validate it's a top-level property
        if (propertyPath.Segments.Count > 1)
        {
            throw new InvalidKeyConditionException(
                $"Key condition properties must be top-level attributes. Property '{propertyPath.FullPath}' is a nested path.",
                propertyPath.FullPath,
                typeof(TSource));
        }

        return propertyPath;
    }

    private (string sortKeyExpr, PropertyInfo propertyInfo) ResolveSortKeyAttribute(PropertyPath propertyPath)
    {
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

        // Alias if reserved keyword
        string sortKeyExpr;
        if (keywordRegistry.IsReserved(attributeName))
        {
            var alias = aliasGen.NextName();
            names[alias] = attributeName;
            sortKeyExpr = alias;
        }
        else
        {
            sortKeyExpr = attributeName;
        }

        return (sortKeyExpr, propertyInfo);
    }
}
