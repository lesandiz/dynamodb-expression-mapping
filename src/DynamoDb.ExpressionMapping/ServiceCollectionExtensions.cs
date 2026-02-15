using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ResultMapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DynamoDb.ExpressionMapping;

/// <summary>
/// Extension methods for registering DynamoDb.ExpressionMapping services with IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers DynamoDb.ExpressionMapping services with default configuration.
    /// </summary>
    public static IServiceCollection AddDynamoDbExpressionMapping(
        this IServiceCollection services)
    {
        return services.AddDynamoDbExpressionMapping(_ => { });
    }

    /// <summary>
    /// Registers DynamoDb.ExpressionMapping services with custom configuration.
    /// Builders are registered as open generics — one instance per entity type,
    /// resolved automatically by the DI container.
    /// </summary>
    public static IServiceCollection AddDynamoDbExpressionMapping(
        this IServiceCollection services,
        Action<DynamoDbExpressionConfig.Builder> configure)
    {
        var builder = new DynamoDbExpressionConfig.Builder();
        configure(builder);
        var config = builder.Build();

        services.AddSingleton(config);
        services.AddSingleton(config.ConverterRegistry);
        services.AddSingleton(config.ReservedKeywords);
        services.AddSingleton(config.Cache);
        services.AddSingleton(config.LoggerFactory);

        // Options for collecting fluent-configured resolvers
        services.AddOptions<AttributeNameResolverFactoryOptions>();

        // Resolver factory — singleton, auto-discovers resolvers for any type via reflection.
        // Expression builders depend on this for cross-type nested path resolution.
        // The factory is built from the options after all AddDynamoDbEntity calls complete.
        services.AddSingleton<IAttributeNameResolverFactory>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AttributeNameResolverFactoryOptions>>().Value;
            return new AttributeNameResolverFactory(config.NameResolutionMode, options.GetResolvers());
        });

        // Open generic registrations — container creates one instance per TSource.
        // Individual resolvers are still available for direct injection if needed.
        services.AddSingleton(typeof(IAttributeNameResolver<>), typeof(AttributeNameResolver<>));
        services.AddSingleton(typeof(IProjectionBuilder<>), typeof(ProjectionBuilder<>));
        services.AddSingleton(typeof(IFilterExpressionBuilder<>), typeof(FilterExpressionBuilder<>));
        services.AddSingleton(typeof(IConditionExpressionBuilder<>), typeof(ConditionExpressionBuilder<>));
        services.AddSingleton(typeof(IUpdateExpressionBuilder<>), typeof(UpdateExpressionBuilder<>));
        services.AddSingleton(typeof(IKeyConditionExpressionBuilder<>), typeof(KeyConditionExpressionBuilder<>));
        services.AddSingleton(typeof(IDirectResultMapper<>), typeof(DirectResultMapper<>));

        return services;
    }

    /// <summary>
    /// Registers a custom IAttributeNameResolver for a specific entity type.
    /// The resolver is registered both as IAttributeNameResolver&lt;TEntity&gt; (for direct injection)
    /// and into the IAttributeNameResolverFactory (for nested path resolution).
    /// Call once per entity type that needs fluent overrides beyond what attribute annotations provide.
    /// </summary>
    public static IServiceCollection AddDynamoDbEntity<TEntity>(
        this IServiceCollection services,
        Action<AttributeNameResolverBuilder<TEntity>>? configure = null)
    {
        var resolverBuilder = new AttributeNameResolverBuilder<TEntity>();
        configure?.Invoke(resolverBuilder);
        var resolver = resolverBuilder.Build();

        // Explicit registration overrides the open generic IAttributeNameResolver<>
        services.AddSingleton<IAttributeNameResolver<TEntity>>(resolver);

        // Also register into the factory so nested path resolution picks it up.
        // This is done via a post-configuration step: after the factory is built,
        // fluent-configured resolvers are registered into it.
        services.Configure<AttributeNameResolverFactoryOptions>(opts =>
            opts.AddResolver(typeof(TEntity), resolver));

        return services;
    }
}
