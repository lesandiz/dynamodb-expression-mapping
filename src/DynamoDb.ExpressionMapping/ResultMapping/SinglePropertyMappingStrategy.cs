using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.ResultMapping;

/// <summary>
/// Handles single property projections: p => p.Foo or p => p.Address.City
/// Reads a single attribute from the dictionary and converts it to the target type.
/// </summary>
internal sealed class SinglePropertyMappingStrategy : IMappingStrategy
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;

    public SinglePropertyMappingStrategy(
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converterRegistry)
    {
        _resolverFactory = resolverFactory;
        _converterRegistry = converterRegistry;
    }

    public Func<Dictionary<string, AttributeValue>, TResult> BuildMapper<TSource, TResult>(
        Expression<Func<TSource, TResult>> selector)
    {
        // Extract the property path
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(selector, out var shape);

        if (shape != ProjectionShape.SingleProperty || paths.Count != 1)
        {
            throw new InvalidOperationException(
                "SinglePropertyMappingStrategy requires exactly one property path");
        }

        var propertyPath = paths[0];

        // Build a compiled delegate that reads the attribute and converts it
        return BuildSinglePropertyMapper<TSource, TResult>(propertyPath);
    }

    private Func<Dictionary<string, AttributeValue>, TResult> BuildSinglePropertyMapper<TSource, TResult>(
        PropertyPath propertyPath)
    {
        // Parameter: Dictionary<string, AttributeValue> attrs
        var attrsParam = Expression.Parameter(typeof(Dictionary<string, AttributeValue>), "attrs");

        // Build the attribute read expression
        var readExpression = BuildAttributeReadExpression(
            attrsParam,
            propertyPath,
            typeof(TSource),
            typeof(TResult));

        // Compile to delegate
        var lambda = Expression.Lambda<Func<Dictionary<string, AttributeValue>, TResult>>(
            readExpression,
            attrsParam);

        return lambda.Compile();
    }

    private Expression BuildAttributeReadExpression(
        ParameterExpression attrsParam,
        PropertyPath propertyPath,
        Type sourceType,
        Type targetType)
    {
        // For nested paths, build navigation logic
        if (propertyPath.IsNested)
        {
            return BuildNestedAttributeRead(attrsParam, propertyPath, sourceType, targetType);
        }

        // For single-level paths, read directly
        return BuildDirectAttributeRead(attrsParam, propertyPath, sourceType, targetType);
    }

    private Expression BuildDirectAttributeRead(
        ParameterExpression attrsParam,
        PropertyPath propertyPath,
        Type sourceType,
        Type targetType)
    {
        // Resolve the DynamoDB attribute name
        var resolver = _resolverFactory.GetResolver(sourceType);
        var attributeName = resolver.GetAttributeName(propertyPath.LeafName);

        // Get the converter for the target type
        var converter = _converterRegistry.GetConverter(targetType);

        // Build expression: converter.FromAttributeValue(attrs.TryGetValue(key, out var av) ? av : null)
        var tryGetValueMethod = typeof(Dictionary<string, AttributeValue>)
            .GetMethod(nameof(Dictionary<string, AttributeValue>.TryGetValue))!;

        var avVariable = Expression.Variable(typeof(AttributeValue), "av");
        var tryGetCall = Expression.Call(
            attrsParam,
            tryGetValueMethod,
            Expression.Constant(attributeName),
            avVariable);

        // If TryGetValue succeeds, use av; otherwise, use null
        var nullAttributeValue = Expression.Constant(null, typeof(AttributeValue));
        var conditionalAv = Expression.Condition(
            tryGetCall,
            avVariable,
            nullAttributeValue);

        // Call converter.FromAttributeValue(av)
        var fromAttributeMethod = converter.GetType()
            .GetMethod(nameof(IAttributeValueConverter<object>.FromAttributeValue))!;

        var convertCall = Expression.Call(
            Expression.Constant(converter),
            fromAttributeMethod,
            conditionalAv);

        // Wrap in a block with the av variable
        return Expression.Block(
            new[] { avVariable },
            convertCall);
    }

    private Expression BuildNestedAttributeRead(
        ParameterExpression attrsParam,
        PropertyPath propertyPath,
        Type sourceType,
        Type targetType)
    {
        // Build path array: ["Address", "City"]
        var pathSegments = propertyPath.Segments.ToArray();

        // Resolve attribute names for each segment
        var resolvedPath = new List<string>();
        var currentType = sourceType;

        for (int i = 0; i < pathSegments.Length; i++)
        {
            var resolver = _resolverFactory.GetResolver(currentType);
            var attributeName = resolver.GetAttributeName(pathSegments[i]);
            resolvedPath.Add(attributeName);

            if (i < pathSegments.Length - 1)
            {
                // Get the property type for the next segment
                var propInfo = propertyPath.SegmentProperties[i];
                currentType = propInfo.PropertyType;
            }
        }

        // Build expression: NavigateToLeaf(attrs, path)
        var navigateMethod = typeof(AttributeValueReader)
            .GetMethod(nameof(AttributeValueReader.NavigateToLeaf))!;

        var pathArray = Expression.Constant(resolvedPath.ToArray());
        var navigateCall = Expression.Call(navigateMethod, attrsParam, pathArray);

        // Store result in variable: var leafDict = NavigateToLeaf(...)
        var leafDictVar = Expression.Variable(
            typeof(Dictionary<string, AttributeValue>),
            "leafDict");

        var assignLeafDict = Expression.Assign(leafDictVar, navigateCall);

        // Check if navigation succeeded (leafDict != null)
        var nullDict = Expression.Constant(null, typeof(Dictionary<string, AttributeValue>));
        var isNull = Expression.Equal(leafDictVar, nullDict);

        // If navigation failed, return default value for target type
        var defaultValue = Expression.Default(targetType);

        // If navigation succeeded, read the leaf attribute
        var leafAttributeName = resolvedPath.Last();
        var converter = _converterRegistry.GetConverter(targetType);

        // Build TryGetValue call for leaf attribute
        var tryGetValueMethod = typeof(Dictionary<string, AttributeValue>)
            .GetMethod(nameof(Dictionary<string, AttributeValue>.TryGetValue))!;

        var avVariable = Expression.Variable(typeof(AttributeValue), "av");
        var tryGetCall = Expression.Call(
            leafDictVar,
            tryGetValueMethod,
            Expression.Constant(leafAttributeName),
            avVariable);

        var nullAttributeValue = Expression.Constant(null, typeof(AttributeValue));
        var conditionalAv = Expression.Condition(
            tryGetCall,
            avVariable,
            nullAttributeValue);

        // Call converter.FromAttributeValue(av)
        var fromAttributeMethod = converter.GetType()
            .GetMethod(nameof(IAttributeValueConverter<object>.FromAttributeValue))!;

        var convertCall = Expression.Call(
            Expression.Constant(converter),
            fromAttributeMethod,
            conditionalAv);

        // Conditional: leafDict == null ? default : convert
        var conditional = Expression.Condition(
            isNull,
            defaultValue,
            convertCall);

        // Wrap in block with variables
        return Expression.Block(
            new[] { leafDictVar, avVariable },
            assignLeafDict,
            conditional);
    }
}
