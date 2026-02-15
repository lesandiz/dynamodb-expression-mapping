using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Converts boolean predicates into DynamoDB filter expression syntax.
/// Traverses expression trees and builds FilterExpression/ConditionExpression strings
/// with attribute name aliases and attribute value placeholders.
/// </summary>
internal sealed class FilterExpressionVisitor : ExpressionVisitor
{
    private readonly IAttributeNameResolverFactory resolverFactory;
    private readonly ExpressionValueEmitter valueEmitter;
    private readonly AliasGenerator aliasGen;
    private readonly StringBuilder result;
    private readonly Dictionary<string, string> names;
    private readonly Dictionary<string, AttributeValue> values;
    private readonly ReservedKeywordRegistry keywordRegistry;

    /// <summary>
    /// Creates a new filter expression visitor.
    /// </summary>
    /// <param name="resolverFactory">Factory for resolving attribute names across types.</param>
    /// <param name="valueEmitter">Emitter for converting .NET values to AttributeValue.</param>
    /// <param name="aliasGen">Scoped alias generator (e.g., "filt" or "cond").</param>
    /// <param name="result">StringBuilder for building the expression string.</param>
    /// <param name="names">Dictionary for storing attribute name aliases.</param>
    /// <param name="values">Dictionary for storing attribute value placeholders.</param>
    public FilterExpressionVisitor(
        IAttributeNameResolverFactory resolverFactory,
        ExpressionValueEmitter valueEmitter,
        AliasGenerator aliasGen,
        StringBuilder result,
        Dictionary<string, string> names,
        Dictionary<string, AttributeValue> values)
    {
        this.resolverFactory = resolverFactory ?? throw new ArgumentNullException(nameof(resolverFactory));
        this.valueEmitter = valueEmitter ?? throw new ArgumentNullException(nameof(valueEmitter));
        this.aliasGen = aliasGen ?? throw new ArgumentNullException(nameof(aliasGen));
        this.result = result ?? throw new ArgumentNullException(nameof(result));
        this.names = names ?? throw new ArgumentNullException(nameof(names));
        this.values = values ?? throw new ArgumentNullException(nameof(values));
        this.keywordRegistry = ReservedKeywordRegistry.Default;
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        // Logical operators: AND, OR
        if (node.NodeType == ExpressionType.AndAlso)
        {
            result.Append('(');
            Visit(node.Left);
            result.Append(") AND (");
            Visit(node.Right);
            result.Append(')');
            return node;
        }

        if (node.NodeType == ExpressionType.OrElse)
        {
            result.Append('(');
            Visit(node.Left);
            result.Append(") OR (");
            Visit(node.Right);
            result.Append(')');
            return node;
        }

        // Comparison operators: ==, !=, >, >=, <, <=
        if (IsComparisonOperator(node.NodeType))
        {
            VisitComparison(node);
            return node;
        }

        throw new UnsupportedExpressionException(node.NodeType, node.ToString());
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        // Logical NOT
        if (node.NodeType == ExpressionType.Not)
        {
            // Check for boolean property: !p.Enabled → "#alias = :value" (value = false)
            if (node.Operand is MemberExpression memberExpr && memberExpr.Type == typeof(bool))
            {
                var propertyPath = BuildPropertyPath(memberExpr);
                var attributeName = ResolveAttributeName(propertyPath);
                var valueAlias = aliasGen.NextValue();

                values[valueAlias] = new AttributeValue { BOOL = false };
                result.Append($"{attributeName} = {valueAlias}");
                return node;
            }

            // NOT (sub-expression)
            result.Append("NOT (");
            Visit(node.Operand);
            result.Append(')');
            return node;
        }

        if (node.NodeType == ExpressionType.Convert || node.NodeType == ExpressionType.ConvertChecked)
        {
            // Transparent conversion (e.g., int to long)
            return Visit(node.Operand);
        }

        throw new UnsupportedExpressionException(node.NodeType, node.ToString());
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Boolean property: p => p.Enabled → "#alias = :value" (value = true)
        if (node.Type == typeof(bool))
        {
            var propertyPath = BuildPropertyPath(node);
            var attributeName = ResolveAttributeName(propertyPath);
            var valueAlias = aliasGen.NextValue();

            values[valueAlias] = new AttributeValue { BOOL = true };
            result.Append($"{attributeName} = {valueAlias}");
            return node;
        }

        throw new UnsupportedExpressionException(node.NodeType, node.ToString());
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // String.StartsWith → begins_with()
        if (node.Method.DeclaringType == typeof(string) && node.Method.Name == nameof(string.StartsWith))
        {
            if (node.Arguments.Count == 1 && node.Object != null)
            {
                var propertyPath = BuildPropertyPath(node.Object);
                var attributeName = ResolveAttributeName(propertyPath);
                var value = EvaluateExpression(node.Arguments[0]);
                var valueAlias = aliasGen.NextValue();

                values[valueAlias] = valueEmitter.Emit(value, propertyPath.PropertyInfo);
                result.Append($"begins_with({attributeName}, {valueAlias})");
                return node;
            }
        }

        // String.Contains → contains()
        if (node.Method.DeclaringType == typeof(string) && node.Method.Name == nameof(string.Contains))
        {
            if (node.Arguments.Count == 1 && node.Object != null)
            {
                var propertyPath = BuildPropertyPath(node.Object);
                var attributeName = ResolveAttributeName(propertyPath);
                var value = EvaluateExpression(node.Arguments[0]);
                var valueAlias = aliasGen.NextValue();

                values[valueAlias] = valueEmitter.Emit(value, propertyPath.PropertyInfo);
                result.Append($"contains({attributeName}, {valueAlias})");
                return node;
            }
        }

        // Collection.Contains → IN operator (e.g., statuses.Contains(p.Status))
        if (node.Method.Name == nameof(Enumerable.Contains))
        {
            // Static method: Enumerable.Contains(collection, item)
            if (node.Method.DeclaringType == typeof(Enumerable) && node.Arguments.Count == 2)
            {
                var collectionExpr = node.Arguments[0];
                var itemExpr = node.Arguments[1];

                // Item must be a property access
                if (itemExpr is MemberExpression memberExpr)
                {
                    var propertyPath = BuildPropertyPath(memberExpr);
                    var attributeName = ResolveAttributeName(propertyPath);
                    var collection = EvaluateExpression(collectionExpr);

                    VisitInOperator(attributeName, collection, propertyPath.PropertyInfo);
                    return node;
                }
            }
            // Instance method: collection.Contains(item)
            else if (node.Object != null && node.Arguments.Count == 1)
            {
                var itemExpr = node.Arguments[0];

                // Item must be a property access
                if (itemExpr is MemberExpression memberExpr)
                {
                    var propertyPath = BuildPropertyPath(memberExpr);
                    var attributeName = ResolveAttributeName(propertyPath);
                    var collection = EvaluateExpression(node.Object);

                    VisitInOperator(attributeName, collection, propertyPath.PropertyInfo);
                    return node;
                }
            }
        }

        // DynamoDbFunctions.AttributeExists → attribute_exists()
        if (node.Method.DeclaringType == typeof(DynamoDbFunctions))
        {
            if (node.Method.Name == nameof(DynamoDbFunctions.AttributeExists))
            {
                var propertyPath = BuildPropertyPath(node.Arguments[0]);
                var attributeName = ResolveAttributeName(propertyPath);
                result.Append($"attribute_exists({attributeName})");
                return node;
            }

            // DynamoDbFunctions.AttributeNotExists → attribute_not_exists()
            if (node.Method.Name == nameof(DynamoDbFunctions.AttributeNotExists))
            {
                var propertyPath = BuildPropertyPath(node.Arguments[0]);
                var attributeName = ResolveAttributeName(propertyPath);
                result.Append($"attribute_not_exists({attributeName})");
                return node;
            }

            // DynamoDbFunctions.Between → BETWEEN
            if (node.Method.Name == nameof(DynamoDbFunctions.Between))
            {
                var propertyPath = BuildPropertyPath(node.Arguments[0]);
                var attributeName = ResolveAttributeName(propertyPath);
                var low = EvaluateExpression(node.Arguments[1]);
                var high = EvaluateExpression(node.Arguments[2]);

                var lowAlias = aliasGen.NextValue();
                var highAlias = aliasGen.NextValue();

                values[lowAlias] = valueEmitter.Emit(low, propertyPath.PropertyInfo);
                values[highAlias] = valueEmitter.Emit(high, propertyPath.PropertyInfo);

                result.Append($"{attributeName} BETWEEN {lowAlias} AND {highAlias}");
                return node;
            }

            // DynamoDbFunctions.Size → size()
            if (node.Method.Name == nameof(DynamoDbFunctions.Size))
            {
                var propertyPath = BuildPropertyPath(node.Arguments[0]);
                var attributeName = ResolveAttributeName(propertyPath);
                result.Append($"size({attributeName})");
                return node;
            }

            // DynamoDbFunctions.AttributeType → attribute_type()
            if (node.Method.Name == nameof(DynamoDbFunctions.AttributeType))
            {
                var propertyPath = BuildPropertyPath(node.Arguments[0]);
                var attributeName = ResolveAttributeName(propertyPath);
                var dynamoDbType = EvaluateExpression(node.Arguments[1]);
                var valueAlias = aliasGen.NextValue();

                values[valueAlias] = new AttributeValue { S = dynamoDbType.ToString() };
                result.Append($"attribute_type({attributeName}, {valueAlias})");
                return node;
            }
        }

        throw new UnsupportedExpressionException(node.NodeType, node.ToString());
    }

