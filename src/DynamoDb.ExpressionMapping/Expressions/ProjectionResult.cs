namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// The result of building a DynamoDB projection expression.
/// Immutable after construction.
/// </summary>
public sealed class ProjectionResult
{
    /// <summary>
    /// The DynamoDB ProjectionExpression string.
    /// E.g. "OrderId, #proj_0, Address.City"
    /// Empty string means "select all attributes" (no projection).
    /// </summary>
    public string ProjectionExpression { get; }

    /// <summary>
    /// ExpressionAttributeNames mapping for reserved keywords.
    /// E.g. {"#proj_0": "Status", "#proj_1": "Name"}
    /// </summary>
    public IReadOnlyDictionary<string, string> ExpressionAttributeNames { get; }

    /// <summary>
    /// The property paths extracted from the expression, in order.
    /// </summary>
    public IReadOnlyList<PropertyPath> PropertyPaths { get; }

    /// <summary>
    /// The shape of the projection (Identity, SingleProperty, Composite).
    /// </summary>
    public ProjectionShape Shape { get; }

    /// <summary>
    /// Whether this projection is empty (selects all attributes).
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(ProjectionExpression);

    /// <summary>
    /// The DynamoDB attribute names that will be fetched (resolved names, not C# names).
    /// Useful for validation and logging.
    /// </summary>
    public IReadOnlyList<string> ResolvedAttributeNames { get; }

    public ProjectionResult(
        string projectionExpression,
        IReadOnlyDictionary<string, string> expressionAttributeNames,
        IReadOnlyList<PropertyPath> propertyPaths,
        ProjectionShape shape,
        IReadOnlyList<string> resolvedAttributeNames)
    {
        ProjectionExpression = projectionExpression ?? string.Empty;
        ExpressionAttributeNames = expressionAttributeNames ?? new Dictionary<string, string>();
        PropertyPaths = propertyPaths ?? Array.Empty<PropertyPath>();
        Shape = shape;
        ResolvedAttributeNames = resolvedAttributeNames ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates an empty projection result (selects all attributes).
    /// </summary>
    public static ProjectionResult Empty { get; } = new ProjectionResult(
        string.Empty,
        new Dictionary<string, string>(),
        Array.Empty<PropertyPath>(),
        ProjectionShape.Identity,
        Array.Empty<string>());
}
