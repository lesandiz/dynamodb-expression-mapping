using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.ResultMapping;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.ResultMapping;

/// <summary>
/// Tests for AttributeValueReader static methods.
/// Numeric readers are consolidated into [Theory] methods; distinct reader types remain as [Fact].
/// Migrated from P3MutationKillingTests as part of test suite refactoring (Phase 3c).
/// </summary>
public class AttributeValueReaderTests
{
    #region NavigateToLeaf

    [Fact]
    public void NavigateToLeaf_SingleSegment_ReturnsInputDictionary()
    {
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
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["Address"] = new() { NULL = true }
        };

        var result = AttributeValueReader.NavigateToLeaf(attrs, new[] { "Address", "City" });

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region ReadString

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
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "42" }
        };

        AttributeValueReader.ReadString(attrs, "key").Should().BeNull();
    }

    #endregion

    #region ReadGuid

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

    #region ReadNullableGuid

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

    #region ReadBool

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

    #region ReadNullableBool

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

    #region Numeric Readers — Consolidated [Theory] Tests

    [Theory]
    [InlineData("42", 42)]
    [InlineData("9999999999", 9999999999L)]
    [InlineData("123.456", 123.456)]
    public void NumericReader_ValidValue_ReturnsParsed(string input, object expected)
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = input }
        };

        if (expected is int expectedInt)
            AttributeValueReader.ReadInt(attrs, "key").Should().Be(expectedInt);
        else if (expected is long expectedLong)
            AttributeValueReader.ReadLong(attrs, "key").Should().Be(expectedLong);
        else if (expected is double expectedDouble)
            AttributeValueReader.ReadDecimal(attrs, "key").Should().Be((decimal)expectedDouble);
    }

    [Theory]
    [InlineData("3.14", 3.14)]
    [InlineData("1.5", 1.5)]
    public void FloatingPointReader_ValidValue_ReturnsParsed(string input, double expected)
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = input }
        };

        if (expected == 3.14)
            AttributeValueReader.ReadDouble(attrs, "key").Should().BeApproximately(expected, 0.001);
        else
            AttributeValueReader.ReadFloat(attrs, "key").Should().BeApproximately((float)expected, 0.01f);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("long")]
    [InlineData("decimal")]
    [InlineData("double")]
    [InlineData("float")]
    public void NumericReader_MissingKey_ReturnsDefault(string type)
    {
        var attrs = new Dictionary<string, AttributeValue>();

        switch (type)
        {
            case "int": AttributeValueReader.ReadInt(attrs, "key").Should().Be(0); break;
            case "long": AttributeValueReader.ReadLong(attrs, "key").Should().Be(0L); break;
            case "decimal": AttributeValueReader.ReadDecimal(attrs, "key").Should().Be(0m); break;
            case "double": AttributeValueReader.ReadDouble(attrs, "key").Should().Be(0.0); break;
            case "float": AttributeValueReader.ReadFloat(attrs, "key").Should().Be(0f); break;
        }
    }

    [Theory]
    [InlineData("int")]
    [InlineData("long")]
    [InlineData("decimal")]
    [InlineData("double")]
    [InlineData("float")]
    public void NumericReader_NullAttribute_ReturnsDefault(string type)
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        switch (type)
        {
            case "int": AttributeValueReader.ReadInt(attrs, "key").Should().Be(0); break;
            case "long": AttributeValueReader.ReadLong(attrs, "key").Should().Be(0L); break;
            case "decimal": AttributeValueReader.ReadDecimal(attrs, "key").Should().Be(0m); break;
            case "double": AttributeValueReader.ReadDouble(attrs, "key").Should().Be(0.0); break;
            case "float": AttributeValueReader.ReadFloat(attrs, "key").Should().Be(0f); break;
        }
    }

    [Theory]
    [InlineData("int", "abc")]
    [InlineData("long", "xyz")]
    [InlineData("decimal", "not-decimal")]
    [InlineData("double", "bad")]
    [InlineData("float", "bad")]
    public void NumericReader_InvalidFormat_ReturnsDefault(string type, string invalidValue)
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = invalidValue }
        };

        switch (type)
        {
            case "int": AttributeValueReader.ReadInt(attrs, "key").Should().Be(0); break;
            case "long": AttributeValueReader.ReadLong(attrs, "key").Should().Be(0L); break;
            case "decimal": AttributeValueReader.ReadDecimal(attrs, "key").Should().Be(0m); break;
            case "double": AttributeValueReader.ReadDouble(attrs, "key").Should().Be(0.0); break;
            case "float": AttributeValueReader.ReadFloat(attrs, "key").Should().Be(0f); break;
        }
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

    [Theory]
    [InlineData("int", "99")]
    [InlineData("long", "1234567890")]
    [InlineData("decimal", "99.99")]
    [InlineData("double", "2.718")]
    [InlineData("float", "7.7")]
    public void NullableNumericReader_ValidValue_ReturnsParsed(string type, string input)
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = input }
        };

        switch (type)
        {
            case "int": AttributeValueReader.ReadNullableInt(attrs, "key").Should().Be(99); break;
            case "long": AttributeValueReader.ReadNullableLong(attrs, "key").Should().Be(1234567890L); break;
            case "decimal": AttributeValueReader.ReadNullableDecimal(attrs, "key").Should().Be(99.99m); break;
            case "double": AttributeValueReader.ReadNullableDouble(attrs, "key").Should().BeApproximately(2.718, 0.001); break;
            case "float": AttributeValueReader.ReadNullableFloat(attrs, "key").Should().BeApproximately(7.7f, 0.01f); break;
        }
    }

    [Theory]
    [InlineData("int")]
    [InlineData("long")]
    [InlineData("decimal")]
    [InlineData("double")]
    [InlineData("float")]
    public void NullableNumericReader_MissingKey_ReturnsNull(string type)
    {
        var attrs = new Dictionary<string, AttributeValue>();

        switch (type)
        {
            case "int": AttributeValueReader.ReadNullableInt(attrs, "key").Should().BeNull(); break;
            case "long": AttributeValueReader.ReadNullableLong(attrs, "key").Should().BeNull(); break;
            case "decimal": AttributeValueReader.ReadNullableDecimal(attrs, "key").Should().BeNull(); break;
            case "double": AttributeValueReader.ReadNullableDouble(attrs, "key").Should().BeNull(); break;
            case "float": AttributeValueReader.ReadNullableFloat(attrs, "key").Should().BeNull(); break;
        }
    }

    [Theory]
    [InlineData("int")]
    [InlineData("long")]
    [InlineData("decimal")]
    [InlineData("double")]
    [InlineData("float")]
    public void NullableNumericReader_NullAttribute_ReturnsNull(string type)
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { NULL = true }
        };

        switch (type)
        {
            case "int": AttributeValueReader.ReadNullableInt(attrs, "key").Should().BeNull(); break;
            case "long": AttributeValueReader.ReadNullableLong(attrs, "key").Should().BeNull(); break;
            case "decimal": AttributeValueReader.ReadNullableDecimal(attrs, "key").Should().BeNull(); break;
            case "double": AttributeValueReader.ReadNullableDouble(attrs, "key").Should().BeNull(); break;
            case "float": AttributeValueReader.ReadNullableFloat(attrs, "key").Should().BeNull(); break;
        }
    }

    [Theory]
    [InlineData("int", "abc")]
    [InlineData("long", "abc")]
    [InlineData("decimal", "xyz")]
    [InlineData("double", "bad")]
    [InlineData("float", "bad")]
    public void NullableNumericReader_InvalidFormat_ReturnsNull(string type, string invalidValue)
    {
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = invalidValue }
        };

        switch (type)
        {
            case "int": AttributeValueReader.ReadNullableInt(attrs, "key").Should().BeNull(); break;
            case "long": AttributeValueReader.ReadNullableLong(attrs, "key").Should().BeNull(); break;
            case "decimal": AttributeValueReader.ReadNullableDecimal(attrs, "key").Should().BeNull(); break;
            case "double": AttributeValueReader.ReadNullableDouble(attrs, "key").Should().BeNull(); break;
            case "float": AttributeValueReader.ReadNullableFloat(attrs, "key").Should().BeNull(); break;
        }
    }

    #endregion

    #region ReadDateTime

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

    #region ReadNullableDateTime

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

    #region ReadDateTimeOffset

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

    #region ReadNullableDateTimeOffset

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

    #region ReadStringList

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
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { N = "42" }
        };

        var result = AttributeValueReader.ReadStringList(attrs, "key");
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region ReadBytes

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
        var attrs = new Dictionary<string, AttributeValue>
        {
            ["key"] = new() { S = "text" }
        };

        AttributeValueReader.ReadBytes(attrs, "key").Should().BeNull();
    }

    #endregion
}
