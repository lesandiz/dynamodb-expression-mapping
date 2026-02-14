namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Describes the shape of a projection expression result.
/// </summary>
public enum ProjectionShape
{
    /// <summary>
    /// Identity projection: p => p (whole object).
    /// No projection needed, full entity fetch.
    /// </summary>
    Identity,

    /// <summary>
    /// Single property projection: p => p.SingleProp (single value).
    /// Read one attribute, convert directly.
    /// </summary>
    SingleProperty,

    /// <summary>
    /// Composite projection: p => new { p.A, p.B } or p => new Dto { X = p.A }.
    /// Read multiple attributes, construct result object.
    /// </summary>
    Composite
}
