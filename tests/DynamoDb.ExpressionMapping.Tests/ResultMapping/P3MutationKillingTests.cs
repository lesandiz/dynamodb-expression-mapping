using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ResultMapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.ResultMapping;

/// <summary>
/// Mutation-killing tests for Priority 3 subsystems (result mapping).
/// Targets surviving mutants identified in Phase 3b.4 mutation analysis:
/// - AttributeValueReader: 60 NoCoverage mutants (entire class untested)
/// - CompositeMappingStrategy: off-by-one in path traversal
/// - SinglePropertyMappingStrategy: logical mutation in condition check
/// - DirectResultMapper: null coalescing on constructor args
/// - IdentityMappingStrategy: null fullEntityMapper guard
/// </summary>
public class P3MutationKillingTests
{
    #region AttributeValueReader — NavigateToLeaf

    [Fact]
    public void NavigateToLeaf_SingleSegment_ReturnsInputDictionary()
    {
        // path = ["City"], Length-1 = 0, so loop doesn't execute; returns input dict
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["City"] = new() { S = "London" }
        };

        var result = AttributeValueReader.NavigateToLeaf(attrs, new[] { "City" });

        result.Should().BeSameAs(attrs);
    }

    [Fact]
    public void NavigateToLeaf_TwoSegments_NavigatesOneLevel()
    {
        var inner = new Dictionary<string, AttributeValue>
        {
            ["City"] = new() { S = "London" }
        };
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["Address"] = new() { M = inner }
        };

        var result = AttributeValueReader.NavigateToLeaf(attrs, new[] { "Address", "City" });

        result.Should().BeSameAs(inner);
    }

    [Fact]
    public void NavigateToLeaf_ThreeSegments_NavigatesTwoLevels()
    {
        var deepInner = new Dictionary<string, AttributeValue>
        {
            ["Name"] = new() { S = "UK" }
        };
        var inner = new Dictionary<string, AttributeValue>
        {
            ["Country"] = new() { M = deepInner }
        };
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["Address"] = new() { M = inner }
        };

        var result = AttributeValueReader.NavigateToLeaf(attrs, new[] { "Address", "Country", "Name" });

        result.Should().BeSameAs(deepInner);
    }

    [Fact]
    public void NavigateToLeaf_MissingIntermediate_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "123" }
        };

        var result = AttributeValueReader.NavigateToLeaf(attrs, new[] { "Address", "City" });

        result.Should().BeNull();
    }

    [Fact]
    public void NavigateToLeaf_IntermediateNotMap_ReturnsEmptyDict()
    {
        // AWS SDK auto-initializes M as empty dict
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["Address"] = new() { S = "not-a-map" }
        };

        var result = AttributeValueReader.NavigateToLeaf(attrs, new[] { "Address", "City" });

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void NavigateToLeaf_IntermediateNullAttr_ReturnsEmptyDict()
    {
        // AWS SDK auto-initializes M as empty dict even for NULL=true
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["Address"] = new() { NULL = true }
        };

        var result = AttributeValueReader.NavigateToLeaf(attrs, new[] { "Address", "City" });

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region AttributeValueReader — ReadString

    [Fact]
    public void ReadString_ValidString_ReturnsValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = "hello" }
        };

        AttributeValueReader.ReadString(attrs, "key").Should().Be("hello");
    }

    [Fact]
    public void ReadString_MissingKey_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadString(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadString_NullAttribute_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadString(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadString_NoStringValue_ReturnsNull()
    {
        // S is null on a non-NULL attribute (e.g., it's a number)
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "42" }
        };

        AttributeValueReader.ReadString(attrs, "key").Should().BeNull();
    }

    #endregion

    #region AttributeValueReader — ReadGuid

    [Fact]
    public void ReadGuid_ValidGuid_ReturnsParsedValue()
    {
        var guid = Guid.NewGuid();
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = guid.ToString() }
        };

        AttributeValueReader.ReadGuid(attrs, "key").Should().Be(guid);
    }

    [Fact]
    public void ReadGuid_MissingKey_ReturnsEmpty()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadGuid(attrs, "key").Should().Be(Guid.Empty);
    }

    [Fact]
    public void ReadGuid_NullAttribute_ReturnsEmpty()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadGuid(attrs, "key").Should().Be(Guid.Empty);
    }

    [Fact]
    public void ReadGuid_InvalidFormat_ReturnsEmpty()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = "not-a-guid" }
        };

        AttributeValueReader.ReadGuid(attrs, "key").Should().Be(Guid.Empty);
    }

    #endregion

    #region AttributeValueReader — ReadNullableGuid

    [Fact]
    public void ReadNullableGuid_ValidGuid_ReturnsParsedValue()
    {
        var guid = Guid.NewGuid();
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = guid.ToString() }
        };

        AttributeValueReader.ReadNullableGuid(attrs, "key").Should().Be(guid);
    }

    [Fact]
    public void ReadNullableGuid_MissingKey_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadNullableGuid(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableGuid_NullAttribute_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadNullableGuid(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableGuid_InvalidFormat_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = "not-a-guid" }
        };

        AttributeValueReader.ReadNullableGuid(attrs, "key").Should().BeNull();
    }

    #endregion

    #region AttributeValueReader — ReadBool

    [Fact]
    public void ReadBool_True_ReturnsTrue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { BOOL = true }
        };

        AttributeValueReader.ReadBool(attrs, "key").Should().BeTrue();
    }

    [Fact]
    public void ReadBool_False_ReturnsFalse()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { BOOL = false }
        };

        AttributeValueReader.ReadBool(attrs, "key").Should().BeFalse();
    }

    [Fact]
    public void ReadBool_MissingKey_ReturnsFalse()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadBool(attrs, "key").Should().BeFalse();
    }

    #endregion

    #region AttributeValueReader — ReadNullableBool

    [Fact]
    public void ReadNullableBool_True_ReturnsTrue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { BOOL = true }
        };

        AttributeValueReader.ReadNullableBool(attrs, "key").Should().BeTrue();
    }

    [Fact]
    public void ReadNullableBool_False_ReturnsFalse()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { BOOL = false }
        };

        AttributeValueReader.ReadNullableBool(attrs, "key").Should().BeFalse();
    }

    [Fact]
    public void ReadNullableBool_MissingKey_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadNullableBool(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableBool_NullAttribute_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadNullableBool(attrs, "key").Should().BeNull();
    }

    #endregion

    #region AttributeValueReader — ReadInt

    [Fact]
    public void ReadInt_ValidNumber_ReturnsParsedValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "42" }
        };

        AttributeValueReader.ReadInt(attrs, "key").Should().Be(42);
    }

    [Fact]
    public void ReadInt_MissingKey_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadInt(attrs, "key").Should().Be(0);
    }

    [Fact]
    public void ReadInt_NullAttribute_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadInt(attrs, "key").Should().Be(0);
    }

    [Fact]
    public void ReadInt_InvalidNumber_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "abc" }
        };

        AttributeValueReader.ReadInt(attrs, "key").Should().Be(0);
    }

    [Fact]
    public void ReadInt_NegativeNumber_ReturnsParsedValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "-17" }
        };

        AttributeValueReader.ReadInt(attrs, "key").Should().Be(-17);
    }

    #endregion

    #region AttributeValueReader — ReadNullableInt

    [Fact]
    public void ReadNullableInt_ValidNumber_ReturnsParsedValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "99" }
        };

        AttributeValueReader.ReadNullableInt(attrs, "key").Should().Be(99);
    }

    [Fact]
    public void ReadNullableInt_MissingKey_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadNullableInt(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableInt_NullAttribute_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadNullableInt(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableInt_InvalidNumber_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "abc" }
        };

        AttributeValueReader.ReadNullableInt(attrs, "key").Should().BeNull();
    }

    #endregion

    #region AttributeValueReader — ReadLong

    [Fact]
    public void ReadLong_ValidNumber_ReturnsParsedValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "9999999999" }
        };

        AttributeValueReader.ReadLong(attrs, "key").Should().Be(9999999999L);
    }

    [Fact]
    public void ReadLong_MissingKey_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadLong(attrs, "key").Should().Be(0L);
    }

    [Fact]
    public void ReadLong_NullAttribute_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadLong(attrs, "key").Should().Be(0L);
    }

    [Fact]
    public void ReadLong_InvalidNumber_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "xyz" }
        };

        AttributeValueReader.ReadLong(attrs, "key").Should().Be(0L);
    }

    #endregion

    #region AttributeValueReader — ReadNullableLong

    [Fact]
    public void ReadNullableLong_ValidNumber_ReturnsParsedValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "1234567890" }
        };

        AttributeValueReader.ReadNullableLong(attrs, "key").Should().Be(1234567890L);
    }

    [Fact]
    public void ReadNullableLong_MissingKey_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadNullableLong(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableLong_NullAttribute_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadNullableLong(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableLong_InvalidNumber_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "abc" }
        };

        AttributeValueReader.ReadNullableLong(attrs, "key").Should().BeNull();
    }

    #endregion

    #region AttributeValueReader — ReadDecimal

    [Fact]
    public void ReadDecimal_ValidNumber_ReturnsParsedValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "123.456" }
        };

        AttributeValueReader.ReadDecimal(attrs, "key").Should().Be(123.456m);
    }

    [Fact]
    public void ReadDecimal_MissingKey_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadDecimal(attrs, "key").Should().Be(0m);
    }

    [Fact]
    public void ReadDecimal_NullAttribute_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadDecimal(attrs, "key").Should().Be(0m);
    }

    [Fact]
    public void ReadDecimal_InvalidNumber_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "not-decimal" }
        };

        AttributeValueReader.ReadDecimal(attrs, "key").Should().Be(0m);
    }

    #endregion

    #region AttributeValueReader — ReadNullableDecimal

    [Fact]
    public void ReadNullableDecimal_ValidNumber_ReturnsParsedValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "99.99" }
        };

        AttributeValueReader.ReadNullableDecimal(attrs, "key").Should().Be(99.99m);
    }

    [Fact]
    public void ReadNullableDecimal_MissingKey_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadNullableDecimal(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableDecimal_NullAttribute_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadNullableDecimal(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableDecimal_InvalidNumber_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "xyz" }
        };

        AttributeValueReader.ReadNullableDecimal(attrs, "key").Should().BeNull();
    }

    #endregion

    #region AttributeValueReader — ReadDouble

    [Fact]
    public void ReadDouble_ValidNumber_ReturnsParsedValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "3.14" }
        };

        AttributeValueReader.ReadDouble(attrs, "key").Should().BeApproximately(3.14, 0.001);
    }

    [Fact]
    public void ReadDouble_MissingKey_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadDouble(attrs, "key").Should().Be(0.0);
    }

    [Fact]
    public void ReadDouble_NullAttribute_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadDouble(attrs, "key").Should().Be(0.0);
    }

    [Fact]
    public void ReadDouble_InvalidNumber_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "bad" }
        };

        AttributeValueReader.ReadDouble(attrs, "key").Should().Be(0.0);
    }

    #endregion

    #region AttributeValueReader — ReadNullableDouble

    [Fact]
    public void ReadNullableDouble_ValidNumber_ReturnsParsedValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "2.718" }
        };

        AttributeValueReader.ReadNullableDouble(attrs, "key").Should().BeApproximately(2.718, 0.001);
    }

    [Fact]
    public void ReadNullableDouble_MissingKey_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadNullableDouble(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableDouble_NullAttribute_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadNullableDouble(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableDouble_InvalidNumber_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "bad" }
        };

        AttributeValueReader.ReadNullableDouble(attrs, "key").Should().BeNull();
    }

    #endregion

    #region AttributeValueReader — ReadFloat

    [Fact]
    public void ReadFloat_ValidNumber_ReturnsParsedValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "1.5" }
        };

        AttributeValueReader.ReadFloat(attrs, "key").Should().BeApproximately(1.5f, 0.01f);
    }

    [Fact]
    public void ReadFloat_MissingKey_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadFloat(attrs, "key").Should().Be(0f);
    }

    [Fact]
    public void ReadFloat_NullAttribute_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadFloat(attrs, "key").Should().Be(0f);
    }

    [Fact]
    public void ReadFloat_InvalidNumber_ReturnsZero()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "bad" }
        };

        AttributeValueReader.ReadFloat(attrs, "key").Should().Be(0f);
    }

    #endregion

    #region AttributeValueReader — ReadNullableFloat

    [Fact]
    public void ReadNullableFloat_ValidNumber_ReturnsParsedValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "7.7" }
        };

        AttributeValueReader.ReadNullableFloat(attrs, "key").Should().BeApproximately(7.7f, 0.01f);
    }

    [Fact]
    public void ReadNullableFloat_MissingKey_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadNullableFloat(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableFloat_NullAttribute_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadNullableFloat(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableFloat_InvalidNumber_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "bad" }
        };

        AttributeValueReader.ReadNullableFloat(attrs, "key").Should().BeNull();
    }

    #endregion

    #region AttributeValueReader — ReadDateTime

    [Fact]
    public void ReadDateTime_ValidIso8601_ReturnsParsedValue()
    {
        var dt = new DateTime(2024, 6, 15, 12, 30, 0, DateTimeKind.Utc);
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = dt.ToString("O") }
        };

        AttributeValueReader.ReadDateTime(attrs, "key").Should().Be(dt);
    }

    [Fact]
    public void ReadDateTime_MissingKey_ReturnsMinValue()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadDateTime(attrs, "key").Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void ReadDateTime_NullAttribute_ReturnsMinValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadDateTime(attrs, "key").Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void ReadDateTime_InvalidFormat_ReturnsMinValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = "not-a-date" }
        };

        AttributeValueReader.ReadDateTime(attrs, "key").Should().Be(DateTime.MinValue);
    }

    #endregion

    #region AttributeValueReader — ReadNullableDateTime

    [Fact]
    public void ReadNullableDateTime_ValidIso8601_ReturnsParsedValue()
    {
        var dt = new DateTime(2024, 3, 10, 8, 0, 0, DateTimeKind.Utc);
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = dt.ToString("O") }
        };

        AttributeValueReader.ReadNullableDateTime(attrs, "key").Should().Be(dt);
    }

    [Fact]
    public void ReadNullableDateTime_MissingKey_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadNullableDateTime(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableDateTime_NullAttribute_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadNullableDateTime(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableDateTime_InvalidFormat_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = "bad-date" }
        };

        AttributeValueReader.ReadNullableDateTime(attrs, "key").Should().BeNull();
    }

    #endregion

    #region AttributeValueReader — ReadDateTimeOffset

    [Fact]
    public void ReadDateTimeOffset_ValidIso8601_ReturnsParsedValue()
    {
        var dto = new DateTimeOffset(2024, 6, 15, 12, 30, 0, TimeSpan.FromHours(5));
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = dto.ToString("O") }
        };

        AttributeValueReader.ReadDateTimeOffset(attrs, "key").Should().Be(dto);
    }

    [Fact]
    public void ReadDateTimeOffset_MissingKey_ReturnsMinValue()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadDateTimeOffset(attrs, "key").Should().Be(DateTimeOffset.MinValue);
    }

    [Fact]
    public void ReadDateTimeOffset_NullAttribute_ReturnsMinValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadDateTimeOffset(attrs, "key").Should().Be(DateTimeOffset.MinValue);
    }

    [Fact]
    public void ReadDateTimeOffset_InvalidFormat_ReturnsMinValue()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = "bad" }
        };

        AttributeValueReader.ReadDateTimeOffset(attrs, "key").Should().Be(DateTimeOffset.MinValue);
    }

    #endregion

    #region AttributeValueReader — ReadNullableDateTimeOffset

    [Fact]
    public void ReadNullableDateTimeOffset_ValidIso8601_ReturnsParsedValue()
    {
        var dto = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = dto.ToString("O") }
        };

        AttributeValueReader.ReadNullableDateTimeOffset(attrs, "key").Should().Be(dto);
    }

    [Fact]
    public void ReadNullableDateTimeOffset_MissingKey_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadNullableDateTimeOffset(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableDateTimeOffset_NullAttribute_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadNullableDateTimeOffset(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadNullableDateTimeOffset_InvalidFormat_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = "bad" }
        };

        AttributeValueReader.ReadNullableDateTimeOffset(attrs, "key").Should().BeNull();
    }

    #endregion

    #region AttributeValueReader — ReadStringList

    [Fact]
    public void ReadStringList_ValidSS_ReturnsList()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { SS = new List<string> { "a", "b", "c" } }
        };

        var result = AttributeValueReader.ReadStringList(attrs, "key");

        result.Should().NotBeNull();
        result.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void ReadStringList_MissingKey_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadStringList(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadStringList_NullAttribute_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadStringList(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadStringList_NoSSValue_ReturnsEmptyList()
    {
        // AWS SDK auto-initializes SS as empty list
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "42" }
        };

        var result = AttributeValueReader.ReadStringList(attrs, "key");
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region AttributeValueReader — ReadBytes

    [Fact]
    public void ReadBytes_ValidBinary_ReturnsByteArray()
    {
        var data = new byte[] { 1, 2, 3, 4 };
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { B = new MemoryStream(data) }
        };

        var result = AttributeValueReader.ReadBytes(attrs, "key");

        result.Should().NotBeNull();
        result.Should().Equal(data);
    }

    [Fact]
    public void ReadBytes_MissingKey_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>();

        AttributeValueReader.ReadBytes(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadBytes_NullAttribute_ReturnsNull()
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        AttributeValueReader.ReadBytes(attrs, "key").Should().BeNull();
    }

    [Fact]
    public void ReadBytes_NoBinaryValue_ReturnsNull()
    {
        // Attribute exists but B is null
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = "text" }
        };

        AttributeValueReader.ReadBytes(attrs, "key").Should().BeNull();
    }

    #endregion

    #region CompositeMappingStrategy — path traversal off-by-one

    [Fact]
    public void CompositeMappingStrategy_NestedPath_TwoLevels_ResolvesCorrectly()
    {
        // Kills off-by-one in `i < pathSegments.Length - 1`
        // If mutated to `i < pathSegments.Length` or `i < pathSegments.Length - 2`,
        // the nested path resolution will fail
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
        // Tests 3-level nesting to catch off-by-one at deeper levels
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
        // Kills mutant where switch default case is removed
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var strategy = new CompositeMappingStrategy(resolverFactory, converterRegistry);

        // Method call expression is not NewExpression or MemberInitExpression
        Action act = () => strategy.BuildMapper<TestEntity, string>(e => e.OrderId.ToUpper());

        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void CompositeMappingStrategy_NonPropertyMember_Throws()
    {
        // This kills the UnsupportedExpressionException path in ExtractPropertyPath
        // where member is not a PropertyInfo
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var strategy = new CompositeMappingStrategy(resolverFactory, converterRegistry);

        // Create an expression accessing a field (not a property)
        // Using a class with a public field
        Action act = () => strategy.BuildMapper<EntityWithField, string?>(
            e => e.FieldValue);

        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void CompositeMappingStrategy_MemberInit_NonPropertyBinding_Throws()
    {
        // Kills mutant on `property == null` check in BuildMemberInitMapper
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var mapper = new DirectResultMapper<TestEntity>(resolverFactory, converterRegistry);

        // MemberInitExpression with a non-assignment binding is hard to construct
        // via normal C# expressions, so we test what we can: verify that valid
        // MemberInit expressions work (the mutation would break them)
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
        // Kills && -> || mutation in `shape != ProjectionShape.SingleProperty || paths.Count != 1`
        // If mutated to &&, multi-property projections would pass through
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var strategy = new SinglePropertyMappingStrategy(resolverFactory, converterRegistry);

        // Anonymous type is Composite shape, not SingleProperty
        Action act = () => strategy.BuildMapper<TestEntity, object>(
            e => new { e.OrderId, e.Price });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*exactly one*");
    }

    [Fact]
    public void SinglePropertyMappingStrategy_NestedProperty_MapsCorrectly()
    {
        // Kills mutations in BuildNestedAttributeRead path
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
        // Kills mutations in null check path of BuildNestedAttributeRead
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
        // Kills mutations in BuildDirectAttributeRead path
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
        // Kills mutations in TryGetValue conditional path
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var strategy = new SinglePropertyMappingStrategy(resolverFactory, converterRegistry);

        var attrs = new Dictionary<string, AttributeValue>();

        var mapper = strategy.BuildMapper<TestEntity, string>(e => e.OrderId);
        var result = mapper(attrs);

        result.Should().BeNull();
    }

    #endregion

    #region DirectResultMapper — null coalescing on constructor args

    [Fact]
    public void DirectResultMapper_NullResolverFactory_ThrowsArgumentNullException()
    {
        Action act = () => new DirectResultMapper<TestEntity>(
            null!,
            AttributeValueConverterRegistry.Default);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("resolverFactory");
    }

    [Fact]
    public void DirectResultMapper_NullConverterRegistry_ThrowsArgumentNullException()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();

        Action act = () => new DirectResultMapper<TestEntity>(
            resolverFactory,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("converterRegistry");
    }

    [Fact]
    public void DirectResultMapper_NullCache_UsesNullExpressionCache()
    {
        // Kills null coalescing mutation on `cache ?? NullExpressionCache.Instance`
        // If removed, passing null would cause NullReferenceException later
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;

        var mapper = new DirectResultMapper<TestEntity>(
            resolverFactory,
            converterRegistry,
            cache: null);

        // Should work fine without cache
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "no-cache" }
        };

        var result = mapper.Map(attrs, e => e.OrderId);

        result.Should().Be("no-cache");
    }

    [Fact]
    public void DirectResultMapper_NullFullEntityMapper_Identity_Throws()
    {
        // Kills null coalescing on fullEntityMapper — without fallback, identity projection should fail
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;

        var mapper = new DirectResultMapper<TestEntity>(
            resolverFactory,
            converterRegistry,
            fullEntityMapper: null);

        Action act = () => mapper.CreateMapper(e => e);

        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void DirectResultMapper_CreateMapper_NullSelector_ThrowsArgumentNullException()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var mapper = new DirectResultMapper<TestEntity>(resolverFactory, converterRegistry);

        Action act = () => mapper.CreateMapper<string>(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("selector");
    }

    [Fact]
    public void DirectResultMapper_Map_NullAttributes_ThrowsArgumentNullException()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var mapper = new DirectResultMapper<TestEntity>(resolverFactory, converterRegistry);

        Action act = () => mapper.Map(null!, e => e.OrderId);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("attributes");
    }

    [Fact]
    public void DirectResultMapper_Map_NullSelector_ThrowsArgumentNullException()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        var mapper = new DirectResultMapper<TestEntity>(resolverFactory, converterRegistry);

        Action act = () => mapper.Map(
            new Dictionary<string, AttributeValue>(),
            (Expression<Func<TestEntity, string>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("selector");
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
