using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Mapping.Converters;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests;

public class DynamoDbExpressionConfigTests
{
    [Fact]
    public void DefaultConfig_HasExpectedDefaults()
    {
        var config = DynamoDbExpressionConfig.Default;

        config.NameResolutionMode.Should().Be(NameResolutionMode.Strict);
        config.NullHandlingMode.Should().Be(NullHandlingMode.OmitNull);
        config.ConverterRegistry.Should().NotBeNull();
        config.ReservedKeywords.Should().BeSameAs(ReservedKeywordRegistry.Default);
        config.Cache.Should().BeSameAs(ExpressionCache.Default);
        config.LoggerFactory.Should().BeSameAs(NullLoggerFactory.Instance);
    }

    [Fact]
    public void DefaultConfig_NameResolutionMode_IsStrict()
    {
        DynamoDbExpressionConfig.Default.NameResolutionMode.Should().Be(NameResolutionMode.Strict);
    }

    [Fact]
    public void DefaultConfig_NullHandlingMode_IsOmitNull()
    {
        DynamoDbExpressionConfig.Default.NullHandlingMode.Should().Be(NullHandlingMode.OmitNull);
    }

    [Fact]
    public void DefaultConfig_LoggerFactory_IsNullLoggerFactory()
    {
        DynamoDbExpressionConfig.Default.LoggerFactory.Should().BeSameAs(NullLoggerFactory.Instance);
    }

    [Fact]
    public void Builder_OverridesIndividualSettings()
    {
        var customCache = NullExpressionCache.Instance;
        var customKeywords = ReservedKeywordRegistry.Default;
        var customLoggerFactory = NullLoggerFactory.Instance;

        var config = new DynamoDbExpressionConfig.Builder()
            .WithNameResolutionMode(NameResolutionMode.Lenient)
            .WithNullHandling(NullHandlingMode.ExplicitNull)
            .WithCache(customCache)
            .WithReservedKeywords(customKeywords)
            .WithLoggerFactory(customLoggerFactory)
            .Build();

        config.NameResolutionMode.Should().Be(NameResolutionMode.Lenient);
        config.NullHandlingMode.Should().Be(NullHandlingMode.ExplicitNull);
        config.Cache.Should().BeSameAs(customCache);
        config.ReservedKeywords.Should().BeSameAs(customKeywords);
        config.LoggerFactory.Should().BeSameAs(customLoggerFactory);
    }

    [Fact]
    public void Builder_WithNameResolutionMode_AppliesMode()
    {
        var config = new DynamoDbExpressionConfig.Builder()
            .WithNameResolutionMode(NameResolutionMode.Lenient)
            .Build();

        config.NameResolutionMode.Should().Be(NameResolutionMode.Lenient);
    }

    [Fact]
    public void Builder_WithNullHandling_AppliesMode()
    {
        var config = new DynamoDbExpressionConfig.Builder()
            .WithNullHandling(NullHandlingMode.ExplicitNull)
            .Build();

        config.NullHandlingMode.Should().Be(NullHandlingMode.ExplicitNull);
    }

    [Fact]
    public void Builder_WithCache_AppliesCustomCache()
    {
        var customCache = NullExpressionCache.Instance;

        var config = new DynamoDbExpressionConfig.Builder()
            .WithCache(customCache)
            .Build();

        config.Cache.Should().BeSameAs(customCache);
    }

    [Fact]
    public void Builder_WithLoggerFactory_AppliesFactory()
    {
        var customLoggerFactory = NullLoggerFactory.Instance;

        var config = new DynamoDbExpressionConfig.Builder()
            .WithLoggerFactory(customLoggerFactory)
            .Build();

        config.LoggerFactory.Should().BeSameAs(customLoggerFactory);
    }

    [Fact]
    public void Builder_WithConverter_ClonesDefaultRegistryBeforeMutating()
    {
        // Get original default registry size
        var originalDefaultRegistry = AttributeValueConverterRegistry.Default;

        // Build a config with a custom converter
        var customConverter = new StringConverter();
        var config = new DynamoDbExpressionConfig.Builder()
            .WithConverter(customConverter)
            .Build();

        // The config should have its own registry
        config.ConverterRegistry.Should().NotBeSameAs(originalDefaultRegistry);
    }

    [Fact]
    public void Builder_WithConverter_DoesNotMutateDefaultRegistry()
    {
        // Build a config with a custom converter
        var customConverter = new StringConverter();
        var config = new DynamoDbExpressionConfig.Builder()
            .WithConverter(customConverter)
            .Build();

        // Should not affect the default registry
        AttributeValueConverterRegistry.Default.Should().NotBeSameAs(config.ConverterRegistry);

        // Default registry should still be frozen
        var act = () => AttributeValueConverterRegistry.Default.Register(new StringConverter());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Builder_WithConverterRegistry_AppliesCustomRegistry()
    {
        var customRegistry = AttributeValueConverterRegistry.Default.Clone();

        var config = new DynamoDbExpressionConfig.Builder()
            .WithConverterRegistry(customRegistry)
            .Build();

        config.ConverterRegistry.Should().BeSameAs(customRegistry);
    }

    [Fact]
    public void Builder_WithReservedKeywords_AppliesCustomKeywords()
    {
        var customKeywords = ReservedKeywordRegistry.Default;

        var config = new DynamoDbExpressionConfig.Builder()
            .WithReservedKeywords(customKeywords)
            .Build();

        config.ReservedKeywords.Should().BeSameAs(customKeywords);
    }
}
