using Amazon.DynamoDBv2.Model;
using Bogus;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Mapping.Converters;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Mapping;

/// <summary>
/// Comprehensive test suite for Spec 05: Type Converter System - Built-in Converters.
/// Tests all built-in converters for round-trip conversion, null handling, and format compliance.
/// </summary>
public class AttributeValueConverterTests
{
    private readonly Faker _faker = new();

    #region String Converter Tests

    [Fact]
    public void String_RoundTrip_PreservesValue()
    {
        var converter = new StringConverter();
        var value = _faker.Lorem.Sentence();

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(value);
        attributeValue.S.Should().Be(value);
    }

    [Fact]
    public void String_FromNull_ReturnsNull()
    {
        var converter = new StringConverter();

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().BeNull();
    }

    [Fact]
    public void String_ToNull_WritesNULLAttribute()
    {
        var converter = new StringConverter();

        var attributeValue = converter.ToAttributeValue(null!);

        attributeValue.NULL.Should().BeTrue();
    }

    #endregion

    #region Guid Converter Tests

    [Fact]
    public void Guid_RoundTrip_PreservesValue()
    {
        var converter = new GuidConverter();
        var value = Guid.NewGuid();

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(value);
        attributeValue.S.Should().Be(value.ToString());
    }

    [Fact]
    public void Guid_FromNull_ReturnsGuidEmpty()
    {
        var converter = new GuidConverter();

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Guid_FromMissing_ReturnsGuidEmpty()
    {
        var converter = new GuidConverter();

        var result = converter.FromAttributeValue(new AttributeValue());

        result.Should().Be(Guid.Empty);
    }

    #endregion

    #region Bool Converter Tests

    [Fact]
    public void Bool_RoundTrip_PreservesValue()
    {
        var converter = new BoolConverter();
        var value = _faker.Random.Bool();

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(value);
        attributeValue.BOOL.Should().Be(value);
    }

    [Fact]
    public void Bool_FromMissing_ReturnsFalse()
    {
        var converter = new BoolConverter();

        var result = converter.FromAttributeValue(new AttributeValue());

        result.Should().BeFalse();
    }

    [Fact]
    public void Bool_FromNull_ReturnsFalse()
    {
        var converter = new BoolConverter();

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().BeFalse();
    }

    #endregion

    #region Int32 Converter Tests

    [Fact]
    public void Int_RoundTrip_PreservesValue()
    {
        var converter = new Int32Converter();
        var value = _faker.Random.Int();

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(value);
        attributeValue.N.Should().Be(value.ToString());
    }

    [Fact]
    public void Int_FromNull_ReturnsZero()
    {
        var converter = new Int32Converter();

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().Be(0);
    }

    [Fact]
    public void Int_FromMissing_ReturnsZero()
    {
        var converter = new Int32Converter();

        var result = converter.FromAttributeValue(new AttributeValue());

        result.Should().Be(0);
    }

    #endregion

    #region Int64 Converter Tests

    [Fact]
    public void Long_RoundTrip_PreservesValue()
    {
        var converter = new Int64Converter();
        var value = _faker.Random.Long();

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(value);
        attributeValue.N.Should().Be(value.ToString());
    }

    [Fact]
    public void Long_FromNull_ReturnsZero()
    {
        var converter = new Int64Converter();

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().Be(0L);
    }

    #endregion

    #region Decimal Converter Tests

    [Fact]
    public void Decimal_RoundTrip_PreservesValue()
    {
        var converter = new DecimalConverter();
        var value = _faker.Random.Decimal(min: -999999.99m, max: 999999.99m);

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(value);
        attributeValue.N.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Decimal_FromNull_ReturnsZero()
    {
        var converter = new DecimalConverter();

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().Be(0m);
    }

    #endregion

    #region Double Converter Tests

    [Fact]
    public void Double_RoundTrip_PreservesValue()
    {
        var converter = new DoubleConverter();
        var value = _faker.Random.Double(min: -999999.99, max: 999999.99);

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().BeApproximately(value, 0.0001);
        attributeValue.N.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Double_FromNull_ReturnsZero()
    {
        var converter = new DoubleConverter();

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().Be(0.0);
    }

    #endregion

    #region DateTime Converter Tests

    [Fact]
    public void DateTime_RoundTrip_PreservesValue()
    {
        var converter = new DateTimeConverter();
        var value = _faker.Date.Recent();

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(value);
    }

    [Fact]
    public void DateTime_ToAttributeValue_WritesIso8601RoundtripFormat()
    {
        var converter = new DateTimeConverter();
        var value = new DateTime(2024, 1, 15, 14, 30, 45, DateTimeKind.Utc);

        var attributeValue = converter.ToAttributeValue(value);

        attributeValue.S.Should().Be("2024-01-15T14:30:45.0000000Z");
    }

    [Fact]
    public void DateTime_FromNull_ReturnsMinValue()
    {
        var converter = new DateTimeConverter();

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void DateTime_FromMissing_ReturnsMinValue()
    {
        var converter = new DateTimeConverter();

        var result = converter.FromAttributeValue(new AttributeValue());

        result.Should().Be(DateTime.MinValue);
    }

    #endregion

    #region DateTimeOffset Converter Tests

    [Fact]
    public void DateTimeOffset_RoundTrip_PreservesValue()
    {
        var converter = new DateTimeOffsetConverter();
        var value = _faker.Date.RecentOffset();

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(value);
    }

    [Fact]
    public void DateTimeOffset_ToAttributeValue_WritesIso8601RoundtripFormat()
    {
        var converter = new DateTimeOffsetConverter();
        var value = new DateTimeOffset(2024, 1, 15, 14, 30, 45, TimeSpan.FromHours(5));

        var attributeValue = converter.ToAttributeValue(value);

        attributeValue.S.Should().Be("2024-01-15T14:30:45.0000000+05:00");
    }

    [Fact]
    public void DateTimeOffset_FromNull_ReturnsMinValue()
    {
        var converter = new DateTimeOffsetConverter();

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().Be(DateTimeOffset.MinValue);
    }

    #endregion

    #region ByteArray Converter Tests

    [Fact]
    public void ByteArray_RoundTrip_PreservesValue()
    {
        var converter = new ByteArrayConverter();
        var value = _faker.Random.Bytes(100);

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Equal(value);
    }

    [Fact]
    public void ByteArray_FromNull_ReturnsNull()
    {
        var converter = new ByteArrayConverter();

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().BeNull();
    }

    [Fact]
    public void ByteArray_FromMissing_ReturnsNull()
    {
        var converter = new ByteArrayConverter();

        var result = converter.FromAttributeValue(new AttributeValue());

        result.Should().BeNull();
    }

    #endregion

    #region List Converter Tests

    [Fact]
    public void ListOfString_RoundTrip_PreservesValue()
    {
        var converter = new ListConverter<string>(new StringConverter());
        var value = _faker.Lorem.Words().ToList();

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Equal(value);
        attributeValue.L.Should().HaveCount(value.Count);
    }

    [Fact]
    public void ListOfInt_RoundTrip_PreservesValue()
    {
        var converter = new ListConverter<int>(new Int32Converter());
        var value = Enumerable.Range(1, 5).Select(_ => _faker.Random.Int()).ToList();

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Equal(value);
    }

    [Fact]
    public void ListOfString_FromMissing_ReturnsEmptyList()
    {
        var converter = new ListConverter<string>(new StringConverter());

        var result = converter.FromAttributeValue(new AttributeValue());

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ListOfString_FromNull_ReturnsEmptyList()
    {
        var converter = new ListConverter<string>(new StringConverter());

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region HashSet Converter Tests

    [Fact]
    public void HashSetOfString_RoundTrip_PreservesValue()
    {
        var converter = new SetConverter<string>(new StringConverter());
        var value = _faker.Lorem.Words(5).ToHashSet();

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().BeEquivalentTo(value);
        attributeValue.SS.Should().HaveCount(value.Count);
    }

    [Fact]
    public void HashSetOfString_FromMissing_ReturnsEmptySet()
    {
        var converter = new SetConverter<string>(new StringConverter());

        var result = converter.FromAttributeValue(new AttributeValue());

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void HashSetOfString_FromNull_ReturnsEmptySet()
    {
        var converter = new SetConverter<string>(new StringConverter());

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region Dictionary Converter Tests

    [Fact]
    public void DictionaryOfStringString_RoundTrip_PreservesValue()
    {
        var converter = new MapConverter<string>(new StringConverter());
        var value = new Dictionary<string, string>
        {
            ["key1"] = _faker.Lorem.Word(),
            ["key2"] = _faker.Lorem.Word(),
            ["key3"] = _faker.Lorem.Word()
        };

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().BeEquivalentTo(value);
        attributeValue.M.Should().HaveCount(value.Count);
    }

    [Fact]
    public void DictionaryOfStringString_FromMissing_ReturnsEmptyDictionary()
    {
        var converter = new MapConverter<string>(new StringConverter());

        var result = converter.FromAttributeValue(new AttributeValue());

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region Nullable Wrapper Tests

    [Fact]
    public void NullableInt_FromNull_ReturnsNull()
    {
        var converter = new NullableConverter<int>(new Int32Converter());

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().BeNull();
    }

    [Fact]
    public void NullableInt_FromValue_ReturnsValue()
    {
        var converter = new NullableConverter<int>(new Int32Converter());
        var value = 42;

        var attributeValue = new AttributeValue { N = value.ToString() };
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(value);
    }

    [Fact]
    public void NullableGuid_FromNull_ReturnsNull()
    {
        var converter = new NullableConverter<Guid>(new GuidConverter());

        var result = converter.FromAttributeValue(new AttributeValue { NULL = true });

        result.Should().BeNull();
    }

    [Fact]
    public void NullableDateTime_FromValue_ReturnsValue()
    {
        var converter = new NullableConverter<DateTime>(new DateTimeConverter());
        var value = DateTime.UtcNow;

        var attributeValue = new AttributeValue { S = value.ToString("O") };
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(value);
    }

    [Fact]
    public void NullableInt_ToAttributeValue_NullWritesNULL()
    {
        var converter = new NullableConverter<int>(new Int32Converter());
        int? value = null;

        var attributeValue = converter.ToAttributeValue(value);

        attributeValue.NULL.Should().BeTrue();
    }

    [Fact]
    public void NullableInt_ToAttributeValue_ValueWritesN()
    {
        var converter = new NullableConverter<int>(new Int32Converter());
        int? value = 42;

        var attributeValue = converter.ToAttributeValue(value);

        attributeValue.N.Should().Be("42");
        attributeValue.NULL.Should().BeFalse();
    }

    #endregion

    #region Enum Converter Tests

    [Fact]
    public void Enum_StringMode_RoundTrip_PreservesValue()
    {
        var converter = new EnumConverter<TestStatus>(EnumStorageMode.String);
        var value = TestStatus.Active;

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(value);
        attributeValue.S.Should().Be("Active");
    }

    [Fact]
    public void Enum_NumberMode_RoundTrip_PreservesValue()
    {
        var converter = new EnumConverter<TestStatus>(EnumStorageMode.Number);
        var value = TestStatus.Inactive;

        var attributeValue = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(value);
        attributeValue.N.Should().Be("1");
    }

    [Fact]
    public void Enum_StringMode_ParsesIgnoreCase()
    {
        var converter = new EnumConverter<TestStatus>(EnumStorageMode.String);

        var attributeValue = new AttributeValue { S = "active" };
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(TestStatus.Active);
    }

    [Fact]
    public void Enum_StringMode_ParsesMixedCase()
    {
        var converter = new EnumConverter<TestStatus>(EnumStorageMode.String);

        var attributeValue = new AttributeValue { S = "AcTiVe" };
        var result = converter.FromAttributeValue(attributeValue);

        result.Should().Be(TestStatus.Active);
    }

    #endregion

    #region Test Helper Types

    public enum TestStatus
    {
        Active = 0,
        Inactive = 1,
        Pending = 2
    }

    #endregion
}
