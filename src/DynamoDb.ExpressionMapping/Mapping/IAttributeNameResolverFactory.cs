namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Factory for creating and caching attribute name resolvers for different entity types.
/// Used for cross-type resolution in nested property paths (e.g., p.Address.City resolves
/// "Address" against Order type, "City" against Address type).
/// </summary>
public interface IAttributeNameResolverFactory
{
    /// <summary>
    /// Gets or creates a resolver for the specified entity type.
    /// </summary>
    /// <param name="entityType">The entity type to resolve attribute names for.</param>
    /// <returns>An attribute name resolver for the specified type.</returns>
    IAttributeNameResolver GetResolver(Type entityType);

    /// <summary>
    /// Gets or creates a typed resolver for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to resolve attribute names for.</typeparam>
    /// <returns>A typed attribute name resolver for T.</returns>
    IAttributeNameResolver<T> GetResolver<T>();
}
