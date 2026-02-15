using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.ResultMapping;

/// <summary>
/// Handles composite projections:
/// - Anonymous types: p => new { p.A, p.B }
/// - Named types: p => new Dto { X = p.A, Y = p.B }
/// - Records: p => new Record(p.A, p.B)
/// </summary>
internal sealed class CompositeMappingStrategy : IMappingStrategy
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;

    public CompositeMappingStrategy(
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converterRegistry)
    {
        _resolverFactory = resolverFactory;
        _converterRegistry = converterRegistry;
    }

    public Func<Dictionary<string, AttributeValue>, TResult> BuildMapper<TSource, TResult>(
        Expression<Func<TSource, TResult>> selector)
    {
        var body = selector.Body;

        // Handle different expression types
        return body switch
        {
            NewExpression newExpr => BuildNewExpressionMapper<TSource, TResult>(selector, newExpr),
            MemberInitExpression initExpr => BuildMemberInitMapper<TSource, TResult>(selector, initExpr),
            _ => throw new UnsupportedExpressionException(body.NodeType, selector.ToString())
        };
    }

    private Func<Dictionary<string, AttributeValue>, TResult> BuildNewExpressionMapper<TSource, TResult>(
        Expression<Func<TSource, TResult>> selector,
        NewExpression newExpr)
    {
        // Parameter: Dictionary<string, AttributeValue> attrs
        var attrsParam = Expression.Parameter(typeof(Dictionary<string, AttributeValue>), "attrs");

        // Build converter expressions for each constructor argument
        var arguments = new List<Expression>();
        var variables = new List<ParameterExpression>();
        var statements = new List<Expression>();

        for (int i = 0; i < newExpr.Arguments.Count; i++)
        {
            var arg = newExpr.Arguments[i];

            // Extract property path from the argument expression
            var propertyPath = ExtractPropertyPath(arg);

            // Determine the target type from the argument expression's type
            // For records and parameterized constructors, Members might be null
            Type targetType;
            if (newExpr.Members != null && i < newExpr.Members.Count)
            {
                var member = newExpr.Members[i] as PropertyInfo;
                targetType = member?.PropertyType ?? arg.Type;
            }
            else
            {
                // Fall back to the argument's type (for records, this is correct)
                targetType = arg.Type;
            }

            // Build the attribute read expression
            var readExpr = BuildAttributeReadExpression(
                attrsParam,
                propertyPath,
                typeof(TSource),
                targetType,
                variables,
                statements);

            arguments.Add(readExpr);
        }

        // Build the constructor call with converted arguments
        var newCall = Expression.New(newExpr.Constructor!, arguments);

        // If we have variables/statements from nested reads, wrap in block
        Expression body;
        if (variables.Any())
        {
            statements.Add(newCall);
            body = Expression.Block(variables, statements);
        }
        else
        {
            body = newCall;
        }

        // Compile to delegate
        var lambda = Expression.Lambda<Func<Dictionary<string, AttributeValue>, TResult>>(
            body,
            attrsParam);

        return lambda.Compile();
    }

    private Func<Dictionary<string, AttributeValue>, TResult> BuildMemberInitMapper<TSource, TResult>(
        Expression<Func<TSource, TResult>> selector,
        MemberInitExpression initExpr)
    {
        // Parameter: Dictionary<string, AttributeValue> attrs
        var attrsParam = Expression.Parameter(typeof(Dictionary<string, AttributeValue>), "attrs");

        var variables = new List<ParameterExpression>();
        var statements = new List<Expression>();

        // Build bindings for each member
        var bindings = new List<MemberBinding>();

        foreach (var binding in initExpr.Bindings)
        {
            if (binding is not MemberAssignment assignment)
            {
                throw new InvalidOperationException(
                    $"Only member assignments are supported, got {binding.BindingType}");
            }

            // Extract property path from the assignment expression
            var propertyPath = ExtractPropertyPath(assignment.Expression);

            var property = assignment.Member as PropertyInfo;
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Member {assignment.Member.Name} must be a property");
            }

            // Build the attribute read expression
            var readExpr = BuildAttributeReadExpression(
                attrsParam,
                propertyPath,
                typeof(TSource),
                property.PropertyType,
                variables,
                statements);

            bindings.Add(Expression.Bind(property, readExpr));
        }

        // Build the member init expression
        var newExpr = Expression.New(initExpr.NewExpression.Constructor!);
        var memberInit = Expression.MemberInit(newExpr, bindings);

        // If we have variables/statements from nested reads, wrap in block
        Expression body;
        if (variables.Any())
        {
            statements.Add(memberInit);
            body = Expression.Block(variables, statements);
        }
        else
        {
            body = memberInit;
        }

        // Compile to delegate
        var lambda = Expression.Lambda<Func<Dictionary<string, AttributeValue>, TResult>>(
            body,
            attrsParam);

        return lambda.Compile();
    }

    private PropertyPath ExtractPropertyPath(Expression expression)
    {
        // Build a temporary lambda to extract the path
        var param = Expression.Parameter(expression.Type, "p");

        // Handle different expression types
        if (expression is MemberExpression memberExpr)
        {
            // Build the path by walking up the member chain
            var segments = new List<string>();
            var properties = new List<PropertyInfo>();
            var current = memberExpr;

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

            return new PropertyPath(segments, properties);
        }

        throw new UnsupportedExpressionException(
            expression.NodeType,
            "Expected property access expression");
    }

    private Expression BuildAttributeReadExpression(
        ParameterExpression attrsParam,
        PropertyPath propertyPath,
        Type sourceType,
        Type targetType,
        List<ParameterExpression> variables,
        List<Expression> statements)
    {
        // For nested paths, build navigation logic
        if (propertyPath.IsNested)
        {
            return BuildNestedAttributeRead(
                attrsParam,
                propertyPath,
                sourceType,
                targetType,
                variables,
                statements);
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

        var avVariable = Expression.Variable(typeof(AttributeValue), $"av_{attributeName}");
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

        // For composite mappings, we inline the variable usage
        // Store the result in a temp variable
        var resultVar = Expression.Variable(targetType, $"result_{attributeName}");
        var assign = Expression.Assign(resultVar, convertCall);

        // Track variables for the block
        return Expression.Block(
            new[] { avVariable, resultVar },
            assign,
            resultVar);
    }

    private Expression BuildNestedAttributeRead(
        ParameterExpression attrsParam,
        PropertyPath propertyPath,
        Type sourceType,
        Type targetType,
        List<ParameterExpression> variables,
        List<Expression> statements)
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
                var propInfo = propertyPath.SegmentProperties[i];
                currentType = propInfo.PropertyType;
            }
        }

        // Create a unique variable name for this nested path
        var pathKey = string.Join("_", resolvedPath);

        // Build expression: NavigateToLeaf(attrs, path)
        var navigateMethod = typeof(AttributeValueReader)
            .GetMethod(nameof(AttributeValueReader.NavigateToLeaf))!;

        var pathArray = Expression.Constant(resolvedPath.ToArray());
        var navigateCall = Expression.Call(navigateMethod, attrsParam, pathArray);

        // Store result in variable
        var leafDictVar = Expression.Variable(
            typeof(Dictionary<string, AttributeValue>),
            $"leafDict_{pathKey}");

        variables.Add(leafDictVar);
        statements.Add(Expression.Assign(leafDictVar, navigateCall));

        // Check if navigation succeeded (leafDict != null)
        var nullDict = Expression.Constant(null, typeof(Dictionary<string, AttributeValue>));
        var isNull = Expression.Equal(leafDictVar, nullDict);

        // If navigation failed, return default value
        var defaultValue = Expression.Default(targetType);

        // If navigation succeeded, read the leaf attribute
        var leafAttributeName = resolvedPath.Last();
        var converter = _converterRegistry.GetConverter(targetType);

        // Build TryGetValue call
        var tryGetValueMethod = typeof(Dictionary<string, AttributeValue>)
            .GetMethod(nameof(Dictionary<string, AttributeValue>.TryGetValue))!;

        var avVariable = Expression.Variable(typeof(AttributeValue), $"av_{pathKey}");
        variables.Add(avVariable);

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
        return Expression.Condition(
            isNull,
            defaultValue,
            convertCall);
    }
}
