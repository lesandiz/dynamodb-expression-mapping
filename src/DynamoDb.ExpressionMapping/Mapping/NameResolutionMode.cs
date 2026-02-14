namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Specifies how the attribute name resolver handles properties marked with [DynamoDbIgnore].
/// </summary>
public enum NameResolutionMode
{
    /// <summary>
    /// Throws when projecting a [DynamoDbIgnore] property. Default.
    /// </summary>
    Strict,

    /// <summary>
    /// Silently excludes [DynamoDbIgnore] properties from projections.
    /// The result mapper will populate them with default values.
    /// </summary>
    Lenient
}
