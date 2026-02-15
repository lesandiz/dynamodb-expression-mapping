namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Specifies how null values are handled during write operations.
/// </summary>
public enum NullHandlingMode
{
    /// <summary>
    /// Omit null-valued attributes from expression attribute values.
    /// This is the recommended default for DynamoDB operations.
    /// </summary>
    OmitNull,

    /// <summary>
    /// Explicitly write null-valued attributes as DynamoDB NULL types.
    /// Use with caution: DynamoDB NULL is not the same as attribute absence.
    /// </summary>
    ExplicitNull
}
