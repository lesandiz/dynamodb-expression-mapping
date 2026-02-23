using Amazon.DynamoDBv2.Model;
using Bogus;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Mapping.Converters;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Mapping;

/// <summary>
/// Comprehensive test suite for Spec 05: Type Converter System - AttributeValueConverterRegistry.
/// Tests default registry, cloning, registration, resolution order, and open-generic collection resolution.
/// </summary>
public class ConverterRegistryTests
{
    private readonly Faker _faker = new();

    #region Default Registry Tests

    [Fact]
    public void Default_ContainsAllBuiltInConverters()
    {
        var registry = AttributeValueConverterRegistry.Default;

        // Primitive types
        registry.HasConverter(typeof(string)).Should().BeTrue();
        registry.HasConverter(typeof(Guid)).Should().BeTrue();
        registry.HasConverter(typeof(bool)).Should().BeTrue();
        registry.HasConverter(typeof(int)).Should().BeTrue();
        registry.HasConverter(typeof(long)).Should().BeTrue();
        registry.HasConverter(typeof(decimal)).Should().BeTrue();
        registry.HasConverter(typeof(double)).Should().BeTrue();
        registry.HasConverter(typeof(DateTime)).Should().BeTrue();
        registry.HasConverter(typeof(DateTimeOffset)).Should().BeTrue();
        registry.HasConverter(typeof(byte[])).Should().BeTrue();

        // Pre-registered collection converters
        registry.HasConverter(typeof(List<string>)).Should().BeTrue();
        registry.HasConverter(typeof(List<int>)).Should().BeTrue();
        registry.HasConverter(typeof(HashSet<string>)).Should().BeTrue();
        registry.HasConverter(typeof(Dictionary<string, string>)).Should().BeTrue();
    }

