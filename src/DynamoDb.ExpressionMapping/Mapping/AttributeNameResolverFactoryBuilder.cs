namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Fluent API for building an <see cref="IAttributeNameResolverFactory"/> with per-type
/// resolver configurations. Enables configuration of types that cannot be annotated
/// (third-party types, shared models) while using auto-discovery for types that follow convention.
/// </summary>
public sealed class AttributeNameResolverFactoryBuilder
{
    private readonly Dictionary<Type, IAttributeNameResolver> _configuredResolvers = new();
    private NameResolutionMode _mode = NameResolutionMode.Strict;

    /// <summary>
    /// Sets the default resolution mode for handling ignored properties.
    /// This mode applies to all resolvers (both configured and auto-discovered).
    /// </summary>
    /// <param name="mode">The resolution mode.</param>
    /// <returns>The builder for method chaining.</returns>
    public AttributeNameResolverFactoryBuilder WithMode(NameResolutionMode mode)
    {
        _mode = mode;
        return this;
    }

    /// <summary>
    /// Configures a specific type using a fluent builder.
    /// For configured types, the resolver is built using the builder and registered with the factory.
    /// For non-configured types, the factory auto-discovers via reflection.
    /// </summary>
    /// <typeparam name="T">The entity type to configure.</typeparam>
    /// <param name="configure">Action that configures the type's attribute name mappings.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if configure is null.</exception>
    public AttributeNameResolverFactoryBuilder Configure<T>(
        Action<AttributeNameResolverBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new AttributeNameResolverBuilder<T>().WithMode(_mode);
        configure(builder);

        var resolver = builder.Build();
        _configuredResolvers[typeof(T)] = resolver;

        return this;
    }

    /// <summary>
    /// Builds the configured <see cref="IAttributeNameResolverFactory"/>.
    /// Configured types use the explicitly built resolvers.
    /// Non-configured types are auto-discovered via reflection.
    /// </summary>
    /// <returns>A configured <see cref="IAttributeNameResolverFactory"/> instance.</returns>
    public IAttributeNameResolverFactory Build()
    {
        var factory = new AttributeNameResolverFactory(_mode);

        // Register all explicitly configured resolvers
        foreach (var (type, resolver) in _configuredResolvers)
        {
            factory.Register(type, resolver);
        }

        return factory;
    }
}
