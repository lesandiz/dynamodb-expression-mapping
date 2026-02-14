namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Default resolver: property name = attribute name, all properties are considered stored.
/// </summary>
internal sealed class PassThroughAttributeNameResolver : IAttributeNameResolver
{
    public static readonly PassThroughAttributeNameResolver Instance = new();

    public string GetAttributeName(string propertyName) => propertyName;
    public bool IsStoredAttribute(string propertyName) => true;
    public string GetPropertyName(string attributeName) => attributeName;
}