    private void VisitComparison(BinaryExpression node)
    {
        // Handle null checks: p.Prop == null / p.Prop != null
        if (IsNullConstant(node.Right))
        {
            var propertyPath = BuildPropertyPath(node.Left);
            var attributeName = ResolveAttributeName(propertyPath);

            if (node.NodeType == ExpressionType.Equal)
            {
                result.Append($"attribute_not_exists({attributeName})");
            }
            else if (node.NodeType == ExpressionType.NotEqual)
            {
                result.Append($"attribute_exists({attributeName})");
            }
            return;
        }

        if (IsNullConstant(node.Left))
        {
            var propertyPath = BuildPropertyPath(node.Right);
            var attributeName = ResolveAttributeName(propertyPath);

            if (node.NodeType == ExpressionType.Equal)
            {
                result.Append($"attribute_not_exists({attributeName})");
            }
            else if (node.NodeType == ExpressionType.NotEqual)
            {
                result.Append($"attribute_exists({attributeName})");
            }
            return;
        }

        // Handle DynamoDbFunctions.Size() in comparisons (e.g., Size(p.Tags) > 0)
        if (node.Left is MethodCallExpression leftMethodCall &&
            leftMethodCall.Method.DeclaringType == typeof(DynamoDbFunctions) &&
            leftMethodCall.Method.Name == nameof(DynamoDbFunctions.Size))
        {
            var propertyPath = BuildPropertyPath(leftMethodCall.Arguments[0]);
            var attributeName = ResolveAttributeName(propertyPath);
            var value = EvaluateExpression(node.Right);
            var valueAlias = aliasGen.NextValue();

            // Size() returns int, so property info is not needed for literal comparison
            values[valueAlias] = valueEmitter.Emit(value, null);

            var op = GetOperator(node.NodeType);
            result.Append($"size({attributeName}) {op} {valueAlias}");
            return;
        }

        // Standard comparison: property OP value
        // Unwrap Convert nodes (common with enum comparisons)
        var left = node.Left;
        var right = node.Right;

        while (left is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unaryLeft)
        {
            left = unaryLeft.Operand;
        }

        while (right is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unaryRight)
        {
            right = unaryRight.Operand;
        }

        if (left is MemberExpression memberExpr)
        {
            var propertyPath = BuildPropertyPath(memberExpr);
            var attributeName = ResolveAttributeName(propertyPath);
            var value = EvaluateExpression(right);
            var valueAlias = aliasGen.NextValue();

            values[valueAlias] = valueEmitter.Emit(value, propertyPath.PropertyInfo);

            var op = GetOperator(node.NodeType);
            result.Append($"{attributeName} {op} {valueAlias}");
            return;
        }

        throw new UnsupportedExpressionException(node.NodeType, node.ToString());
    }

