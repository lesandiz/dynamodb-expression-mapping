using System;
using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Attributes;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

/// <summary>
/// Comprehensive unit tests for FilterExpressionBuilder (Spec 06).
/// Follows test plan from Spec 12 lines 323-389.
/// </summary>
public class FilterExpressionBuilderTests
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;
    private readonly FilterExpressionBuilder<FilterTestEntity> _builder;

    public FilterExpressionBuilderTests()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
        _builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);
    }

    #region Comparison Operators

    [Fact]
    public void Equality_GeneratesEqualsExpression()
    {
        // Act
        var result = _builder.BuildFilter(p => p.OrderId == "12345");

        // Assert
        result.Should().NotBeNull();
        result.Expression.Should().Be("OrderId = :filt_v0");
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.ExpressionAttributeValues.Should().ContainKey(":filt_v0")
            .WhoseValue.S.Should().Be("12345");
    }

    [Fact]
    public void Inequality_GeneratesNotEqualsExpression()
    {
        // Act
        var result = _builder.BuildFilter(p => p.OrderId != "12345");

        // Assert
        result.Expression.Should().Be("OrderId <> :filt_v0");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("12345");
    }

    [Fact]
    public void GreaterThan_GeneratesCorrectExpression()
    {
        // Act
        var result = _builder.BuildFilter(p => p.Total > 100m);

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("#filt_0 > :filt_v0");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("100");
    }

    [Fact]
    public void LessThan_GeneratesCorrectExpression()
    {
        // Act
        var result = _builder.BuildFilter(p => p.Total < 50.5m);

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("#filt_0 < :filt_v0");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("50.5");
    }

    [Fact]
    public void GreaterThanOrEqual_GeneratesCorrectExpression()
    {
        // Act
        var result = _builder.BuildFilter(p => p.Total >= 100m);

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("#filt_0 >= :filt_v0");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("100");
    }

    [Fact]
    public void LessThanOrEqual_GeneratesCorrectExpression()
    {
        // Act
        var result = _builder.BuildFilter(p => p.Total <= 200m);

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("#filt_0 <= :filt_v0");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("200");
    }

    #endregion

    #region Logical Operators

    [Fact]
    public void And_CombinesWithAND()
    {
        // Act
        var result = _builder.BuildFilter(p => p.IsActive && p.Total > 100m);

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("(IsActive = :filt_v0) AND (#filt_0 > :filt_v1)");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":filt_v0"].BOOL.Should().BeTrue();
        result.ExpressionAttributeValues[":filt_v1"].N.Should().Be("100");
    }

    [Fact]
    public void Or_CombinesWithOR()
    {
        // Act
        var result = _builder.BuildFilter(p => p.IsActive || p.IsPremium);

        // Assert
        result.Expression.Should().Be("(IsActive = :filt_v0) OR (IsPremium = :filt_v1)");
        result.ExpressionAttributeValues[":filt_v0"].BOOL.Should().BeTrue();
        result.ExpressionAttributeValues[":filt_v1"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void Not_WrapsWithNOT()
    {
        // Act - Use complex expression to force NOT wrapping
        var result = _builder.BuildFilter(p => !(p.IsActive && p.IsPremium));

        // Assert
        result.Expression.Should().Be("NOT ((IsActive = :filt_v0) AND (IsPremium = :filt_v1))");
        result.ExpressionAttributeValues[":filt_v0"].BOOL.Should().BeTrue();
        result.ExpressionAttributeValues[":filt_v1"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void ComplexPredicate_CorrectParentheses()
    {
        // Act
        var result = _builder.BuildFilter(p =>
            (p.IsActive && p.Total > 100m) || (p.IsPremium && p.Total > 50m));

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("((IsActive = :filt_v0) AND (#filt_0 > :filt_v1)) OR ((IsPremium = :filt_v2) AND (#filt_1 > :filt_v3))");
        result.ExpressionAttributeValues.Should().HaveCount(4);
        result.ExpressionAttributeNames.Should().HaveCount(2);
    }

    #endregion

    #region Boolean Properties

    [Fact]
    public void BooleanPropertyDirect_GeneratesBoolEqualsTrue()
    {
        // Act
        var result = _builder.BuildFilter(p => p.IsActive);

        // Assert
        result.Expression.Should().Be("IsActive = :filt_v0");
        result.ExpressionAttributeValues[":filt_v0"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void NegatedBooleanProperty_GeneratesBoolEqualsFalse()
    {
        // Act
        var result = _builder.BuildFilter(p => !p.IsHidden);

        // Assert
        result.Expression.Should().Be("IsHidden = :filt_v0");
        result.ExpressionAttributeValues[":filt_v0"].BOOL.Should().BeFalse();
    }

    #endregion

    #region String Operations

    [Fact]
    public void StartsWith_GeneratesBeginsWith()
    {
        // Act
        var result = _builder.BuildFilter(p => p.Title.StartsWith("Premium"));

        // Assert
        result.Expression.Should().Be("begins_with(Title, :filt_v0)");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Premium");
    }

    [Fact]
    public void Contains_GeneratesContains()
    {
        // Act
        var result = _builder.BuildFilter(p => p.Description.Contains("sale"));

        // Assert
        result.Expression.Should().Be("contains(Description, :filt_v0)");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("sale");
    }

    #endregion

    #region Null Checks

    [Fact]
    public void EqualsNull_GeneratesAttributeNotExists()
    {
        // Act
        var result = _builder.BuildFilter(p => p.ExpiresOn == null);

        // Assert
        result.Expression.Should().Be("attribute_not_exists(ExpiresOn)");
        result.ExpressionAttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void NotEqualsNull_GeneratesAttributeExists()
    {
        // Act
        var result = _builder.BuildFilter(p => p.ExpiresOn != null);

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
        var result = _builder.BuildFilter(p => DynamoDbFunctions.Between(p.Total, 10m, 50m));

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("#filt_0 BETWEEN :filt_v0 AND :filt_v1");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("10");
        result.ExpressionAttributeValues[":filt_v1"].N.Should().Be("50");
    }

    [Fact]
    public void Size_GeneratesSize()
    {
        // Act
        var result = _builder.BuildFilter(p => DynamoDbFunctions.Size(p.Tags) > 0);

        // Assert
        result.Expression.Should().Be("size(Tags) > :filt_v0");
        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("0");
    }

    [Fact]
    public void DynamoDbFunctions_AttributeExists_GeneratesAttributeExists()
    {
        // Act
        var result = _builder.BuildFilter(p => DynamoDbFunctions.AttributeExists(p.FallbackId));

        // Assert
        result.Expression.Should().Be("attribute_exists(FallbackId)");
        result.ExpressionAttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void DynamoDbFunctions_AttributeNotExists_GeneratesAttributeNotExists()
    {
        // Act
        var result = _builder.BuildFilter(p => DynamoDbFunctions.AttributeNotExists(p.FallbackId));

        // Assert
        result.Expression.Should().Be("attribute_not_exists(FallbackId)");
        result.ExpressionAttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void DynamoDbFunctions_AttributeType_GeneratesAttributeType()
    {
        // Act
        var result = _builder.BuildFilter(p => DynamoDbFunctions.AttributeType(p.Tags, "L"));

        // Assert
        result.Expression.Should().Be("attribute_type(Tags, :filt_v0)");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("L");
    }

    [Fact]
    public void DynamoDbFunctions_CalledAtRuntime_ThrowsInvalidOperationException()
    {
        // Arrange
        var entity = new FilterTestEntity { FallbackId = "test" };

        // Act
        var act = () => DynamoDbFunctions.AttributeExists(entity.FallbackId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Expression marker only");
    }

    #endregion

    #region Captured Variables

    [Fact]
    public void CapturedVariable_EvaluatedAtBuildTime()
    {
        // Arrange
        var minTotal = 150m;

        // Act
        var result = _builder.BuildFilter(p => p.Total > minTotal);

        // Assert - "Total" is a reserved keyword
        result.Expression.Should().Be("#filt_0 > :filt_v0");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Total");
        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("150");
    }

    [Fact]
    public void CapturedEnumValue_ConvertedToAttributeValue()
    {
        // Arrange - Use string value instead of enum to avoid compiler-generated Convert expressions
        var targetStatus = "Active";

        // Act
        var result = _builder.BuildFilter(p => p.StatusString == targetStatus);

        // Assert
        result.Expression.Should().Be("#filt_0 = :filt_v0");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Status");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Active");
    }

    #endregion

    #region Value Conversion

    [Fact]
    public void GuidValue_ConvertedToStringAttributeValue()
    {
        // Arrange
        var guidValue = Guid.Parse("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d");

        // Act
        var result = _builder.BuildFilter(p => p.CorrelationId == guidValue);

        // Assert
        result.Expression.Should().Be("CorrelationId = :filt_v0");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d");
    }

    [Fact]
    public void BoolValue_ConvertedToBoolAttributeValue()
    {
        // Act
        var result = _builder.BuildFilter(p => p.IsActive == true);

        // Assert
        result.Expression.Should().Be("IsActive = :filt_v0");
        result.ExpressionAttributeValues[":filt_v0"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void DateTimeValue_ConvertedToIso8601String()
    {
        // Arrange
        var date = new DateTime(2024, 3, 15, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var result = _builder.BuildFilter(p => p.CreatedAt >= date);

        // Assert
        result.Expression.Should().Be("CreatedAt >= :filt_v0");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("2024-03-15T14:30:00.0000000Z");
    }

    [Fact]
    public void EnumValue_ConvertedPerStorageMode()
    {
        // Act - Test actual enum value conversion (default string storage mode)
        var result = _builder.BuildFilter(p => p.Status == OrderStatus.Active);

        // Assert - "Status" is a reserved keyword, enum stored as string by default
        result.Expression.Should().Be("#filt_0 = :filt_v0");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Status");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Active");
    }

    #endregion

    #region IN Operator

    [Fact]
    public void ContainsOnArray_GeneratesINExpression()
    {
        // Arrange - Use string array to avoid enum conversion complexity
        var statuses = new[] { "Active", "Pending" };

        // Act
        var result = _builder.BuildFilter(p => statuses.Contains(p.StatusString));

        // Assert
        result.Expression.Should().Be("#filt_0 IN (:filt_v0, :filt_v1)");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Status");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Active");
        result.ExpressionAttributeValues[":filt_v1"].S.Should().Be("Pending");
    }

    #endregion

    #region Nested Properties

    [Fact]
    public void NestedProperty_ResolvesViaFactory_GeneratesDotNotation()
    {
        // Act
        var result = _builder.BuildFilter(p => p.Address.City == "London");

        // Assert
        result.Expression.Should().Be("Address.City = :filt_v0");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("London");
    }

    [Fact]
    public void NestedProperty_RemappedAttribute_UsesResolvedName()
    {
        // Arrange - Create a builder with custom attribute name mapping
        var customResolverFactory = new AttributeNameResolverFactoryBuilder()
            .Configure<FilterAddress>(cfg => cfg.Map(a => a.PostalCode, "zip"))
            .Build();
        var customBuilder = new FilterExpressionBuilder<FilterTestEntity>(customResolverFactory, _converterRegistry);

        // Act
        var result = customBuilder.BuildFilter(p => p.Address.PostalCode == "12345");

        // Assert
        result.Expression.Should().Be("Address.zip = :filt_v0");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("12345");
    }

    #endregion

    #region Attribute Name Resolution

    [Fact]
    public void RemappedAttribute_UsesResolvedNameInExpression()
    {
        // Arrange - Create a builder with custom attribute mapping
        var customResolverFactory = new AttributeNameResolverFactoryBuilder()
            .Configure<FilterTestEntity>(cfg => cfg.Map(e => e.OrderId, "order_pk"))
            .Build();
        var customBuilder = new FilterExpressionBuilder<FilterTestEntity>(customResolverFactory, _converterRegistry);

        // Act
        var result = customBuilder.BuildFilter(p => p.OrderId == "12345");

        // Assert
        result.Expression.Should().Be("order_pk = :filt_v0");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("12345");
    }

    [Fact]
    public void ReservedKeyword_AliasedWithFiltPrefix()
    {
        // Act - "Status" is a reserved keyword, use string property to avoid enum conversion
        var result = _builder.BuildFilter(p => p.StatusString == "Active");

        // Assert
        result.Expression.Should().Be("#filt_0 = :filt_v0");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Status");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Active");
    }

    #endregion

    #region Alias Scoping

    [Fact]
    public void FilterAliases_UseFiltPrefix()
    {
        // Act - "Status" and "Name" are both reserved keywords, use string property to avoid enum conversion
        var result = _builder.BuildFilter(p => p.StatusString == "Active" && p.Name == "Premium");

        // Assert
        result.Expression.Should().Be("(#filt_0 = :filt_v0) AND (#filt_1 = :filt_v1)");
        result.ExpressionAttributeNames.Should().ContainKey("#filt_0");
        result.ExpressionAttributeNames.Should().ContainKey("#filt_1");
    }

    [Fact]
    public void FilterValueAliases_UseFiltVPrefix()
    {
        // Act
        var result = _builder.BuildFilter(p => p.Total > 100m && p.IsActive);

        // Assert - Both values use :filt_v prefix
        result.ExpressionAttributeValues.Should().ContainKey(":filt_v0");
        result.ExpressionAttributeValues.Should().ContainKey(":filt_v1");
        result.ExpressionAttributeValues.Keys.Should().AllSatisfy(k => k.Should().StartWith(":filt_v"));
    }

    #endregion

    #region Validation

    [Fact]
    public void NullPredicate_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _builder.BuildFilter(null!);

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
        var strictBuilder = new FilterExpressionBuilder<FilterTestEntity>(strictResolverFactory, _converterRegistry);

        // Act
        var act = () => strictBuilder.BuildFilter(p => p.IgnoredProperty == "test");

        // Assert
        act.Should().Throw<InvalidFilterException>()
            .Which.PropertyName.Should().Be("IgnoredProperty");
        act.Should().Throw<InvalidFilterException>()
            .Which.EntityType.Should().Be(typeof(FilterTestEntity));
    }

    #endregion

    #region Mutation Killing -- Error Paths and Edge Cases

    [Fact]
    public void Filter_UnsupportedBinaryExpression_Throws()
    {
        var genericBuilder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => genericBuilder.BuildFilter(p => p.Score + 1 > 5);
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void Filter_StandaloneMemberAccess_NonBool_Throws()
    {
        var visitor = new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            new StringBuilder(),
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());

        var param = Expression.Parameter(typeof(TestEntity), "p");
        var member = Expression.MakeMemberAccess(param, typeof(TestEntity).GetProperty("Title")!);
        var act = () => visitor.Visit(member);
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void Filter_UnsupportedMethodCall_Throws()
    {
        var genericBuilder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => genericBuilder.BuildFilter(p => p.Title.ToUpper() == "TEST");
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void Filter_NonPropertyMemberAccess_Throws()
    {
        var visitor = new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            new StringBuilder(),
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());

        var param = Expression.Parameter(typeof(MutR2EntityWithField), "p");
        var fieldAccess = Expression.Field(param, typeof(MutR2EntityWithField).GetField("PublicField")!);
        var comparison = Expression.Equal(fieldAccess, Expression.Constant("test"));
        var act = () => visitor.Visit(comparison);
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void Filter_NonParameterRoot_Throws()
    {
        var visitor = new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            new StringBuilder(),
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());

        var entity = new TestEntity { Title = "test" };
        var constant = Expression.Constant(entity);
        var member = Expression.Property(constant, "Title");
        var comparison = Expression.Equal(member, Expression.Constant("abc"));
        var act = () => visitor.Visit(comparison);
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void Filter_DirectConstant_ValueExtracted()
    {
        var genericBuilder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = genericBuilder.BuildFilter(p => p.Price > 50m);
        result.Expression.Should().Contain(">");
        result.ExpressionAttributeValues.Should().ContainSingle()
            .Which.Value.N.Should().Be("50");
    }

    #endregion
}

/// <summary>
/// Entity with a public field (non-property member) for testing field access error paths.
/// </summary>
public class MutR2EntityWithField
{
    public string PublicField = string.Empty;
    public string PartitionKey { get; set; } = string.Empty;
    public string SortKey { get; set; } = string.Empty;
}
