using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.ResultMapping;

/// <summary>
/// Rewrites a selector expression by replacing source parameter property accesses
/// with DynamoDB attribute dictionary reads. Preserves all other expression nodes
/// (method calls, constructors, member inits, casts, etc.) unchanged.
/// </summary>
internal sealed class SelectorRewritingVisitor : ExpressionVisitor
{
    private readonly ParameterExpression _sourceParam;
    private readonly ParameterExpression _attrsParam;
    private readonly Type _sourceType;
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;

    public SelectorRewritingVisitor(
        ParameterExpression sourceParam,
        ParameterExpression attrsParam,
        Type sourceType,
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converterRegistry)
    {
        _sourceParam = sourceParam;
        _attrsParam = attrsParam;
        _sourceType = sourceType;
        _resolverFactory = resolverFactory;
        _converterRegistry = converterRegistry;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (IsSourceParameterAccess(node))
        {
            var (segments, properties) = ExtractMemberChain(node);
            var propertyPath = new PropertyPath(segments, properties);
            var targetType = node.Type;

            if (propertyPath.IsNested)
            {
                return BuildNestedAttributeRead(propertyPath, targetType);
            }

            return BuildDirectAttributeRead(propertyPath, targetType);
        }

        return base.VisitMember(node);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node == _sourceParam)
        {
            throw new UnsupportedExpressionException(
                node.NodeType,
                "Direct use of source parameter not supported; use property access");
        }

        return base.VisitParameter(node);
    }

    private bool IsSourceParameterAccess(MemberExpression node)
    {
        Expression? current = node;
        while (current is MemberExpression member)
        {
            current = member.Expression;
        }

        return current == _sourceParam;
    }

    private static (List<string> segments, List<PropertyInfo> properties) ExtractMemberChain(
        MemberExpression node)
    {
        var segments = new List<string>();
        var properties = new List<PropertyInfo>();
        var current = (MemberExpression?)node;

        while (current != null)
        {
            if (current.Member is not PropertyInfo propInfo)
            {
                throw new UnsupportedExpressionException(
                    current.NodeType,
                    $"Member {current.Member.Name} must be a property");
            }

            segments.Insert(0, propInfo.Name);
            properties.Insert(0, propInfo);
            current = current.Expression as MemberExpression;
        }

        return (segments, properties);
    }

    private Expression BuildDirectAttributeRead(PropertyPath propertyPath, Type targetType)
    {
        var resolver = _resolverFactory.GetResolver(_sourceType);
        var attributeName = resolver.GetAttributeName(propertyPath.LeafName);

        var converter = _converterRegistry.GetConverter(targetType);

        var tryGetValueMethod = typeof(Dictionary<string, AttributeValue>)
            .GetMethod(nameof(Dictionary<string, AttributeValue>.TryGetValue))!;

        var avVariable = Expression.Variable(typeof(AttributeValue), $"av_{attributeName}");
        var tryGetCall = Expression.Call(
            _attrsParam,
            tryGetValueMethod,
            Expression.Constant(attributeName),
            avVariable);

        var nullAttributeValue = Expression.Constant(null, typeof(AttributeValue));
        var conditionalAv = Expression.Condition(
            tryGetCall,
            avVariable,
            nullAttributeValue);

        var fromAttributeMethod = converter.GetType()
            .GetMethod(nameof(IAttributeValueConverter<object>.FromAttributeValue))!;

        var convertCall = Expression.Call(
            Expression.Constant(converter),
            fromAttributeMethod,
            conditionalAv);

        return Expression.Block(
            new[] { avVariable },
            convertCall);
    }

    private Expression BuildNestedAttributeRead(PropertyPath propertyPath, Type targetType)
    {
        var pathSegments = propertyPath.Segments.ToArray();

        var resolvedPath = new List<string>();
        var currentType = _sourceType;

        for (int i = 0; i < pathSegments.Length; i++)
        {
            var resolver = _resolverFactory.GetResolver(currentType);
            var attributeName = resolver.GetAttributeName(pathSegments[i]);
            resolvedPath.Add(attributeName);

            if (i < pathSegments.Length - 1)
            {
                var propInfo = propertyPath.SegmentProperties[i];
                currentType = propInfo.PropertyType;
            }
        }

        var pathKey = string.Join("_", resolvedPath);

        var navigateMethod = typeof(AttributeValueReader)
            .GetMethod(nameof(AttributeValueReader.NavigateToLeaf))!;

        var pathArray = Expression.Constant(resolvedPath.ToArray());
        var navigateCall = Expression.Call(navigateMethod, _attrsParam, pathArray);

        var leafDictVar = Expression.Variable(
            typeof(Dictionary<string, AttributeValue>),
            $"leafDict_{pathKey}");

        var assignLeafDict = Expression.Assign(leafDictVar, navigateCall);

        var nullDict = Expression.Constant(null, typeof(Dictionary<string, AttributeValue>));
        var isNull = Expression.Equal(leafDictVar, nullDict);

        var defaultValue = Expression.Default(targetType);

        var leafAttributeName = resolvedPath.Last();
        var converter = _converterRegistry.GetConverter(targetType);

        var tryGetValueMethod = typeof(Dictionary<string, AttributeValue>)
            .GetMethod(nameof(Dictionary<string, AttributeValue>.TryGetValue))!;

        var avVariable = Expression.Variable(typeof(AttributeValue), $"av_{pathKey}");

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

        var fromAttributeMethod = converter.GetType()
            .GetMethod(nameof(IAttributeValueConverter<object>.FromAttributeValue))!;

        var convertCall = Expression.Call(
            Expression.Constant(converter),
            fromAttributeMethod,
            conditionalAv);

        var conditional = Expression.Condition(
            isNull,
            defaultValue,
            convertCall);

        return Expression.Block(
            new[] { leafDictVar, avVariable },
            assignLeafDict,
            conditional);
    }
}