    private void VisitInOperator(string attributeName, object collection, PropertyInfo propertyInfo)
    {
        if (collection is not System.Collections.IEnumerable enumerable)
        {
            throw new InvalidFilterException("IN operator requires a collection");
        }

        var valueAliases = new List<string>();
        foreach (var item in enumerable)
        {
            var valueAlias = aliasGen.NextValue();
            values[valueAlias] = valueEmitter.Emit(item, propertyInfo);
            valueAliases.Add(valueAlias);
        }

        if (valueAliases.Count == 0)
        {
            throw new InvalidFilterException("IN operator requires a non-empty collection");
        }

        result.Append($"{attributeName} IN ({string.Join(", ", valueAliases)})");
    }

    /// <summary>
    /// Builds a PropertyPath from a member expression (e.g., p.Address.City).
    /// </summary>
    private PropertyPath BuildPropertyPath(Expression expr)
    {
        var segments = new List<string>();
        var properties = new List<PropertyInfo>();

        var current = expr;
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
            throw new UnsupportedExpressionException(expr.NodeType, expr.ToString());
        }

        return new PropertyPath(segments, properties);
    }

    /// <summary>
    /// Resolves a PropertyPath to a DynamoDB attribute name (with aliasing if reserved).
    /// Uses cross-type resolution for nested paths.
    /// </summary>
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
                throw new InvalidFilterException(
                    $"Property '{propertyPath.FullPath}' is marked with [DynamoDbIgnore] and cannot be used in filter expressions",
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

    /// <summary>
    /// Evaluates an expression to extract its constant value (handles closures).
    /// </summary>
    private object EvaluateExpression(Expression expr)
    {
        // Constant expression
        if (expr is ConstantExpression constExpr)
        {
            return constExpr.Value!;
        }

        // Member access on closure (e.g., captured variable)
        if (expr is MemberExpression memberExpr)
        {
            var instance = EvaluateExpression(memberExpr.Expression!);

            if (memberExpr.Member is FieldInfo fieldInfo)
            {
                return fieldInfo.GetValue(instance)!;
            }

            if (memberExpr.Member is PropertyInfo propInfo)
            {
                return propInfo.GetValue(instance)!;
            }
        }

        // Compile and invoke the expression
        var lambda = Expression.Lambda<Func<object>>(Expression.Convert(expr, typeof(object)));
        var compiled = lambda.Compile();
        return compiled();
    }

    private static bool IsComparisonOperator(ExpressionType nodeType)
    {
        return nodeType is ExpressionType.Equal
            or ExpressionType.NotEqual
            or ExpressionType.GreaterThan
            or ExpressionType.GreaterThanOrEqual
            or ExpressionType.LessThan
            or ExpressionType.LessThanOrEqual;
    }

    private static bool IsNullConstant(Expression expr)
    {
        return expr is ConstantExpression { Value: null };
    }

    private static string GetOperator(ExpressionType nodeType)
    {
        return nodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            _ => throw new UnsupportedExpressionException(nodeType, nodeType.ToString())
        };
    }
}
