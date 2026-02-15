using System.Collections.Concurrent;

namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Options for configuring the AttributeNameResolverFactory in DI scenarios.
/// Used by the Configure&lt;AttributeNameResolverFactoryOptions&gt; pattern
/// to register fluent-configured resolvers into the factory after it's built.
/// </summary>
public sealed class AttributeNameResolverFactoryOptions
{
    private readonly ConcurrentDictionary<Type, IAttributeNameResolver> resolvers = new();

    /// <summary>
    /// Adds a resolver for a specific entity type.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="resolver">The resolver for the entity type.</param>
    public void AddResolver(Type entityType, IAttributeNameResolver resolver)
    {
        resolvers[entityType] = resolver;
    }

    /// <summary>
    /// Gets all registered resolvers.
    /// </summary>
    internal IReadOnlyDictionary<Type, IAttributeNameResolver> GetResolvers()
    {
        return resolvers;
    }
}
