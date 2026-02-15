using DynamoDb.ExpressionMapping.Attributes;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using DynamoDb.ExpressionMapping.ResultMapping;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDynamoDbExpressionMapping_RegistersOpenGenericBuilders()
    {
        var services = new ServiceCollection();
        services.AddDynamoDbExpressionMapping();

        var provider = services.BuildServiceProvider();

        // All open generic builders should be resolvable for any entity type
        provider.GetService<IProjectionBuilder<TestEntity>>().Should().NotBeNull();
        provider.GetService<IFilterExpressionBuilder<TestEntity>>().Should().NotBeNull();
        provider.GetService<IConditionExpressionBuilder<TestEntity>>().Should().NotBeNull();
        provider.GetService<IUpdateExpressionBuilder<TestEntity>>().Should().NotBeNull();
        provider.GetService<IKeyConditionExpressionBuilder<TestEntity>>().Should().NotBeNull();
        provider.GetService<IDirectResultMapper<TestEntity>>().Should().NotBeNull();
        provider.GetService<IAttributeNameResolver<TestEntity>>().Should().NotBeNull();
    }

    [Fact]
    public void AddDynamoDbExpressionMapping_RegistersResolverFactory()
    {
        var services = new ServiceCollection();
        services.AddDynamoDbExpressionMapping();

        var provider = services.BuildServiceProvider();

        var factory = provider.GetService<IAttributeNameResolverFactory>();
        factory.Should().NotBeNull();
    }

    [Fact]
    public void AddDynamoDbExpressionMapping_WithConfigure_AppliesSettings()
    {
        var services = new ServiceCollection();
        var customCache = NullExpressionCache.Instance;

        services.AddDynamoDbExpressionMapping(builder =>
        {
            builder.WithNameResolutionMode(NameResolutionMode.Lenient);
            builder.WithNullHandling(NullHandlingMode.ExplicitNull);
            builder.WithCache(customCache);
        });

        var provider = services.BuildServiceProvider();

        var config = provider.GetService<DynamoDbExpressionConfig>();
        config.Should().NotBeNull();
        config!.NameResolutionMode.Should().Be(NameResolutionMode.Lenient);
        config.NullHandlingMode.Should().Be(NullHandlingMode.ExplicitNull);
        config.Cache.Should().BeSameAs(customCache);
    }

    [Fact]
    public void AddDynamoDbEntity_OverridesOpenGenericResolver()
    {
        var services = new ServiceCollection();
        services.AddDynamoDbExpressionMapping();

        services.AddDynamoDbEntity<TestEntityWithCustomMapping>(entity =>
        {
            entity.Map(e => e.Name, "custom_name");
        });

        var provider = services.BuildServiceProvider();

        var resolver = provider.GetService<IAttributeNameResolver<TestEntityWithCustomMapping>>();
        resolver.Should().NotBeNull();
        resolver!.GetAttributeName(nameof(TestEntityWithCustomMapping.Name)).Should().Be("custom_name");
    }

    [Fact]
    public void AddDynamoDbEntity_RegistersIntoFactory_ForNestedResolution()
    {
        var services = new ServiceCollection();
        services.AddDynamoDbExpressionMapping();

        services.AddDynamoDbEntity<TestEntityWithCustomMapping>(entity =>
        {
            entity.Map(e => e.Name, "custom_name");
        });

        var provider = services.BuildServiceProvider();

        var factory = provider.GetService<IAttributeNameResolverFactory>();
        var resolver = factory!.GetResolver<TestEntityWithCustomMapping>();

        resolver.GetAttributeName(nameof(TestEntityWithCustomMapping.Name)).Should().Be("custom_name");
    }

    [Fact]
    public void AddDynamoDbEntity_MultipleEntities_EachConfiguredIndependently()
    {
        var services = new ServiceCollection();
        services.AddDynamoDbExpressionMapping();

        services.AddDynamoDbEntity<TestEntity>(entity =>
        {
            entity.Map(e => e.Name, "entity1_name");
        });

        services.AddDynamoDbEntity<TestEntityWithCustomMapping>(entity =>
        {
            entity.Map(e => e.Name, "entity2_name");
        });

        var provider = services.BuildServiceProvider();

        var resolver1 = provider.GetService<IAttributeNameResolver<TestEntity>>();
        var resolver2 = provider.GetService<IAttributeNameResolver<TestEntityWithCustomMapping>>();

        resolver1!.GetAttributeName(nameof(TestEntity.Name)).Should().Be("entity1_name");
        resolver2!.GetAttributeName(nameof(TestEntityWithCustomMapping.Name)).Should().Be("entity2_name");
    }

    [Fact]
    public void OpenGenericResolver_FallsBackToReflection()
    {
        var services = new ServiceCollection();
        services.AddDynamoDbExpressionMapping();

        var provider = services.BuildServiceProvider();

        // This entity is not explicitly configured, so it should use the default resolver
        var resolver = provider.GetService<IAttributeNameResolver<TestEntityWithAttributes>>();
        resolver.Should().NotBeNull();
        resolver!.GetAttributeName(nameof(TestEntityWithAttributes.CustomId)).Should().Be("custom_id");
    }

    [Fact]
    public void ManualInstantiation_WorksWithoutDI()
    {
        var factory = new AttributeNameResolverFactory();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var projectionBuilder = new ProjectionBuilder<TestEntity>(factory);
        var resultMapper = new DirectResultMapper<TestEntity>(factory, converterRegistry);

        projectionBuilder.Should().NotBeNull();
        resultMapper.Should().NotBeNull();
    }

    [Fact]
    public void ManualInstantiation_WithFluentFactoryBuilder()
    {
        var factory = new AttributeNameResolverFactoryBuilder()
            .WithMode(NameResolutionMode.Strict)
            .Configure<TestEntity>(b => b.Map(e => e.Name, "custom_name"))
            .Build();

        var resolver = factory.GetResolver<TestEntity>();
        resolver.GetAttributeName(nameof(TestEntity.Name)).Should().Be("custom_name");
    }

    [Fact]
    public void AddDynamoDbExpressionMapping_RegistersSingletonComponents()
    {
        var services = new ServiceCollection();
        services.AddDynamoDbExpressionMapping();

        var provider = services.BuildServiceProvider();

        // Verify core components are registered
        provider.GetService<DynamoDbExpressionConfig>().Should().NotBeNull();
        provider.GetService<IAttributeValueConverterRegistry>().Should().NotBeNull();
        provider.GetService<ReservedKeywordRegistry>().Should().NotBeNull();
        provider.GetService<IExpressionCache>().Should().NotBeNull();
    }

    // Test entities
    private class TestEntity
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    private class TestEntityWithCustomMapping
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    private class TestEntityWithAttributes
    {
        [DynamoDbAttribute("custom_id")]
        public string CustomId { get; set; } = "";

        public string Name { get; set; } = "";
    }
}
