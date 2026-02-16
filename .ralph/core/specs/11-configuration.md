# Spec 11: Configuration and Dependency Injection

## Motivation

The library needs a centralised configuration point for global settings (strict/lenient mode, null handling, custom converters) and should integrate with standard .NET dependency injection while also supporting manual instantiation for environments without a DI container.

## Design

### 1. Configuration Object

```csharp
namespace DynamoDb.ExpressionMapping;

/// <summary>
/// Global configuration for the expression mapping library.
/// Immutable after construction (builder pattern).
/// </summary>
public sealed class DynamoDbExpressionConfig
{
    /// <summary>
    /// How to handle [DynamoDbIgnore] properties in expressions.
    /// Default: Strict (throw).
    /// </summary>
    public NameResolutionMode NameResolutionMode { get; }

    /// <summary>
    /// How to handle null values in write operations.
    /// Default: OmitNull.
    /// </summary>
    public NullHandlingMode NullHandlingMode { get; }

    /// <summary>
    /// The type converter registry. Default: built-in converters.
    /// </summary>
    public IAttributeValueConverterRegistry ConverterRegistry { get; }

    /// <summary>
    /// The reserved keyword registry. Default: official AWS list.
    /// </summary>
    public ReservedKeywordRegistry ReservedKeywords { get; }

    /// <summary>
    /// The expression cache. Default: shared singleton cache.
    /// </summary>
    public IExpressionCache Cache { get; }

    /// <summary>
    /// The logger factory for diagnostic output.
    /// Default: NullLoggerFactory.Instance (no-op).
    /// </summary>
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Default configuration with sensible defaults.
    /// </summary>
    public static readonly DynamoDbExpressionConfig Default = new Builder().Build();
}
```

### 2. Builder

```csharp
public sealed class Builder
{
    private NameResolutionMode nameResolutionMode = NameResolutionMode.Strict;
    private NullHandlingMode nullHandlingMode = NullHandlingMode.OmitNull;
    private IAttributeValueConverterRegistry converterRegistry;
    private ReservedKeywordRegistry reservedKeywords;
    private IExpressionCache cache;
    private ILoggerFactory loggerFactory;

    public Builder WithNameResolutionMode(NameResolutionMode mode)
    {
        this.nameResolutionMode = mode;
        return this;
    }

    public Builder WithNullHandling(NullHandlingMode mode)
    {
        this.nullHandlingMode = mode;
        return this;
    }

    public Builder WithConverter<T>(IAttributeValueConverter<T> converter)
    {
        EnsureConverterRegistryCloned();
        this.converterRegistry.Register(converter);
        return this;
    }

    private void EnsureConverterRegistryCloned()
    {
        this.converterRegistry ??= AttributeValueConverterRegistry.Default.Clone();
    }

    public Builder WithCache(IExpressionCache cache)
    {
        this.cache = cache;
        return this;
    }

    public Builder WithLoggerFactory(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
        return this;
    }

    public DynamoDbExpressionConfig Build()
    {
        return new DynamoDbExpressionConfig(
            nameResolutionMode,
            nullHandlingMode,
            converterRegistry ?? AttributeValueConverterRegistry.Default.Clone(),
            reservedKeywords ?? ReservedKeywordRegistry.Default,
            cache ?? (IExpressionCache)ExpressionCache.Default,
            loggerFactory ?? NullLoggerFactory.Instance);
    }
}
```

### 3. DI Registration (IServiceCollection)

```csharp
namespace DynamoDb.ExpressionMapping;

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

        // Resolver factory — singleton, auto-discovers resolvers for any type via reflection.
        // Expression builders depend on this for cross-type nested path resolution.
        services.AddSingleton<IAttributeNameResolverFactory>(sp =>
            new AttributeNameResolverFactory(config.NameResolutionMode));

        // Open generic registrations — container creates one instance per TSource.
        // All expression builders are singleton-safe: they hold only immutable dependencies
        // and create mutable state locally per method call (or per fluent chain via clone-on-use
        // for UpdateExpressionBuilder — see ADR-001).
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
        Action<AttributeNameResolverBuilder<TEntity>> configure = null)
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
```

### 4. Usage Without DI

For consumers not using `IServiceCollection` (tests, console apps, custom containers):

```csharp
// Quick start — all defaults.
// Factory auto-discovers resolvers for any type via reflection.
var factory = new AttributeNameResolverFactory();
var projectionBuilder = new ProjectionBuilder<Order>(factory);
var resultMapper = new DirectResultMapper<Order>(factory.GetResolver<Order>());

// Custom configuration
var config = new DynamoDbExpressionConfig.Builder()
    .WithNullHandling(NullHandlingMode.ExplicitNull)
    .WithConverter(new MoneyConverter())
    .Build();

var factory = new AttributeNameResolverFactory(config.NameResolutionMode);
var projectionBuilder = new ProjectionBuilder<Order>(factory, config.ReservedKeywords, config.Cache);
var resultMapper = new DirectResultMapper<Order>(config.ConverterRegistry, factory.GetResolver<Order>());

// Fluent overrides for specific types (nested or root) without modifying the entity class.
// Types without explicit configuration are auto-discovered.
var factory = new AttributeNameResolverFactoryBuilder()
    .WithMode(NameResolutionMode.Strict)
    .Configure<Order>(b => b.Map(e => e.CustomerId, "cust_id"))
    .Configure<Address>(b => b.Map(a => a.City, "city_name"))
    .Build();

var projectionBuilder = new ProjectionBuilder<Order>(factory);
// p => p.Address.City → "Address.city_name"
```

### 5. Per-Entity Configuration

Different entities may need different resolvers. This includes nested types used in navigation properties — `AddDynamoDbEntity` registers the resolver into both the DI container and the `IAttributeNameResolverFactory`, so nested path resolution picks it up automatically.

```csharp
services.AddDynamoDbExpressionMapping();

// Entity with computed properties to ignore
services.AddDynamoDbEntity<ProductProjection>(entity =>
{
    entity.Ignore(p => p.IsAvailable);
    entity.Ignore(p => p.ComputedStatus);
});

// Entity with custom attribute names
services.AddDynamoDbEntity<OrderProjection>(entity =>
{
    entity.Map(p => p.CustomerId, "cust_id");
    entity.Map(p => p.CreatedAt, "created_at");
});

// Nested type — only needed when the nested type deviates from convention.
// Types with attribute annotations or convention naming are auto-discovered
// by the factory and do not need explicit registration.
services.AddDynamoDbEntity<Address>(entity =>
{
    entity.Map(a => a.City, "city_name");
    entity.Map(a => a.PostCode, "postal_code");
});
```

### 6. Logging

The library uses `Microsoft.Extensions.Logging.Abstractions` (`ILogger`) rather than any specific logging framework, to remain framework-agnostic. Consumers bridge to their preferred logging implementation via standard .NET logging configuration.

Log levels:
- `Debug` — Cache hits/misses, expression analysis details
- `Warning` — Attribute missing from dictionary during mapping, type conversion fallback
- `Error` — Expression analysis failures, converter exceptions

Logging is optional. If no `ILoggerFactory` is provided via `WithLoggerFactory()`, defaults to `NullLoggerFactory.Instance` (no-op). In DI scenarios, the container-registered `ILoggerFactory` is used automatically. Components obtain typed loggers internally via `loggerFactory.CreateLogger<T>()`.
