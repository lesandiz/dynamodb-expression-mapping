using System;
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
/// Unit tests for ConditionExpressionBuilder (Spec 06).
/// ConditionExpressionBuilder is functionally identical to FilterExpressionBuilder
/// but uses #cond_ prefix for name aliases and :cond_v prefix for value aliases.
/// Follows test plan from Spec 12 lines 323-389.
/// </summary>
public class ConditionExpressionBuilderTests
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;
    private readonly ConditionExpressionBuilder<FilterTestEntity> _builder;

    public ConditionExpressionBuilderTests()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
        _builder = new ConditionExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);
    }

    #region Comparison Operators

    [Fact]
    public void Equality_GeneratesEqualsExpression()
    {
        // Act
        var result = _builder.BuildCondition(p => p.OrderId == "12345");

        // Assert
        result.Should().NotBeNull();
        result.Expression.Should().Be("OrderId = :cond_v0");
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.ExpressionAttributeValues.Should().ContainKey(":cond_v0")
            .WhoseValue.S.Should().Be("12345");
    }

    [Fact]
    public void Inequality_GeneratesNotEqualsExpression()
    {
        // Act
        var result = _builder.BuildCondition(p => p.OrderId != "12345");

        // Assert
        result.Expression.Should().Be("OrderId <> :cond_v0");
        result.ExpressionAttributeValues[":cond_v0"].S.Should().Be("12345");
    }

    [Fact]
    public void GreaterThan_GeneratesCorrectExpression()
    {
        // Act
        var result = _builder.BuildCondition(p => p.Total > 100m);

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("#cond_0 > :cond_v0");
        result.ExpressionAttributeNames["#cond_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":cond_v0"].N.Should().Be("100");
    }

    [Fact]
    public void LessThan_GeneratesCorrectExpression()
    {
        // Act
        var result = _builder.BuildCondition(p => p.Total < 50.5m);

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("#cond_0 < :cond_v0");
        result.ExpressionAttributeNames["#cond_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":cond_v0"].N.Should().Be("50.5");
    }

    [Fact]
    public void GreaterThanOrEqual_GeneratesCorrectExpression()
    {
        // Act
        var result = _builder.BuildCondition(p => p.Total >= 100m);

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("#cond_0 >= :cond_v0");
        result.ExpressionAttributeNames["#cond_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":cond_v0"].N.Should().Be("100");
    }

    [Fact]
    public void LessThanOrEqual_GeneratesCorrectExpression()
    {
        // Act
        var result = _builder.BuildCondition(p => p.Total <= 200m);

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("#cond_0 <= :cond_v0");
        result.ExpressionAttributeNames["#cond_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":cond_v0"].N.Should().Be("200");
    }

    #endregion

    #region Logical Operators

    [Fact]
    public void And_CombinesWithAND()
    {
        // Act
        var result = _builder.BuildCondition(p => p.IsActive && p.Total > 100m);

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("(IsActive = :cond_v0) AND (#cond_0 > :cond_v1)");
        result.ExpressionAttributeNames["#cond_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":cond_v0"].BOOL.Should().BeTrue();
        result.ExpressionAttributeValues[":cond_v1"].N.Should().Be("100");
    }

    [Fact]
    public void Or_CombinesWithOR()
    {
        // Act
        var result = _builder.BuildCondition(p => p.IsActive || p.IsPremium);

        // Assert
        result.Expression.Should().Be("(IsActive = :cond_v0) OR (IsPremium = :cond_v1)");
        result.ExpressionAttributeValues[":cond_v0"].BOOL.Should().BeTrue();
        result.ExpressionAttributeValues[":cond_v1"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void Not_WrapsWithNOT()
    {
        // Act - Use complex expression to force NOT wrapping
        var result = _builder.BuildCondition(p => !(p.IsActive && p.IsPremium));

        // Assert
        result.Expression.Should().Be("NOT ((IsActive = :cond_v0) AND (IsPremium = :cond_v1))");
        result.ExpressionAttributeValues[":cond_v0"].BOOL.Should().BeTrue();
        result.ExpressionAttributeValues[":cond_v1"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void ComplexPredicate_CorrectParentheses()
    {
        // Act
        var result = _builder.BuildCondition(p =>
            (p.IsActive && p.Total > 100m) || (p.IsPremium && p.Total > 50m));

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("((IsActive = :cond_v0) AND (#cond_0 > :cond_v1)) OR ((IsPremium = :cond_v2) AND (#cond_1 > :cond_v3))");
        result.ExpressionAttributeValues.Should().HaveCount(4);
        result.ExpressionAttributeNames.Should().HaveCount(2);
    }

    #endregion

    #region Null Checks

    [Fact]
    public void EqualsNull_GeneratesAttributeNotExists()
    {
        // Act
        var result = _builder.BuildCondition(p => p.ExpiresOn == null);

        // Assert
        result.Expression.Should().Be("attribute_not_exists(ExpiresOn)");
        result.ExpressionAttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void NotEqualsNull_GeneratesAttributeExists()
    {
        // Act
        var result = _builder.BuildCondition(p => p.ExpiresOn != null);

        // Assert
        result.Expression.Should().Be("attribute_exists(ExpiresOn)");
        result.ExpressionAttributeValues.Should().BeEmpty();
    }

    #endregion

    #region DynamoDB Functions

    [Fact]
    public void Between_GeneratesBETWEEN()
    {
        // Act
        var result = _builder.BuildCondition(p => DynamoDbFunctions.Between(p.Total, 10m, 50m));

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("#cond_0 BETWEEN :cond_v0 AND :cond_v1");
        result.ExpressionAttributeNames["#cond_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":cond_v0"].N.Should().Be("10");
        result.ExpressionAttributeValues[":cond_v1"].N.Should().Be("50");
    }

    [Fact]
    public void Size_GeneratesSize()
    {
        // Act
        var result = _builder.BuildCondition(p => DynamoDbFunctions.Size(p.Tags) > 0);

        // Assert
        result.Expression.Should().Be("size(Tags) > :cond_v0");
        result.ExpressionAttributeValues[":cond_v0"].N.Should().Be("0");
    }

    [Fact]
    public void DynamoDbFunctions_AttributeExists_GeneratesAttributeExists()
    {
        // Act
        var result = _builder.BuildCondition(p => DynamoDbFunctions.AttributeExists(p.FallbackId));

        // Assert
        result.Expression.Should().Be("attribute_exists(FallbackId)");
        result.ExpressionAttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void DynamoDbFunctions_AttributeNotExists_GeneratesAttributeNotExists()
    {
        // Act
        var result = _builder.BuildCondition(p => DynamoDbFunctions.AttributeNotExists(p.FallbackId));

        // Assert
        result.Expression.Should().Be("attribute_not_exists(FallbackId)");
        result.ExpressionAttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void DynamoDbFunctions_AttributeType_GeneratesAttributeType()
    {
        // Act
        var result = _builder.BuildCondition(p => DynamoDbFunctions.AttributeType(p.Tags, "L"));

        // Assert
        result.Expression.Should().Be("attribute_type(Tags, :cond_v0)");
        result.ExpressionAttributeValues[":cond_v0"].S.Should().Be("L");
    }

    [Fact]
    public void StartsWith_GeneratesBeginsWith()
    {
        // Act
        var result = _builder.BuildCondition(p => p.Title.StartsWith("Premium"));

        // Assert
        result.Expression.Should().Be("begins_with(Title, :cond_v0)");
        result.ExpressionAttributeValues[":cond_v0"].S.Should().Be("Premium");
    }

    [Fact]
    public void Contains_GeneratesContains()
    {
        // Act
        var result = _builder.BuildCondition(p => p.Description.Contains("sale"));

        // Assert
        result.Expression.Should().Be("contains(Description, :cond_v0)");
        result.ExpressionAttributeValues[":cond_v0"].S.Should().Be("sale");
    }

    #endregion

    #region Alias Scoping Verification

    [Fact]
    public void ConditionAliases_UseCondPrefix()
    {
        // Act - "Status" and "Name" are both reserved keywords
        var result = _builder.BuildCondition(p => p.StatusString == "Active" && p.Name == "Premium");

        // Assert - Verify #cond_ prefix, NOT #filt_
        result.Expression.Should().Be("(#cond_0 = :cond_v0) AND (#cond_1 = :cond_v1)");
        result.ExpressionAttributeNames.Should().ContainKey("#cond_0");
        result.ExpressionAttributeNames.Should().ContainKey("#cond_1");
        result.ExpressionAttributeNames.Keys.Should().AllSatisfy(k => k.Should().StartWith("#cond_"));
    }

    [Fact]
    public void ConditionValueAliases_UseCondVPrefix()
    {
        // Act
        var result = _builder.BuildCondition(p => p.Total > 100m && p.IsActive);

        // Assert - Verify :cond_v prefix, NOT :filt_v
        result.ExpressionAttributeValues.Should().ContainKey(":cond_v0");
        result.ExpressionAttributeValues.Should().ContainKey(":cond_v1");
        result.ExpressionAttributeValues.Keys.Should().AllSatisfy(k => k.Should().StartWith(":cond_v"));
    }

    [Fact]
    public void MultipleConditions_AllUseCondScope()
    {
        // Act - Build a complex condition to verify all aliases use cond scope
        var result = _builder.BuildCondition(p =>
            p.StatusString == "Active" &&
            p.Total > 100m &&
            DynamoDbFunctions.Between(p.Total, 50m, 200m));

        // Assert
        result.Expression.Should().Contain("#cond_");
        result.Expression.Should().Contain(":cond_v");
        result.Expression.Should().NotContain("#filt_");
        result.Expression.Should().NotContain(":filt_v");
        result.ExpressionAttributeNames.Keys.Should().AllSatisfy(k => k.Should().StartWith("#cond_"));
        result.ExpressionAttributeValues.Keys.Should().AllSatisfy(k => k.Should().StartWith(":cond_v"));
    }

    #endregion

    #region Nested Properties

    [Fact]
    public void NestedProperty_ResolvesViaFactory_GeneratesDotNotation()
    {
        // Act
        var result = _builder.BuildCondition(p => p.Address.City == "London");

        // Assert
        result.Expression.Should().Be("Address.City = :cond_v0");
        result.ExpressionAttributeValues[":cond_v0"].S.Should().Be("London");
    }

    [Fact]
    public void NestedProperty_RemappedAttribute_UsesResolvedName()
    {
        // Arrange - Create a builder with custom attribute name mapping
        var customResolverFactory = new AttributeNameResolverFactoryBuilder()
            .Configure<FilterAddress>(cfg => cfg.Map(a => a.PostalCode, "zip"))
            .Build();
        var customBuilder = new ConditionExpressionBuilder<FilterTestEntity>(customResolverFactory, _converterRegistry);

        // Act
        var result = customBuilder.BuildCondition(p => p.Address.PostalCode == "12345");

        // Assert
        result.Expression.Should().Be("Address.zip = :cond_v0");
        result.ExpressionAttributeValues[":cond_v0"].S.Should().Be("12345");
    }

    #endregion

    #region Reserved Keywords

    [Fact]
    public void ReservedKeyword_AliasedWithCondPrefix()
    {
        // Act - "Status" is a reserved keyword
        var result = _builder.BuildCondition(p => p.StatusString == "Active");

        // Assert - Verify #cond_ prefix, NOT #filt_
        result.Expression.Should().Be("#cond_0 = :cond_v0");
        result.ExpressionAttributeNames["#cond_0"].Should().Be("Status");
        result.ExpressionAttributeValues[":cond_v0"].S.Should().Be("Active");
    }

    #endregion

    #region Validation

    [Fact]
    public void NullPredicate_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _builder.BuildCondition(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("predicate");
    }

    [Fact]
    public void DynamoDbIgnore_StrictMode_ThrowsInvalidFilterException_WithPropertyAndType()
    {
        // Arrange - Create a builder with strict mode enabled
        var strictResolverFactory = new AttributeNameResolverFactoryBuilder()
            .WithMode(NameResolutionMode.Strict)
            .Build();
        var strictBuilder = new ConditionExpressionBuilder<FilterTestEntity>(strictResolverFactory, _converterRegistry);

        // Act
        var act = () => strictBuilder.BuildCondition(p => p.IgnoredProperty == "test");

        // Assert - ConditionExpressionBuilder uses FilterExpressionVisitor internally, so throws InvalidFilterException
        act.Should().Throw<InvalidFilterException>()
            .Which.PropertyName.Should().Be("IgnoredProperty");
        act.Should().Throw<InvalidFilterException>()
            .Which.EntityType.Should().Be(typeof(FilterTestEntity));
    }

    #endregion

    #region Value Conversion

    [Fact]
    public void GuidValue_ConvertedToStringAttributeValue()
    {
        // Arrange
        var guidValue = Guid.Parse("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d");

        // Act
        var result = _builder.BuildCondition(p => p.CorrelationId == guidValue);

        // Assert
        result.Expression.Should().Be("CorrelationId = :cond_v0");
        result.ExpressionAttributeValues[":cond_v0"].S.Should().Be("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d");
    }

    [Fact]
    public void DateTimeValue_ConvertedToIso8601String()
    {
        // Arrange
        var date = new DateTime(2024, 3, 15, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var result = _builder.BuildCondition(p => p.CreatedAt >= date);

        // Assert
        result.Expression.Should().Be("CreatedAt >= :cond_v0");
        result.ExpressionAttributeValues[":cond_v0"].S.Should().Be("2024-03-15T14:30:00.0000000Z");
    }

    #endregion
}
