using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ResultMapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.ResultMapping;

/// <summary>
/// Tests for CompositeMappingStrategy, SinglePropertyMappingStrategy, and IdentityMappingStrategy.
/// Migrated from P3MutationKillingTests as part of test suite refactoring (Phase 3c).
/// </summary>
public class MappingStrategyTests
{
    #region CompositeMappingStrategy — path traversal off-by-one

    [Fact]
    public void CompositeMappingStrategy_NestedPath_TwoLevels_ResolvesCorrectly()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var mapper = new DirectResultMapper<TestEntity>(resolverFactory, converterRegistry);

        var attrs = new Dictionary<string, AttributeValue>
        {
            ["Address"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["City"] = new() { S = "Paris" },
                    ["Street"] = new() { S = "Champs" }
                }
            }
        };

        var result = mapper.Map(attrs, e => new { e.Address!.City, e.Address!.Street });

        result.City.Should().Be("Paris");
        result.Street.Should().Be("Champs");
    }

    [Fact]
    public void CompositeMappingStrategy_ThreeLevelNestedPath_ResolvesCorrectly()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var mapper = new DirectResultMapper<TestEntity>(resolverFactory, converterRegistry);

        var attrs = new Dictionary<string, AttributeValue>
        {
            ["Address"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["Country"] = new()
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["Code"] = new() { S = "FR" }
                        }
                    }
                }
            }
        };

        var result = mapper.Map(attrs, e => new { CountryCode = e.Address!.Country!.Code });

        result.CountryCode.Should().Be("FR");
    }

    [Fact]
    public void CompositeMappingStrategy_UnsupportedExpression_Throws()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var strategy = new CompositeMappingStrategy(resolverFactory, converterRegistry);

        Action act = () => strategy.BuildMapper<TestEntity, string>(e => e.OrderId.ToUpper());

        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void CompositeMappingStrategy_NonPropertyMember_Throws()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var strategy = new CompositeMappingStrategy(resolverFactory, converterRegistry);

        Action act = () => strategy.BuildMapper<EntityWithField, string?>(
            e => e.FieldValue);

        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void CompositeMappingStrategy_MemberInit_NonPropertyBinding_Throws()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var mapper = new DirectResultMapper<TestEntity>(resolverFactory, converterRegistry);

        var attrs = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "init-test" }
        };

        var result = mapper.Map(attrs, e => new OrderSummary { Id = e.OrderId });

        result.Id.Should().Be("init-test");
    }

    #endregion

    #region SinglePropertyMappingStrategy — logical mutation

    [Fact]
    public void SinglePropertyMappingStrategy_ShapeNotSingleProperty_Throws()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var strategy = new SinglePropertyMappingStrategy(resolverFactory, converterRegistry);

        Action act = () => strategy.BuildMapper<TestEntity, object>(
            e => new { e.OrderId, e.Price });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*exactly one*");
    }

    [Fact]
    public void SinglePropertyMappingStrategy_NestedProperty_MapsCorrectly()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var strategy = new SinglePropertyMappingStrategy(resolverFactory, converterRegistry);

        var attrs = new Dictionary<string, AttributeValue>
        {
            ["Address"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["City"] = new() { S = "Berlin" }
                }
            }
        };

        var mapper = strategy.BuildMapper<TestEntity, string>(e => e.Address!.City);
        var result = mapper(attrs);

        result.Should().Be("Berlin");
    }

    [Fact]
    public void SinglePropertyMappingStrategy_NestedProperty_MissingIntermediate_ReturnsDefault()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var strategy = new SinglePropertyMappingStrategy(resolverFactory, converterRegistry);

        var attrs = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "123" }
        };

        var mapper = strategy.BuildMapper<TestEntity, string>(e => e.Address!.City);
        var result = mapper(attrs);

        result.Should().BeNull();
    }

    [Fact]
    public void SinglePropertyMappingStrategy_DirectProperty_MapsCorrectly()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var strategy = new SinglePropertyMappingStrategy(resolverFactory, converterRegistry);

        var attrs = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "direct-123" }
        };

        var mapper = strategy.BuildMapper<TestEntity, string>(e => e.OrderId);
        var result = mapper(attrs);

        result.Should().Be("direct-123");
    }

    [Fact]
    public void SinglePropertyMappingStrategy_MissingAttribute_ReturnsDefault()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var strategy = new SinglePropertyMappingStrategy(resolverFactory, converterRegistry);

        var attrs = new Dictionary<string, AttributeValue>();

        var mapper = strategy.BuildMapper<TestEntity, string>(e => e.OrderId);
        var result = mapper(attrs);

        result.Should().BeNull();
    }

    #endregion

    #region IdentityMappingStrategy — edge cases

    [Fact]
    public void IdentityMappingStrategy_WithMapper_DelegatesToIt()
    {
        var called = false;
        var entityMapper = new Func<Dictionary<string, AttributeValue>, object>(attrs =>
        {
            called = true;
            return new TestEntity { OrderId = "identity" };
        });

        var strategy = new IdentityMappingStrategy(entityMapper);
        var mapper = strategy.BuildMapper<TestEntity, TestEntity>(e => e);

        var result = mapper(new Dictionary<string, AttributeValue>());

        called.Should().BeTrue();
        result.OrderId.Should().Be("identity");
    }

    [Fact]
    public void IdentityMappingStrategy_WithoutMapper_ThrowsUnsupportedExpressionException()
    {
        var strategy = new IdentityMappingStrategy(null);

        Action act = () => strategy.BuildMapper<TestEntity, TestEntity>(e => e);

        act.Should().Throw<UnsupportedExpressionException>();
    }

    #endregion

    #region Test Helper Types

    private class EntityWithField
    {
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        public string? FieldValue;
#pragma warning restore CS0649
    }

    #endregion
}
