using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Attributes;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

/// <summary>
/// Comprehensive unit tests for UpdateExpressionBuilder (Spec 07).
/// Follows test plan from Spec 12 lines 488-520.
/// </summary>
public class UpdateExpressionBuilderTests
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;
    private readonly UpdateExpressionBuilder<UpdateTestEntity> _builder;

    public UpdateExpressionBuilderTests()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
        _builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);
    }

    #region Clause Generation

    [Fact]
    public void Set_GeneratesSETClause()
    {
        // Act
        var result = _builder
            .Set(p => p.Title, "New Title")
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Expression.Should().Be("SET Title = :upd_v0");
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.ExpressionAttributeValues.Should().ContainKey(":upd_v0")
            .WhoseValue.S.Should().Be("New Title");
    }

    [Fact]
    public void Increment_GeneratesAddExpression()
    {
        // Act
        var result = _builder
            .Increment(p => p.ViewCount, 1)
            .Build();

        // Assert
        result.Expression.Should().Be("SET ViewCount = ViewCount + :upd_v0");
        result.ExpressionAttributeValues[":upd_v0"].N.Should().Be("1");
    }

    [Fact]
    public void Decrement_GeneratesSubtractExpression()
    {
        // Act
        var result = _builder
            .Decrement(p => p.Price, 10.5m)
            .Build();

        // Assert
        result.Expression.Should().Be("SET Price = Price - :upd_v0");
        result.ExpressionAttributeValues[":upd_v0"].N.Should().Be("10.5");
    }

    [Fact]
    public void SetIfNotExists_GeneratesIfNotExistsFunction()
    {
        // Act
        var result = _builder
            .SetIfNotExists(p => p.CreatedAt, new DateTime(2024, 1, 1))
            .Build();

        // Assert
        result.Expression.Should().Be("SET CreatedAt = if_not_exists(CreatedAt, :upd_v0)");
        result.ExpressionAttributeValues[":upd_v0"].S.Should().NotBeEmpty();
    }

    [Fact]
    public void AppendToList_GeneratesListAppendFunction()
    {
        // Act
        var result = _builder
            .AppendToList(p => p.Tags, new List<string> { "new-tag" })
            .Build();

        // Assert
        result.Expression.Should().Be("SET Tags = list_append(Tags, :upd_v0)");
        result.ExpressionAttributeValues[":upd_v0"].L.Should().HaveCount(1);
        result.ExpressionAttributeValues[":upd_v0"].L[0].S.Should().Be("new-tag");
    }

    [Fact]
    public void Remove_GeneratesREMOVEClause()
    {
        // Act
        var result = _builder
            .Remove(p => p.TempFlag)
            .Build();

        // Assert
        result.Expression.Should().Be("REMOVE TempFlag");
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.ExpressionAttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void Add_GeneratesADDClause()
    {
        // Act
        var result = _builder
            .Add(p => p.ViewCount, 5)
            .Build();

        // Assert
        result.Expression.Should().Be("ADD ViewCount :upd_v0");
        result.ExpressionAttributeValues[":upd_v0"].N.Should().Be("5");
    }

    [Fact]
    public void Delete_GeneratesDELETEClause()
    {
        // Act
        var result = _builder
            .Delete(p => p.EnabledFeatures, new HashSet<string> { "feature1", "feature2" })
            .Build();

        // Assert
        result.Expression.Should().Be("DELETE EnabledFeatures :upd_v0");
        result.ExpressionAttributeValues[":upd_v0"].SS.Should().HaveCount(2);
        result.ExpressionAttributeValues[":upd_v0"].SS.Should().Contain("feature1");
        result.ExpressionAttributeValues[":upd_v0"].SS.Should().Contain("feature2");
    }

    [Fact]
    public void MultipleClauses_CombinedCorrectly()
    {
        // Act
        var result = _builder
            .Set(p => p.Title, "Updated")
            .Increment(p => p.ViewCount, 1)
            .Remove(p => p.TempFlag)
            .Add(p => p.Score, 10)
            .Delete(p => p.EnabledFeatures, new HashSet<string> { "beta" })
            .Build();

        // Assert
        result.Expression.Should().Contain("SET Title = :upd_v0, ViewCount = ViewCount + :upd_v1");
        result.Expression.Should().Contain("REMOVE TempFlag");
        result.Expression.Should().Contain("ADD Score :upd_v2");
        result.Expression.Should().Contain("DELETE EnabledFeatures :upd_v3");
    }

    [Fact]
    public void NoOperations_ReturnsEmpty()
    {
        // Act
        var result = _builder.Build();

        // Assert
        result.Should().NotBeNull();
        result.IsEmpty.Should().BeTrue();
        result.Expression.Should().BeEmpty();
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.ExpressionAttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void DuplicateProperty_LastWins()
    {
        // Act
        var result = _builder
            .Set(p => p.Title, "First")
            .Set(p => p.Title, "Second")
            .Build();

        // Assert - Should only have one SET operation for Title with the second value
        result.Expression.Should().Be("SET Title = :upd_v1");
        result.ExpressionAttributeValues.Should().ContainKey(":upd_v1")
            .WhoseValue.S.Should().Be("Second");
        // First value alias might still exist in dict, but expression should only reference the last one
    }

    #endregion

    #region Alias Scoping

    [Fact]
    public void UpdateAliases_UseUpdPrefix()
    {
        // Act
        var result = _builder
            .Set(p => p.Status, "Active") // "Status" is a reserved keyword
            .Build();

        // Assert
        result.ExpressionAttributeNames.Should().ContainKey("#upd_0")
            .WhoseValue.Should().Be("Status");
        result.Expression.Should().Contain("#upd_0");
    }

    [Fact]
    public void UpdateValueAliases_UseUpdVPrefix()
    {
        // Act
        var result = _builder
            .Set(p => p.Title, "Test")
            .Increment(p => p.ViewCount, 1)
            .Build();

        // Assert
        result.ExpressionAttributeValues.Should().ContainKey(":upd_v0");
        result.ExpressionAttributeValues.Should().ContainKey(":upd_v1");
    }

    [Fact]
    public void ReservedKeyword_AliasedInUpdateExpression()
    {
        // Act - "Name" and "Status" are reserved keywords
        var result = _builder
            .Set(p => p.Name, "John")
            .Set(p => p.Status, "Active")
            .Build();

        // Assert
        result.ExpressionAttributeNames.Should().Contain(new KeyValuePair<string, string>("#upd_0", "Name"));
        result.ExpressionAttributeNames.Should().Contain(new KeyValuePair<string, string>("#upd_1", "Status"));
        result.Expression.Should().Contain("#upd_0 = :upd_v0");
        result.Expression.Should().Contain("#upd_1 = :upd_v1");
    }

    #endregion

    #region Property Resolution

    [Fact]
    public void RemappedAttribute_UsesResolvedName()
    {
        // Arrange - Configure resolver with custom mapping
        var factoryBuilder = new AttributeNameResolverFactoryBuilder();
        factoryBuilder.Configure<UpdateTestEntity>(cfg =>
            cfg.Map(p => p.Title, "custom_title"));
        var factory = factoryBuilder.Build();
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(factory, _converterRegistry);

        // Act
        var result = builder
            .Set(p => p.Title, "Test")
            .Build();

        // Assert
        result.Expression.Should().Be("SET custom_title = :upd_v0");
    }

    [Fact]
    public void NestedProperty_ResolvesCrossType()
    {
        // Arrange - Configure nested type mappings
        var factoryBuilder = new AttributeNameResolverFactoryBuilder();
        factoryBuilder.Configure<UpdateTestEntity>(cfg =>
            cfg.Map(p => p.Address, "addr"));
        factoryBuilder.Configure<Address>(cfg =>
            cfg.Map(p => p.City, "city_name"));
        var factory = factoryBuilder.Build();
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(factory, _converterRegistry);

        // Act
        var result = builder
            .Set(p => p.Address!.City, "London")
            .Build();

        // Assert
        result.Expression.Should().Be("SET addr.city_name = :upd_v0");
    }

    #endregion

    #region Value Conversion

    [Fact]
    public void EnumValue_ConvertedViaRegistry()
    {
        // Act
        var result = _builder
            .Set(p => p.Priority, TestPriority.High)
            .Build();

        // Assert
        result.ExpressionAttributeValues[":upd_v0"].S.Should().Be("High");
    }

    #endregion

    #region Validation

    [Fact]
    public void ConflictingClauses_ThrowsInvalidUpdateException_WithPropertyName()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidUpdateException>(() =>
            _builder
                .Set(p => p.Title, "New")
                .Remove(p => p.Title)
                .Build());

        exception.PropertyName.Should().Be("Title");
        exception.Message.Should().Contain("conflicting");
    }

    [Fact]
    public void DynamoDbIgnore_ThrowsInvalidUpdateException_WithPropertyAndType()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidUpdateException>(() =>
            _builder
                .Set(p => p.InternalField, "value")
                .Build());

        exception.PropertyName.Should().Be("InternalField");
        exception.EntityType.Should().Be(typeof(UpdateTestEntity));
        exception.Message.Should().Contain("DynamoDbIgnore");
    }

    [Fact]
    public void NullPropertyExpression_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _builder.Set<string>(null!, "value"));
    }

    #endregion
}
