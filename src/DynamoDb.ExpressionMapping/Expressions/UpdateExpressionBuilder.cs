using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Builds DynamoDB UpdateExpression strings from a fluent builder API.
/// Thread-safe: designed for singleton registration. Each fluent method returns a new instance.
/// </summary>
/// <typeparam name="TSource">The entity type to build update expressions for.</typeparam>
public sealed class UpdateExpressionBuilder<TSource> : IUpdateExpressionBuilder<TSource>
{
    // Immutable dependencies — shared across all instances in a fluent chain
    private readonly IAttributeNameResolverFactory resolverFactory;
    private readonly IAttributeValueConverterRegistry converterRegistry;
    private readonly ReservedKeywordRegistry keywordRegistry;

    // Mutable state — each fluent method creates a new instance with cloned + extended state
    private readonly AliasGenerator aliasGen;
    private readonly ExpressionValueEmitter valueEmitter;
    private readonly Dictionary<string, string> names;
    private readonly Dictionary<string, AttributeValue> values;
    private readonly Dictionary<string, UpdateOperation> setOperations;
    private readonly HashSet<string> removeProperties;
    private readonly Dictionary<string, UpdateOperation> addOperations;
    private readonly Dictionary<string, UpdateOperation> deleteOperations;

    /// <summary>
    /// Creates a new update expression builder.
    /// </summary>
    /// <param name="resolverFactory">Factory for resolving attribute names across types.</param>
    /// <param name="converterRegistry">Registry for converting .NET values to AttributeValue.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public UpdateExpressionBuilder(
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converterRegistry)
        : this(resolverFactory, converterRegistry, ReservedKeywordRegistry.Default)
    {
    }

    internal UpdateExpressionBuilder(
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converterRegistry,
        ReservedKeywordRegistry keywordRegistry)
    {
        this.resolverFactory = resolverFactory ?? throw new ArgumentNullException(nameof(resolverFactory));
        this.converterRegistry = converterRegistry ?? throw new ArgumentNullException(nameof(converterRegistry));
        this.keywordRegistry = keywordRegistry ?? throw new ArgumentNullException(nameof(keywordRegistry));

        // Initialize fresh state for a new fluent chain
        aliasGen = new AliasGenerator("upd");
        valueEmitter = new ExpressionValueEmitter(converterRegistry);
        names = new Dictionary<string, string>();
        values = new Dictionary<string, AttributeValue>();
        setOperations = new Dictionary<string, UpdateOperation>();
        removeProperties = new HashSet<string>();
        addOperations = new Dictionary<string, UpdateOperation>();
        deleteOperations = new Dictionary<string, UpdateOperation>();
    }

    /// <summary>
    /// Private constructor for cloning — creates a new instance with copied state.
    /// </summary>
    private UpdateExpressionBuilder(
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converterRegistry,
        ReservedKeywordRegistry keywordRegistry,
        AliasGenerator aliasGen,
        Dictionary<string, string> names,
        Dictionary<string, AttributeValue> values,
        Dictionary<string, UpdateOperation> setOperations,
        HashSet<string> removeProperties,
        Dictionary<string, UpdateOperation> addOperations,
        Dictionary<string, UpdateOperation> deleteOperations)
    {
        this.resolverFactory = resolverFactory;
        this.converterRegistry = converterRegistry;
        this.keywordRegistry = keywordRegistry;
        this.aliasGen = aliasGen;
        this.valueEmitter = new ExpressionValueEmitter(converterRegistry);
        this.names = names;
        this.values = values;
        this.setOperations = setOperations;
        this.removeProperties = removeProperties;
        this.addOperations = addOperations;
        this.deleteOperations = deleteOperations;
    }

    public IUpdateExpressionBuilder<TSource> Set<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);

        // Clone current state
        var clone = Clone();

        var propertyPath = clone.ExtractPropertyPath(property);
        var attributeName = clone.ResolveAttributeName(propertyPath);

        // Remove old value placeholder if this property was already set (last-wins semantics)
        if (clone.setOperations.TryGetValue(propertyPath.FullPath, out var existingOp))
        {
            clone.RemoveOldValuePlaceholders(existingOp.Expression);
        }

        var valueAlias = clone.aliasGen.NextValue();

        clone.values[valueAlias] = clone.valueEmitter.Emit(value!, propertyPath.PropertyInfo);

        var operation = new UpdateOperation(
            UpdateOperationType.Set,
            attributeName,
            $"{attributeName} = {valueAlias}");

        clone.CheckForConflicts(propertyPath.FullPath, UpdateOperationType.Set);
        clone.setOperations[propertyPath.FullPath] = operation;

        return clone;
    }

    public IUpdateExpressionBuilder<TSource> Increment<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue amount) where TValue : struct
    {
        ArgumentNullException.ThrowIfNull(property);

        // Clone current state
        var clone = Clone();

        var propertyPath = clone.ExtractPropertyPath(property);
        var attributeName = clone.ResolveAttributeName(propertyPath);

        // Remove old value placeholder if this property was already set (last-wins semantics)
        if (clone.setOperations.TryGetValue(propertyPath.FullPath, out var existingOp))
        {
            clone.RemoveOldValuePlaceholders(existingOp.Expression);
        }

        var valueAlias = clone.aliasGen.NextValue();

        clone.values[valueAlias] = clone.valueEmitter.Emit(amount, propertyPath.PropertyInfo);

        var operation = new UpdateOperation(
            UpdateOperationType.Set,
            attributeName,
            $"{attributeName} = {attributeName} + {valueAlias}");

        clone.CheckForConflicts(propertyPath.FullPath, UpdateOperationType.Set);
        clone.setOperations[propertyPath.FullPath] = operation;

        return clone;
    }

    public IUpdateExpressionBuilder<TSource> Decrement<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue amount) where TValue : struct
    {
        ArgumentNullException.ThrowIfNull(property);

        // Clone current state
        var clone = Clone();

        var propertyPath = clone.ExtractPropertyPath(property);
        var attributeName = clone.ResolveAttributeName(propertyPath);

        // Remove old value placeholder if this property was already set (last-wins semantics)
        if (clone.setOperations.TryGetValue(propertyPath.FullPath, out var existingOp))
        {
            clone.RemoveOldValuePlaceholders(existingOp.Expression);
        }

        var valueAlias = clone.aliasGen.NextValue();

        clone.values[valueAlias] = clone.valueEmitter.Emit(amount, propertyPath.PropertyInfo);

        var operation = new UpdateOperation(
            UpdateOperationType.Set,
            attributeName,
            $"{attributeName} = {attributeName} - {valueAlias}");

        clone.CheckForConflicts(propertyPath.FullPath, UpdateOperationType.Set);
        clone.setOperations[propertyPath.FullPath] = operation;

        return clone;
    }

    public IUpdateExpressionBuilder<TSource> SetIfNotExists<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);

        // Clone current state
        var clone = Clone();

        var propertyPath = clone.ExtractPropertyPath(property);
        var attributeName = clone.ResolveAttributeName(propertyPath);

        // Remove old value placeholder if this property was already set (last-wins semantics)
        if (clone.setOperations.TryGetValue(propertyPath.FullPath, out var existingOp))
        {
            clone.RemoveOldValuePlaceholders(existingOp.Expression);
        }

        var valueAlias = clone.aliasGen.NextValue();

        clone.values[valueAlias] = clone.valueEmitter.Emit(value!, propertyPath.PropertyInfo);

        var operation = new UpdateOperation(
            UpdateOperationType.Set,
            attributeName,
            $"{attributeName} = if_not_exists({attributeName}, {valueAlias})");

        clone.CheckForConflicts(propertyPath.FullPath, UpdateOperationType.Set);
        clone.setOperations[propertyPath.FullPath] = operation;

        return clone;
    }

    public IUpdateExpressionBuilder<TSource> AppendToList<TValue>(
        Expression<Func<TSource, List<TValue>>> property,
        List<TValue> values)
    {
        ArgumentNullException.ThrowIfNull(property);

        // Clone current state
        var clone = Clone();

        var propertyPath = clone.ExtractPropertyPath(property);
        var attributeName = clone.ResolveAttributeName(propertyPath);

        // Remove old value placeholder if this property was already set (last-wins semantics)
        if (clone.setOperations.TryGetValue(propertyPath.FullPath, out var existingOp))
        {
            clone.RemoveOldValuePlaceholders(existingOp.Expression);
        }

        var valueAlias = clone.aliasGen.NextValue();

        clone.values[valueAlias] = clone.valueEmitter.Emit(values, propertyPath.PropertyInfo);

        var operation = new UpdateOperation(
            UpdateOperationType.Set,
            attributeName,
            $"{attributeName} = list_append({attributeName}, {valueAlias})");

        clone.CheckForConflicts(propertyPath.FullPath, UpdateOperationType.Set);
        clone.setOperations[propertyPath.FullPath] = operation;

        return clone;
    }

    public IUpdateExpressionBuilder<TSource> Remove<TValue>(
        Expression<Func<TSource, TValue>> property)
    {
        ArgumentNullException.ThrowIfNull(property);

        // Clone current state
        var clone = Clone();

        var propertyPath = clone.ExtractPropertyPath(property);
        var attributeName = clone.ResolveAttributeName(propertyPath);

        clone.CheckForConflicts(propertyPath.FullPath, UpdateOperationType.Remove);
        clone.removeProperties.Add(propertyPath.FullPath);

        return clone;
    }

    public IUpdateExpressionBuilder<TSource> Add<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);

        // Clone current state
        var clone = Clone();

        var propertyPath = clone.ExtractPropertyPath(property);
        var attributeName = clone.ResolveAttributeName(propertyPath);

        // Remove old value placeholder if this property was already set (last-wins semantics)
        if (clone.addOperations.TryGetValue(propertyPath.FullPath, out var existingOp))
        {
            clone.RemoveOldValuePlaceholders(existingOp.Expression);
        }

        var valueAlias = clone.aliasGen.NextValue();

        clone.values[valueAlias] = clone.valueEmitter.Emit(value!, propertyPath.PropertyInfo);

        var operation = new UpdateOperation(
            UpdateOperationType.Add,
            attributeName,
            $"{attributeName} {valueAlias}");

        clone.CheckForConflicts(propertyPath.FullPath, UpdateOperationType.Add);
        clone.addOperations[propertyPath.FullPath] = operation;

        return clone;
    }

    public IUpdateExpressionBuilder<TSource> Delete<TValue>(
        Expression<Func<TSource, HashSet<TValue>>> property,
        HashSet<TValue> values)
    {
        ArgumentNullException.ThrowIfNull(property);

        // Clone current state
        var clone = Clone();

        var propertyPath = clone.ExtractPropertyPath(property);
        var attributeName = clone.ResolveAttributeName(propertyPath);

        // Remove old value placeholder if this property was already set (last-wins semantics)
        if (clone.deleteOperations.TryGetValue(propertyPath.FullPath, out var existingOp))
        {
            clone.RemoveOldValuePlaceholders(existingOp.Expression);
        }

        var valueAlias = clone.aliasGen.NextValue();

        clone.values[valueAlias] = clone.valueEmitter.Emit(values, propertyPath.PropertyInfo);

        var operation = new UpdateOperation(
            UpdateOperationType.Delete,
            attributeName,
            $"{attributeName} {valueAlias}");

        clone.CheckForConflicts(propertyPath.FullPath, UpdateOperationType.Delete);
        clone.deleteOperations[propertyPath.FullPath] = operation;

        return clone;
    }

    public UpdateExpressionResult Build()
    {
        // If no operations were added, return empty result
        if (setOperations.Count == 0 && removeProperties.Count == 0 &&
            addOperations.Count == 0 && deleteOperations.Count == 0)
        {
            return UpdateExpressionResult.Empty;
        }

        var expressionParts = new List<string>();

        // Build SET clause
        if (setOperations.Count > 0)
        {
            var setClauses = setOperations.Values.Select(op => op.Expression);
            expressionParts.Add($"SET {string.Join(", ", setClauses)}");
        }

        // Build REMOVE clause
        if (removeProperties.Count > 0)
        {
            var removeAttrs = removeProperties.Select(propPath =>
            {
                // Re-resolve the attribute name for each removed property
                var segments = propPath.Split('.');
                return ResolveAttributeNameFromSegments(segments);
            });
            expressionParts.Add($"REMOVE {string.Join(", ", removeAttrs)}");
        }

        // Build ADD clause
        if (addOperations.Count > 0)
        {
            var addClauses = addOperations.Values.Select(op => op.Expression);
            expressionParts.Add($"ADD {string.Join(", ", addClauses)}");
        }

        // BUILD DELETE clause
        if (deleteOperations.Count > 0)
        {
            var deleteClauses = deleteOperations.Values.Select(op => op.Expression);
            expressionParts.Add($"DELETE {string.Join(", ", deleteClauses)}");
        }

        var expression = string.Join(" ", expressionParts);

        return new UpdateExpressionResult(expression, names, values);
    }

    /// <summary>
    /// Clones this instance with all current state, creating a new independent fluent chain.
    /// </summary>
    private UpdateExpressionBuilder<TSource> Clone()
    {
        return new UpdateExpressionBuilder<TSource>(
            resolverFactory,
            converterRegistry,
            keywordRegistry,
            aliasGen.Clone(),
            new Dictionary<string, string>(names),
            new Dictionary<string, AttributeValue>(values),
            new Dictionary<string, UpdateOperation>(setOperations),
            new HashSet<string>(removeProperties),
            new Dictionary<string, UpdateOperation>(addOperations),
            new Dictionary<string, UpdateOperation>(deleteOperations));
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

    private string ResolveAttributeName(PropertyPath propertyPath)
    {
        var segments = new List<string>();

        for (int i = 0; i < propertyPath.Segments.Count; i++)
        {
            var segmentProperty = propertyPath.SegmentProperties[i];
            var declaringType = segmentProperty.DeclaringType!;
            var resolver = resolverFactory.GetResolver(declaringType);

            // Check if property is stored
            if (!resolver.IsStoredAttribute(segmentProperty.Name))
            {
                throw new InvalidUpdateException(
                    $"Property '{propertyPath.FullPath}' is marked with [DynamoDbIgnore] and cannot be used in update expressions",
                    propertyPath.FullPath,
                    declaringType);
            }

            var attributeName = resolver.GetAttributeName(segmentProperty.Name);

            // Alias if reserved keyword
            if (keywordRegistry.IsReserved(attributeName))
            {
                var alias = aliasGen.NextName();
                names[alias] = attributeName;
                segments.Add(alias);
            }
            else
            {
                segments.Add(attributeName);
            }
        }

        return string.Join(".", segments);
    }

    private string ResolveAttributeNameFromSegments(string[] segments)
    {
        // This is a simplified version for REMOVE clause reconstruction
        // In a real scenario, we'd need to cache the resolved names
        // For now, we'll use the already-aliased names from the first resolution
        var result = new List<string>();

        foreach (var segment in segments)
        {
            // Find if this segment was already aliased
            var alias = names.FirstOrDefault(kvp => kvp.Value == segment).Key;
            result.Add(alias ?? segment);
        }

        return string.Join(".", result);
    }

    private void CheckForConflicts(string propertyPath, UpdateOperationType operationType)
    {
        // Check for conflicting operations
        bool hasConflict = operationType switch
        {
            UpdateOperationType.Set => removeProperties.Contains(propertyPath) ||
                                      addOperations.ContainsKey(propertyPath) ||
                                      deleteOperations.ContainsKey(propertyPath),
            UpdateOperationType.Remove => setOperations.ContainsKey(propertyPath) ||
                                         addOperations.ContainsKey(propertyPath) ||
                                         deleteOperations.ContainsKey(propertyPath),
            UpdateOperationType.Add => setOperations.ContainsKey(propertyPath) ||
                                      removeProperties.Contains(propertyPath) ||
                                      deleteOperations.ContainsKey(propertyPath),
            UpdateOperationType.Delete => setOperations.ContainsKey(propertyPath) ||
                                         removeProperties.Contains(propertyPath) ||
                                         addOperations.ContainsKey(propertyPath),
            _ => false
        };

        if (hasConflict)
        {
            throw new InvalidUpdateException(
                $"Property '{propertyPath}' has conflicting update operations",
                propertyPath);
        }
    }

    /// <summary>
    /// Removes old value placeholders from the values dictionary when overwriting an existing operation.
    /// This prevents orphaned placeholders that are created but not used in the final expression.
    /// </summary>
    private void RemoveOldValuePlaceholders(string expression)
    {
        if (string.IsNullOrEmpty(expression))
            return;

        // Extract all value placeholders from the expression (e.g., :upd_v0, :upd_v1)
        var valuePlaceholderPattern = System.Text.RegularExpressions.Regex.Matches(
            expression,
            @":upd_v\d+");

        foreach (System.Text.RegularExpressions.Match match in valuePlaceholderPattern)
        {
            values.Remove(match.Value);
        }

        // Extract all name aliases from the expression (e.g., #upd_0, #upd_1)
        var nameAliasPattern = System.Text.RegularExpressions.Regex.Matches(
            expression,
            @"#upd_\d+");

        foreach (System.Text.RegularExpressions.Match match in nameAliasPattern)
        {
            names.Remove(match.Value);
        }
    }

    private sealed class UpdateOperation
    {
        public UpdateOperationType Type { get; }
        public string AttributeName { get; }
        public string Expression { get; }

        public UpdateOperation(UpdateOperationType type, string attributeName, string expression)
        {
            Type = type;
            AttributeName = attributeName;
            Expression = expression;
        }
    }

    private enum UpdateOperationType
    {
        Set,
        Remove,
        Add,
        Delete
    }
}
