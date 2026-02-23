using System.Diagnostics.CodeAnalysis;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DynamoDb.ExpressionMapping;

/// <summary>
/// Global configuration for the expression mapping library.
/// Immutable after construction (builder pattern).
/// </summary>
[ExcludeFromCodeCoverage]
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

    private DynamoDbExpressionConfig(
        NameResolutionMode nameResolutionMode,
        NullHandlingMode nullHandlingMode,
        IAttributeValueConverterRegistry converterRegistry,
        ReservedKeywordRegistry reservedKeywords,
        IExpressionCache cache,
        ILoggerFactory loggerFactory)
    {
        NameResolutionMode = nameResolutionMode;
        NullHandlingMode = nullHandlingMode;
        ConverterRegistry = converterRegistry;
        ReservedKeywords = reservedKeywords;
        Cache = cache;
        LoggerFactory = loggerFactory;
    }

    /// <summary>
    /// Builder for constructing DynamoDbExpressionConfig instances.
    /// </summary>
    public sealed class Builder
    {
        private NameResolutionMode nameResolutionMode = NameResolutionMode.Strict;
        private NullHandlingMode nullHandlingMode = NullHandlingMode.OmitNull;
        private IAttributeValueConverterRegistry? converterRegistry;
        private ReservedKeywordRegistry? reservedKeywords;
        private IExpressionCache? cache;
        private ILoggerFactory? loggerFactory;

        /// <summary>
        /// Sets the name resolution mode.
        /// </summary>
        public Builder WithNameResolutionMode(NameResolutionMode mode)
        {
            this.nameResolutionMode = mode;
            return this;
        }

        /// <summary>
        /// Sets the null handling mode.
        /// </summary>
        public Builder WithNullHandling(NullHandlingMode mode)
        {
            this.nullHandlingMode = mode;
            return this;
        }

        /// <summary>
        /// Registers a custom converter. Clones the default registry on first call.
        /// </summary>
        public Builder WithConverter<T>(IAttributeValueConverter<T> converter)
        {
            EnsureConverterRegistryCloned();
            this.converterRegistry!.Register(converter);
            return this;
        }

        /// <summary>
        /// Sets a custom converter registry.
        /// </summary>
        public Builder WithConverterRegistry(IAttributeValueConverterRegistry registry)
        {
            this.converterRegistry = registry;
            return this;
        }

        /// <summary>
        /// Sets a custom reserved keyword registry.
        /// </summary>
        public Builder WithReservedKeywords(ReservedKeywordRegistry keywords)
        {
            this.reservedKeywords = keywords;
            return this;
        }

        /// <summary>
        /// Sets a custom expression cache.
        /// </summary>
        public Builder WithCache(IExpressionCache cache)
        {
            this.cache = cache;
            return this;
        }

        /// <summary>
        /// Sets a custom logger factory.
        /// </summary>
        public Builder WithLoggerFactory(ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            return this;
        }

        /// <summary>
        /// Builds the configuration.
        /// </summary>
        public DynamoDbExpressionConfig Build()
        {
            return new DynamoDbExpressionConfig(
                nameResolutionMode,
                nullHandlingMode,
                converterRegistry ?? AttributeValueConverterRegistry.Default.Clone(),
                reservedKeywords ?? ReservedKeywordRegistry.Default,
                cache ?? ExpressionCache.Default,
                loggerFactory ?? NullLoggerFactory.Instance);
        }

        private void EnsureConverterRegistryCloned()
        {
            this.converterRegistry ??= AttributeValueConverterRegistry.Default.Clone();
        }
    }
}
