namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Specifies how enum values are stored in DynamoDB.
/// </summary>
public enum EnumStorageMode
{
    /// <summary>
    /// Stored as string name (default). Uses DynamoDB String (S) attribute.
    /// </summary>
    String,

    /// <summary>
    /// Stored as numeric value. Uses DynamoDB Number (N) attribute.
    /// </summary>
    Number
}
