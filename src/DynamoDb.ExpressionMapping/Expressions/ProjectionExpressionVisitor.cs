using System.Linq.Expressions;
using System.Reflection;
using DynamoDb.ExpressionMapping.Exceptions;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Extracts property access paths from C# lambda expression trees.
/// Generic over TSource — not coupled to any specific entity type.
/// </summary>
public sealed class ProjectionExpressionVisitor : ExpressionVisitor
{
    private readonly List<PropertyPath> _paths = new();
    private readonly HashSet<string> _visitedPaths = new(StringComparer.Ordinal);
    private bool _isLeafContext;
    private ProjectionShape _shape = ProjectionShape.Identity;

    /// <summary>
    /// Extracts property paths from a lambda expression.
    /// </summary>
    /// <typeparam name="TSource">Source entity type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="expression">Lambda expression to analyze</param>
    /// <returns>List of extracted property paths (deduplicated)</returns>
    public static IReadOnlyList<PropertyPath> ExtractPropertyPaths<TSource, TResult>(
        Expression<Func<TSource, TResult>> expression)
    {
        var visitor = new ProjectionExpressionVisitor();
        visitor.Visit(expression);
        return visitor._paths;
    }

    /// <summary>
    /// Extracts property paths and returns the projection shape.
    /// </summary>
    /// <typeparam name="TSource">Source entity type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="expression">Lambda expression to analyze</param>
    /// <param name="shape">Detected projection shape</param>
    /// <returns>List of extracted property paths (deduplicated)</returns>
    public static IReadOnlyList<PropertyPath> ExtractPropertyPaths<TSource, TResult>(
        Expression<Func<TSource, TResult>> expression,
        out ProjectionShape shape)
    {
        var visitor = new ProjectionExpressionVisitor();
        visitor.Visit(expression);
        shape = visitor._shape;
        return visitor._paths;
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        // Lambda body determines the shape
        if (node.Body is ParameterExpression)
        {
            // p => p (identity)
            _shape = ProjectionShape.Identity;
            return node;
        }

        // Visit the body with leaf context
        _isLeafContext = true;
        Visit(node.Body);

        // Determine shape based on collected paths
        if (_paths.Count == 0)
        {
            _shape = ProjectionShape.Identity;
        }
        else if (_paths.Count == 1 && node.Body is MemberExpression)
        {
            _shape = ProjectionShape.SingleProperty;
        }
        else
        {
            _shape = ProjectionShape.Composite;
        }

        return node;
    }

    protected override Expression VisitNew(NewExpression node)
    {
        // Anonymous type or tuple construction
        // Each argument is a leaf
        var wasLeafContext = _isLeafContext;
        foreach (var arg in node.Arguments)
        {
            _isLeafContext = true;
            Visit(arg);
        }
        _isLeafContext = wasLeafContext;
        return node;
    }

    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        // Object initializer: new Dto { X = p.A, Y = p.B }
        // Each binding value is a leaf
        var wasLeafContext = _isLeafContext;
        foreach (var binding in node.Bindings)
        {
            if (binding is MemberAssignment assignment)
            {
                _isLeafContext = true;
                Visit(assignment.Expression);
            }
        }
        _isLeafContext = wasLeafContext;
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Only process if this is a leaf context
        if (!_isLeafContext)
        {
            return base.VisitMember(node);
        }

        // Extract the property chain
        var segments = new List<string>();
        var properties = new List<PropertyInfo>();
        var current = node;

        while (current != null)
        {
            if (current.Member is not PropertyInfo propInfo)
            {
                throw new UnsupportedExpressionException(
                    current.NodeType,
                    current.ToString());
            }

            segments.Insert(0, propInfo.Name);
            properties.Insert(0, propInfo);

            if (current.Expression is MemberExpression innerMember)
            {
                current = innerMember;
            }
            else if (current.Expression is ParameterExpression)
            {
                // Reached the parameter (root)
                break;
            }
            else
            {
                throw new UnsupportedExpressionException(
                    current.Expression?.NodeType ?? ExpressionType.Default,
                    current.Expression?.ToString() ?? "null");
            }
        }

        // Create the path
        var fullPath = string.Join(".", segments);
        if (_visitedPaths.Add(fullPath))
        {
            _paths.Add(new PropertyPath(segments, properties));
        }

        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Method calls (e.g. Enum.Parse<T>(p.Property), p.Property.ToString())
        // are treated as client-side transformations.
        // We recurse into the arguments (and instance) to extract any
        // property paths referenced, but the method itself is not translated
        // to a DynamoDB expression — it runs during result mapping.

        if (node.Object != null)
            Visit(node.Object);

        foreach (var arg in node.Arguments)
            Visit(arg);

        return node;
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        throw new UnsupportedExpressionException(node.NodeType, node.ToString());
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        throw new UnsupportedExpressionException(node.NodeType, node.ToString());
    }

    protected override Expression VisitIndex(IndexExpression node)
    {
        throw new UnsupportedExpressionException(node.NodeType, node.ToString());
    }
}