    [Fact]
    public void Default_IsFrozen_RegisterThrowsInvalidOperationException()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var act = () => registry.Register(new StringConverter());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*default converter registry*");
    }

    [Fact]
    public void HasConverter_ReturnsTrueForRegisteredType()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var result = registry.HasConverter(typeof(string));

        result.Should().BeTrue();
    }

    [Fact]
    public void HasConverter_ReturnsFalseForUnregisteredType()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var result = registry.HasConverter(typeof(Money));

        result.Should().BeFalse();
    }

    #endregion

    #region Clone Tests

    [Fact]
    public void Clone_CreatesMutableCopy()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var clone = registry.Clone();

        // Should not throw
        var act = () => clone.Register(new MoneyConverter());
        act.Should().NotThrow();
    }

    [Fact]
    public void Clone_MutationsDoNotAffectSource()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var clone = registry.Clone();

        clone.Register(new MoneyConverter());

        registry.HasConverter(typeof(Money)).Should().BeFalse();
        clone.HasConverter(typeof(Money)).Should().BeTrue();
    }

    [Fact]
    public void Clone_CopiesAllRegisteredConverters()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var clone = registry.Clone();

        clone.HasConverter(typeof(string)).Should().BeTrue();
        clone.HasConverter(typeof(int)).Should().BeTrue();
        clone.HasConverter(typeof(Guid)).Should().BeTrue();
    }

    #endregion

    #region Custom Registration Tests

    [Fact]
    public void Register_CustomConverter_OverridesExisting()
    {
        var registry = AttributeValueConverterRegistry.Default.Clone();
        var customStringConverter = new CustomStringConverter();

        registry.Register(customStringConverter);

        var converter = registry.GetConverter<string>();
        converter.Should().BeSameAs(customStringConverter);
    }

    [Fact]
    public void Register_CustomConverter_AvailableViaGetConverter()
    {
        var registry = AttributeValueConverterRegistry.Default.Clone();
        var moneyConverter = new MoneyConverter();

        registry.Register(moneyConverter);

        var converter = registry.GetConverter<Money>();
        converter.Should().BeSameAs(moneyConverter);
    }

    [Fact]
    public void Register_NullConverter_ThrowsArgumentNullException()
    {
        var registry = AttributeValueConverterRegistry.Default.Clone();

        var act = () => registry.Register<string>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Resolution Order Tests

    [Fact]
    public void GetConverter_NullableType_WrapsInnerConverter()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var converter = registry.GetConverter(typeof(int?));

        converter.Should().NotBeNull();
        converter.TargetType.Should().Be(typeof(int?));
    }

    [Fact]
    public void GetConverter_NullableType_RoundTripWorks()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var converter = registry.GetConverter<int?>();

        int? nullValue = null;
        var nullAttr = converter.ToAttributeValue(nullValue);
        nullAttr.NULL.Should().BeTrue();

        int? value = 42;
        var attr = converter.ToAttributeValue(value);
        attr.N.Should().Be("42");

        var result = converter.FromAttributeValue(attr);
        result.Should().Be(42);
    }

    [Fact]
    public void GetConverter_EnumType_ReturnsEnumConverter()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var converter = registry.GetConverter(typeof(OrderStatus));

        converter.Should().NotBeNull();
        converter.TargetType.Should().Be(typeof(OrderStatus));
    }

    [Fact]
    public void GetConverter_EnumType_RoundTripWorks()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var converter = registry.GetConverter<OrderStatus>();

        var value = OrderStatus.Shipped;
        var attr = converter.ToAttributeValue(value);
        attr.S.Should().Be("Shipped");

        var result = converter.FromAttributeValue(attr);
        result.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public void GetConverter_UnregisteredType_ThrowsMissingConverterException()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var act = () => registry.GetConverter(typeof(Money));

        var ex = act.Should().Throw<MissingConverterException>().Which;
        ex.TargetType.Should().Be(typeof(Money));
        ex.Message.Should().Contain("Money");
    }

    #endregion

    #region Open-Generic Collection Resolution Tests

    [Fact]
    public void GetConverter_ListOfEnum_ComposesListConverterWithEnumConverter()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var converter = registry.GetConverter<List<OrderStatus>>();

        converter.Should().NotBeNull();
        converter.TargetType.Should().Be(typeof(List<OrderStatus>));

        // Verify round-trip
        var value = new List<OrderStatus> { OrderStatus.Pending, OrderStatus.Shipped };
        var attr = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attr);
        result.Should().Equal(value);
    }

    [Fact]
    public void GetConverter_ListOfGuid_ComposesListConverterWithGuidConverter()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var converter = registry.GetConverter<List<Guid>>();

        converter.Should().NotBeNull();

        // Verify round-trip
        var value = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var attr = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attr);
        result.Should().Equal(value);
    }

    [Fact]
    public void GetConverter_HashSetOfGuid_ComposesSetConverterWithGuidConverter()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var converter = registry.GetConverter<HashSet<Guid>>();

        converter.Should().NotBeNull();

        // Verify round-trip
        var value = new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var attr = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attr);
        result.Should().BeEquivalentTo(value);
    }

    [Fact]
    public void GetConverter_HashSetOfInt_UsesNativeNS()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var converter = registry.GetConverter<HashSet<int>>();

        // Verify it uses NS (Number Set)
        var value = new HashSet<int> { 1, 2, 3 };
        var attr = converter.ToAttributeValue(value);
        attr.NS.Should().HaveCount(3);
        attr.NS.Should().Contain("1");
        attr.NS.Should().Contain("2");
        attr.NS.Should().Contain("3");
    }

    [Fact]
    public void GetConverter_HashSetOfEnum_UsesSS_WhenStringMode()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var converter = registry.GetConverter<HashSet<OrderStatus>>();

        // Verify it uses SS (String Set) since enum default is String mode
        var value = new HashSet<OrderStatus> { OrderStatus.Pending, OrderStatus.Shipped };
        var attr = converter.ToAttributeValue(value);
        attr.SS.Should().HaveCount(2);
        attr.SS.Should().Contain("Pending");
        attr.SS.Should().Contain("Shipped");
    }

    [Fact]
    public void GetConverter_DictionaryStringMoney_ComposesMapConverterWithCustomConverter()
    {
        var registry = AttributeValueConverterRegistry.Default.Clone();
        registry.Register(new MoneyConverter());

        var converter = registry.GetConverter<Dictionary<string, Money>>();

        converter.Should().NotBeNull();

        // Verify round-trip
        var value = new Dictionary<string, Money>
        {
            ["price1"] = new Money(100.50m, "USD"),
            ["price2"] = new Money(200.75m, "EUR")
        };
        var attr = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attr);
        result.Should().BeEquivalentTo(value);
    }

    [Fact]
    public void GetConverter_DictionaryIntString_ThrowsMissingConverter_NonStringKey()
    {
        var registry = AttributeValueConverterRegistry.Default;

        // DynamoDB M (Map) requires string keys, so Dictionary<int, string> should not resolve
        var act = () => registry.GetConverter<Dictionary<int, string>>();

        act.Should().Throw<MissingConverterException>();
    }

    [Fact]
    public void GetConverter_NestedList_ListOfListOfString_ComposesRecursively()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var converter = registry.GetConverter<List<List<string>>>();

        converter.Should().NotBeNull();

        // Verify round-trip
        var value = new List<List<string>>
        {
            new() { "a", "b" },
            new() { "c", "d", "e" }
        };
        var attr = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attr);
        result.Should().BeEquivalentTo(value);
    }

    [Fact]
    public void GetConverter_ListOfCustomType_WithRegisteredConverter_Composes()
    {
        var registry = AttributeValueConverterRegistry.Default.Clone();
        registry.Register(new MoneyConverter());

        var converter = registry.GetConverter<List<Money>>();

        converter.Should().NotBeNull();

        // Verify round-trip
        var value = new List<Money>
        {
            new(100m, "USD"),
            new(200m, "EUR")
        };
        var attr = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(attr);
        result.Should().BeEquivalentTo(value);
    }

    [Fact]
    public void GetConverter_ListOfUnregisteredType_ThrowsMissingConverterException()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var act = () => registry.GetConverter<List<Money>>();

        act.Should().Throw<MissingConverterException>()
            .Which.TargetType.Should().Be(typeof(Money));
    }

    [Fact]
    public void GenericCollectionConverter_CachedAfterFirstResolution()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var converter1 = registry.GetConverter<List<Guid>>();
        var converter2 = registry.GetConverter<List<Guid>>();

        // Should return the same cached instance
        converter1.Should().BeSameAs(converter2);
    }

    [Fact]
    public void ExactTypeRegistration_TakesPrecedenceOverGenericResolution()
    {
        var registry = AttributeValueConverterRegistry.Default.Clone();
        var customListConverter = new CustomListConverter<int>();

        registry.Register(customListConverter);

        var converter = registry.GetConverter<List<int>>();

        // Should return the exact match, not compose a new one
        converter.Should().BeSameAs(customListConverter);
    }

    #endregion

    #region Test Helper Types

    public enum OrderStatus
    {
        Pending,
        Shipped,
        Delivered,
        Cancelled
    }

    public record Money(decimal Amount, string Currency);

    public class MoneyConverter : AttributeValueConverterBase<Money>
    {
        public override AttributeValue ToAttributeValue(Money value)
        {
            return new AttributeValue
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["amount"] = new AttributeValue { N = value.Amount.ToString() },
                    ["currency"] = new AttributeValue { S = value.Currency }
                }
            };
        }

        public override Money FromAttributeValue(AttributeValue attributeValue)
        {
            if (attributeValue?.M == null)
                return new Money(0, "USD");

            var amount = decimal.Parse(attributeValue.M["amount"].N);
            var currency = attributeValue.M["currency"].S;
            return new Money(amount, currency);
        }
    }

    public class CustomStringConverter : AttributeValueConverterBase<string>
    {
        public override AttributeValue ToAttributeValue(string value)
        {
            return new AttributeValue { S = value?.ToUpperInvariant() ?? "" };
        }

        public override string FromAttributeValue(AttributeValue attributeValue)
        {
            return attributeValue?.S ?? "";
        }
    }

    public class CustomListConverter<T> : AttributeValueConverterBase<List<T>>
    {
        public override AttributeValue ToAttributeValue(List<T> value)
        {
            return new AttributeValue { L = new List<AttributeValue>() };
        }

        public override List<T> FromAttributeValue(AttributeValue attributeValue)
        {
            return new List<T>();
        }
    }

    #endregion
}
