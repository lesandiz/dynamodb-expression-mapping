using System;
using System.Threading.Tasks;
using DynamoDb.ExpressionMapping.Attributes;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

/// <summary>
/// Comprehensive unit tests for KeyConditionExpressionBuilder (Spec 13).
/// Follows test plan from Spec 12 lines 442-486.
/// </summary>
public class KeyConditionExpressionBuilderTests
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;
    private readonly KeyConditionExpressionBuilder<KeyConditionTestEntity> _builder;

    public KeyConditionExpressionBuilderTests()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
        _builder = new KeyConditionExpressionBuilder<KeyConditionTestEntity>(_resolverFactory, _converterRegistry);
    }

    #region Partition Key Only

    [Fact]
    public void PartitionKeyOnly_GeneratesEqualityExpression()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.PK, "USER#123")
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Expression.Should().Be("PK = :key_v0");
        result.ExpressionAttributeNames.Should().BeEmpty(); // PK is not a reserved keyword
        result.ExpressionAttributeValues.Should().ContainKey(":key_v0")
            .WhoseValue.S.Should().Be("USER#123");
    }

    #endregion

    #region Partition Key + Sort Key Operators

    [Fact]
    public void SortKeyEquals_GeneratesEquality()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.PK, "USER#123")
            .WithSortKeyEquals(e => e.SK, "ORDER#456");

        // Assert
        result.Expression.Should().Be("PK = :key_v0 AND SK = :key_v1");
        result.ExpressionAttributeValues.Should().HaveCount(2);
        result.ExpressionAttributeValues[":key_v0"].S.Should().Be("USER#123");
        result.ExpressionAttributeValues[":key_v1"].S.Should().Be("ORDER#456");
    }

    [Fact]
    public void SortKeyLessThan_GeneratesLessThan()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.PK, "USER#123")
            .WithSortKeyLessThan(e => e.SK, "ORDER#999");

        // Assert
        result.Expression.Should().Be("PK = :key_v0 AND SK < :key_v1");
        result.ExpressionAttributeValues[":key_v1"].S.Should().Be("ORDER#999");
    }

    [Fact]
    public void SortKeyLessThanOrEqual_GeneratesLessThanOrEqual()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.PK, "USER#123")
            .WithSortKeyLessThanOrEqual(e => e.SK, "ORDER#999");

        // Assert
        result.Expression.Should().Be("PK = :key_v0 AND SK <= :key_v1");
    }

    [Fact]
    public void SortKeyGreaterThan_GeneratesGreaterThan()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.PK, "USER#123")
            .WithSortKeyGreaterThan(e => e.SK, "ORDER#100");

        // Assert
        result.Expression.Should().Be("PK = :key_v0 AND SK > :key_v1");
    }

    [Fact]
    public void SortKeyGreaterThanOrEqual_GeneratesGreaterThanOrEqual()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.PK, "USER#123")
            .WithSortKeyGreaterThanOrEqual(e => e.SK, "ORDER#100");

        // Assert
        result.Expression.Should().Be("PK = :key_v0 AND SK >= :key_v1");
    }

    [Fact]
    public void SortKeyBetween_GeneratesBETWEEN()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.PK, "USER#123")
            .WithSortKeyBetween(e => e.SK, "ORDER#100", "ORDER#999");

        // Assert
        result.Expression.Should().Be("PK = :key_v0 AND SK BETWEEN :key_v1 AND :key_v2");
        result.ExpressionAttributeValues.Should().HaveCount(3);
        result.ExpressionAttributeValues[":key_v1"].S.Should().Be("ORDER#100");
        result.ExpressionAttributeValues[":key_v2"].S.Should().Be("ORDER#999");
    }

    [Fact]
    public void SortKeyBeginsWith_GeneratesBeginsWithFunction()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.PK, "USER#123")
            .WithSortKeyBeginsWith(e => e.SK, "ORDER#");

        // Assert
        result.Expression.Should().Be("PK = :key_v0 AND begins_with(SK, :key_v1)");
        result.ExpressionAttributeValues[":key_v1"].S.Should().Be("ORDER#");
    }

    #endregion

    #region Alias Scoping

    [Fact]
    public void KeyConditionAliases_UseKeyPrefix()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.Name, "Reserved") // Name is a reserved keyword
            .Build();

        // Assert
        result.ExpressionAttributeNames.Should().ContainKey("#key_0")
            .WhoseValue.Should().Be("Name");
        result.Expression.Should().Contain("#key_0");
    }

    [Fact]
    public void KeyConditionValueAliases_UseKeyVPrefix()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.PK, "TEST")
            .WithSortKeyEquals(e => e.SK, "VALUE");

        // Assert
        result.ExpressionAttributeValues.Should().ContainKey(":key_v0");
        result.ExpressionAttributeValues.Should().ContainKey(":key_v1");
    }

    #endregion

    #region Smart Aliasing

    [Fact]
    public void NonReservedAttribute_UsedDirectly_NotAliased()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.PK, "USER#123")
            .Build();

        // Assert
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.Expression.Should().Be("PK = :key_v0");
    }

    [Fact]
    public void ReservedAttribute_Aliased()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.Name, "TestName") // Name is reserved
            .Build();

        // Assert
        result.ExpressionAttributeNames.Should().ContainKey("#key_0");
        result.Expression.Should().Be("#key_0 = :key_v0");
    }

    #endregion

    #region Property Resolution

    [Fact]
    public void RemappedAttribute_UsesResolvedName()
    {
        // Arrange
        var builder = new KeyConditionExpressionBuilder<RemappedEntity>(_resolverFactory, _converterRegistry);

        // Act
        var result = builder
            .WithPartitionKey(e => e.PartitionKey, "USER#123")
            .Build();

        // Assert
        result.Expression.Should().Be("pk = :key_v0");
        result.ExpressionAttributeNames.Should().BeEmpty();
    }

    #endregion

    #region Value Conversion

    [Fact]
    public void StringValue_ConvertedToStringAttributeValue()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.PK, "USER#123")
            .Build();

        // Assert
        result.ExpressionAttributeValues[":key_v0"].S.Should().Be("USER#123");
        result.ExpressionAttributeValues[":key_v0"].NULL.Should().BeFalse();
    }

    [Fact]
    public void GuidValue_ConvertedToStringAttributeValue()
    {
        // Arrange
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789012");

        // Act
        var result = _builder
            .WithPartitionKey(e => e.Id, guid)
            .Build();

        // Assert
        result.ExpressionAttributeValues[":key_v0"].S.Should().Be("12345678-1234-1234-1234-123456789012");
    }

    #endregion

    #region Result Type

    [Fact]
    public void Build_ReturnsKeyConditionExpressionResult()
    {
        // Act
        var result = _builder
            .WithPartitionKey(e => e.PK, "USER#123")
            .Build();

        // Assert
        result.Should().BeOfType<KeyConditionExpressionResult>();
        result.Expression.Should().NotBeNullOrEmpty();
        result.ExpressionAttributeNames.Should().NotBeNull();
        result.ExpressionAttributeValues.Should().NotBeNull();
    }

    #endregion

    #region Validation Errors

    [Fact]
    public void NullPropertyExpression_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _builder.WithPartitionKey<string>(null!, "VALUE");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("property");
    }

    [Fact]
    public void DynamoDbIgnore_ThrowsInvalidKeyConditionException_WithPropertyAndType()
    {
        // Act
        Action act = () => _builder.WithPartitionKey(e => e.InternalField, "VALUE");

        // Assert
        act.Should().Throw<InvalidKeyConditionException>()
            .Which.PropertyName.Should().Be("InternalField");
    }

    [Fact]
    public void NestedProperty_ThrowsInvalidKeyConditionException_WithPropertyName()
    {
        // Act
        Action act = () => _builder.WithPartitionKey(e => e.Address.City, "Seattle");

        // Assert
        act.Should().Throw<InvalidKeyConditionException>()
            .WithMessage("*nested*")
            .Which.PropertyName.Should().Be("Address.City");
    }

    [Fact]
    public void NullPartitionKeyValue_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _builder.WithPartitionKey(e => e.PK, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("value");
    }

    [Fact]
    public void Between_LowGreaterThanHigh_ThrowsArgumentException()
    {
        // Arrange
        var partitionBuilder = _builder.WithPartitionKey(e => e.PK, "USER#123");

        // Act
        Action act = () => partitionBuilder.WithSortKeyBetween(e => e.SK, "ORDER#999", "ORDER#100");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Low value must be less than or equal to high value*")
            .WithParameterName("low");
    }

    [Fact]
    public void BeginsWith_NullPrefix_ThrowsArgumentException()
    {
        // Arrange
        var partitionBuilder = _builder.WithPartitionKey(e => e.PK, "USER#123");

        // Act
        Action act = () => partitionBuilder.WithSortKeyBeginsWith(e => e.SK, null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null*")
            .WithParameterName("prefix");
    }

    [Fact]
    public void BeginsWith_EmptyPrefix_ThrowsArgumentException()
    {
        // Arrange
        var partitionBuilder = _builder.WithPartitionKey(e => e.PK, "USER#123");

        // Act
        Action act = () => partitionBuilder.WithSortKeyBeginsWith(e => e.SK, string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*")
            .WithParameterName("prefix");
    }

    #endregion

    #region Thread Safety

    [Fact]
    public async Task ConcurrentWithPartitionKey_ProducesIndependentBuilders()
    {
        // Act - spawn 10 concurrent calls to WithPartitionKey
        var tasks = Enumerable.Range(0, 10)
            .Select(i => Task.Run(() =>
            {
                var result = _builder
                    .WithPartitionKey(e => e.PK, $"USER#{i}")
                    .WithSortKeyEquals(e => e.SK, $"ORDER#{i}");

                return result;
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - each result should have unique values
        results.Should().HaveCount(10);
        for (int i = 0; i < 10; i++)
        {
            results[i].ExpressionAttributeValues[":key_v0"].S.Should().Be($"USER#{i}");
            results[i].ExpressionAttributeValues[":key_v1"].S.Should().Be($"ORDER#{i}");
        }
    }

    #endregion
}
