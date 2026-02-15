using System.Collections.Concurrent;

namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Default factory that auto-creates resolvers via reflection.
/// For types with [DynamoDbAttribute], [DynamoDBProperty], or [DynamoDbIgnore]
/// annotations, resolvers are created automatically — no explicit registration needed.
/// Fluent overrides can be registered for types that require custom mappings.
/// </summary>
public sealed class AttributeNameResolverFactory : IAttributeNameResolverFactory
{
    private readonly ConcurrentDictionary<Type, IAttributeNameResolver> cache = new();
    private readonly NameResolutionMode mode;

    /// <summary>
    /// Creates a new attribute name resolver factory.
    /// </summary>
    /// <param name="mode">The resolution mode for handling ignored properties. Defaults to Strict.</param>
    public AttributeNameResolverFactory(NameResolutionMode mode = NameResolutionMode.Strict)
    {
        this.mode = mode;
    }

    /// <summary>
    /// Creates a new attribute name resolver factory with pre-registered resolvers.
    /// Used by the DI container to seed the factory with fluent-configured resolvers.
    /// </summary>
    /// <param name="mode">The resolution mode for handling ignored properties.</param>
    /// <param name="preRegisteredResolvers">Resolvers to pre-register before auto-discovery.</param>
    internal AttributeNameResolverFactory(
        NameResolutionMode mode,
        IReadOnlyDictionary<Type, IAttributeNameResolver> preRegisteredResolvers)
    {
        this.mode = mode;
        foreach (var kvp in preRegisteredResolvers)
        {
            cache[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Gets or creates a resolver for the specified entity type.
    /// </summary>
    /// <param name="entityType">The entity type to resolve attribute names for.</param>
    /// <returns>An attribute name resolver for the specified type.</returns>
    public IAttributeNameResolver GetResolver(Type entityType)
    {
        return cache.GetOrAdd(entityType, type =>
        {
            // Creates AttributeNameResolver<T> via reflection for the given type.
            // The resolver scans the type's properties for attribute annotations
            // using the same resolution order defined in Section 5 of Spec 01.
            var resolverType = typeof(AttributeNameResolver<>).MakeGenericType(type);
            return (IAttributeNameResolver)Activator.CreateInstance(resolverType, mode)!;
        });
    }

    /// <summary>
    /// Gets or creates a typed resolver for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to resolve attribute names for.</typeparam>
    /// <returns>A typed attribute name resolver for T.</returns>
    public IAttributeNameResolver<T> GetResolver<T>()
    {
        return (IAttributeNameResolver<T>)GetResolver(typeof(T));
    }

    /// <summary>
    /// Registers an explicit resolver for a type, overriding auto-discovery.
    /// Used for fluent-configured resolvers or test doubles.
    /// </summary>
    /// <param name="entityType">The entity type to register the resolver for.</param>
    /// <param name="resolver">The resolver to register.</param>
    internal void Register(Type entityType, IAttributeNameResolver resolver)
    {
        cache[entityType] = resolver;
    }
}
