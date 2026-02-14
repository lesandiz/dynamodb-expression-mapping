using System.Linq.Expressions;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

/// <summary>
/// Unit tests for ProjectionBuilder (Spec 03).
/// </summary>
public class ProjectionBuilderTests
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly ProjectionBuilder<TestEntity> _builder;

    public ProjectionBuilderTests()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _builder = new ProjectionBuilder<TestEntity>(
            _resolverFactory,
            ReservedKeywordRegistry.Default,
            NullExpressionCache.Instance);
    }

    #region Basic Projections

    [Fact]
    public void BuildProjection_Identity_ReturnsEmpty()
    {
        // Act
        var result = _builder.BuildProjection(p => p);

        // Assert
        result.Should().NotBeNull();
        result.ProjectionExpression.Should().BeEmpty();
        result.Shape.Should().Be(ProjectionShape.Identity);
        result.IsEmpty.Should().BeTrue();
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.PropertyPaths.Should().BeEmpty();
        result.ResolvedAttributeNames.Should().BeEmpty();
    }

    [Fact]
    public void BuildProjection_SingleProperty_ReturnsCorrectExpression()
    {
        // Act
        var result = _builder.BuildProjection(p => p.OrderId);

        // Assert
        result.ProjectionExpression.Should().Be("OrderId");
        result.Shape.Should().Be(ProjectionShape.SingleProperty);
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.PropertyPaths.Should().HaveCount(1);
        result.ResolvedAttributeNames.Should().Equal("OrderId");
    }

    [Fact]
    public void BuildProjection_MultipleProperties_ReturnsCommaSeparated()
    {
        // Act
        var result = _builder.BuildProjection(p => new { p.OrderId, p.CustomerId });

        // Assert
        result.ProjectionExpression.Should().Be("OrderId, CustomerId");
        result.Shape.Should().Be(ProjectionShape.Composite);
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.PropertyPaths.Should().HaveCount(2);
        result.ResolvedAttributeNames.Should().Equal("OrderId", "CustomerId");
    }

    #endregion

    #region Reserved Keywords

    [Fact]
    public void BuildProjection_ReservedKeyword_CreatesAlias()
    {
        // Act - "Name" and "Status" are reserved words
        var result = _builder.BuildProjection(p => new { p.OrderId, p.Name });

        // Assert
        result.ProjectionExpression.Should().Be("OrderId, #proj_0");
        result.ExpressionAttributeNames.Should().ContainKey("#proj_0")
            .WhoseValue.Should().Be("Name");
        result.ResolvedAttributeNames.Should().Equal("OrderId", "Name");
    }

    [Fact]
    public void BuildProjection_MultipleReservedKeywords_CreatesMultipleAliases()
    {
        // Act
        var result = _builder.BuildProjection(p => new { p.Name, p.Status });

        // Assert
        result.ProjectionExpression.Should().Be("#proj_0, #proj_1");
        result.ExpressionAttributeNames.Should().HaveCount(2);
        result.ExpressionAttributeNames["#proj_0"].Should().Be("Name");
        result.ExpressionAttributeNames["#proj_1"].Should().Be("Status");
    }

    #endregion

    #region Nested Attributes

    [Fact]
    public void BuildProjection_NestedProperty_UsesDotNotation()
    {
        // Act
        var result = _builder.BuildProjection(p => p.Address!.City);

        // Assert
        result.ProjectionExpression.Should().Be("Address.City");
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.PropertyPaths.Should().HaveCount(1);
        result.PropertyPaths[0].Segments.Should().Equal("Address", "City");
    }

    [Fact]
    public void BuildProjection_MultipleNestedProperties_ReturnsCorrectPaths()
    {
        // Act
        var result = _builder.BuildProjection(p => new { p.Address!.City, p.Address.ZipCode });

        // Assert
        result.ProjectionExpression.Should().Be("Address.City, Address.ZipCode");
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.PropertyPaths.Should().HaveCount(2);
    }

    [Fact]
    public void BuildProjection_DeeplyNestedProperty_UsesMultipleDots()
    {
        // Act
        var result = _builder.BuildProjection(p => p.Address!.Country!.Name);

        // Assert
        result.ProjectionExpression.Should().Be("Address.Country.#proj_0");
        result.ExpressionAttributeNames.Should().ContainKey("#proj_0")
            .WhoseValue.Should().Be("Name");
        result.PropertyPaths.Should().HaveCount(1);
        result.PropertyPaths[0].Segments.Should().Equal("Address", "Country", "Name");
    }

    [Fact]
    public void BuildProjection_NestedWithReservedKeyword_AliasesCorrectSegment()
    {
        // Act - "Name" is reserved
        var result = _builder.BuildProjection(p => p.Address!.Country!.Name);

        // Assert
        result.ProjectionExpression.Should().Be("Address.Country.#proj_0");
        result.ExpressionAttributeNames.Should().ContainKey("#proj_0")
            .WhoseValue.Should().Be("Name");
    }

    #endregion

    #region Named Type Projections

    [Fact]
    public void BuildProjection_NamedTypeWithPropertyInitializer_ExtractsAllProperties()
    {
        // Act
        var result = _builder.BuildProjection(p => new OrderSummary
        {
            Id = p.OrderId,
            Name = p.Name,
            Price = p.Price
        });

        // Assert
        result.ProjectionExpression.Should().Be("OrderId, #proj_0, Price");
        result.Shape.Should().Be(ProjectionShape.Composite);
        result.ExpressionAttributeNames["#proj_0"].Should().Be("Name");
        result.PropertyPaths.Should().HaveCount(3);
    }

    #endregion

    #region Validation

    [Fact]
    public void BuildProjection_NullSelector_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _builder.BuildProjection<TestEntity>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("selector");
    }

    [Fact]
    public void BuildProjection_UnsupportedExpression_ThrowsUnsupportedExpressionException()
    {
        // Act - method call is not supported
        Action act = () => _builder.BuildProjection(p => p.OrderId.ToUpper());

        // Assert
        act.Should().Throw<UnsupportedExpressionException>();
    }

    #endregion

    #region Caching

    [Fact]
    public void BuildProjection_SameExpressionTwice_UsesCachedResult()
    {
        // Arrange
        var cache = new ExpressionCache();
        var builder = new ProjectionBuilder<TestEntity>(_resolverFactory, cache: cache);
        Expression<Func<TestEntity, object>> selector = p => new { p.OrderId, p.CustomerId };

        // Act
        var result1 = builder.BuildProjection(selector);
        var result2 = builder.BuildProjection(selector);

        // Assert
        result1.Should().BeSameAs(result2, "cached results should be the same instance");
    }

    #endregion

    #region Custom Attribute Names

    [Fact]
    public void BuildProjection_CustomAttributeName_UsesRemappedName()
    {
        // Arrange - Entity with custom attribute mapping
        var customFactory = new AttributeNameResolverFactoryBuilder()
            .Configure<EntityWithCustomAttribute>(builder => builder
                .Map(e => e.CustomerId, "cust_id"))
            .Build();

        var builder = new ProjectionBuilder<EntityWithCustomAttribute>(
            customFactory,
            cache: NullExpressionCache.Instance);

        // Act
        var result = builder.BuildProjection(p => new { p.OrderId, p.CustomerId });

        // Assert
        result.ProjectionExpression.Should().Be("OrderId, cust_id");
        result.ExpressionAttributeNames.Should().BeEmpty();
    }

    #endregion

    #region Special Characters

    [Fact]
    public void BuildProjection_SpecialCharactersInName_CreatesAlias()
    {
        // Arrange
        var customFactory = new AttributeNameResolverFactoryBuilder()
            .Configure<TestEntity>(builder => builder
                .Map(e => e.OrderId, "order.id")) // Contains dot
            .Build();

        var builder = new ProjectionBuilder<TestEntity>(
            customFactory,
            cache: NullExpressionCache.Instance);

        // Act
        var result = builder.BuildProjection(p => p.OrderId);

        // Assert
        result.ProjectionExpression.Should().Be("#proj_0");
        result.ExpressionAttributeNames["#proj_0"].Should().Be("order.id");
    }

    #endregion
}

/// <summary>
/// Test entity with custom attribute mapping.
/// </summary>
public class EntityWithCustomAttribute
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
}
