using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Bogus;
using DynamoDb.ExpressionMapping.Attributes;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Mapping;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Mapping;

/// <summary>
/// Comprehensive test suite for Spec 05: Type Converter System - ExpressionValueEmitter.
/// Tests converter resolution, PropertyInfo handling, integration with expression builders, and thread safety.
/// </summary>
public class ExpressionValueEmitterTests
{
    private readonly Faker _faker = new();

    #region Converter Resolution Tests

    [Fact]
    public void Emit_WithDynamoDbConverterAttribute_UsesAttributeConverter()
    {
        var registry = AttributeValueConverterRegistry.Default.Clone();
        registry.Register(new DefaultMoneyConverter());
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.PriceWithCustomConverter))!;
        var value = new Money(99.99m, "USD");

        var result = emitter.Emit(value, property);

        // CustomMoneyConverter adds a "custom" field
        result.M.Should().ContainKey("custom");
        result.M["custom"].S.Should().Be("true");
    }

    [Fact]
    public void Emit_WithRegisteredConverter_UsesRegistryConverter()
    {
        var registry = AttributeValueConverterRegistry.Default.Clone();
        registry.Register(new DefaultMoneyConverter());
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.PriceWithoutAttribute))!;
        var value = new Money(99.99m, "USD");

        var result = emitter.Emit(value, property);

        // DefaultMoneyConverter does not add "custom" field
        result.M.Should().NotContainKey("custom");
        result.M["amount"].N.Should().Be("99.99");
    }

    [Fact]
    public void Emit_NullableValue_WrapsInNullableConverter()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.Stock))!;
        int? value = 42;

        var result = emitter.Emit(value, property);

        result.N.Should().Be("42");
    }

    [Fact]
    public void Emit_NullableValue_Null_WritesNULL()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.Stock))!;
        int? value = null;

        var result = emitter.Emit(value!, property);

        result.NULL.Should().BeTrue();
    }

    [Fact]
    public void Emit_EnumValue_UsesEnumConverter()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.Status))!;
        var value = ProductStatus.Available;

        var result = emitter.Emit(value, property);

        result.S.Should().Be("Available");
    }

    [Fact]
    public void Emit_CollectionValue_UsesGenericCollectionConverter()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.Tags))!;
        var value = new List<string> { "new", "featured", "sale" };

        var result = emitter.Emit(value, property);

        result.L.Should().HaveCount(3);
        result.L[0].S.Should().Be("new");
    }

    [Fact]
    public void Emit_UnregisteredType_ThrowsMissingConverterException()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.PriceWithoutAttribute))!;
        var value = new Money(99.99m, "USD");

        var act = () => emitter.Emit(value, property);

        act.Should().Throw<MissingConverterException>()
            .Which.TargetType.Should().Be(typeof(Money));
    }

    #endregion

    #region PropertyInfo Null Handling Tests

    [Fact]
    public void Emit_NullPropertyInfo_ResolvesViaRuntimeType()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var value = _faker.Lorem.Word();

        var result = emitter.Emit(value, null);

        result.S.Should().Be(value);
    }

    [Fact]
    public void Emit_NullPropertyInfo_IntValue_UsesIntConverter()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var value = 42;

        var result = emitter.Emit(value, null);

        result.N.Should().Be("42");
    }

    [Fact]
    public void Emit_NullPropertyInfo_EnumValue_UsesEnumConverter()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var value = ProductStatus.Available;

        var result = emitter.Emit(value, null);

        result.S.Should().Be("Available");
    }

    [Fact]
    public void Emit_NullPropertyInfo_GuidValue_UsesGuidConverter()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var value = Guid.NewGuid();

        var result = emitter.Emit(value, null);

        result.S.Should().Be(value.ToString());
    }

    [Fact]
    public void Emit_NullPropertyInfo_ListValue_UsesListConverter()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var value = new List<int> { 1, 2, 3 };

        var result = emitter.Emit(value, null);

        result.L.Should().HaveCount(3);
        result.L[0].N.Should().Be("1");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Emit_GuidValue_ProducesStringAttributeValue()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.Id))!;
        var value = Guid.NewGuid();

        var result = emitter.Emit(value, property);

        result.S.Should().Be(value.ToString());
        result.N.Should().BeNull();
        result.BOOL.Should().BeFalse();
    }

    [Fact]
    public void Emit_DecimalValue_ProducesNumberAttributeValue()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.Weight))!;
        var value = 123.45m;

        var result = emitter.Emit(value, property);

        result.N.Should().NotBeNullOrEmpty();
        result.S.Should().BeNull();
    }

    [Fact]
    public void Emit_DateTimeValue_ProducesIso8601StringAttributeValue()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.CreatedAt))!;
        var value = new DateTime(2024, 1, 15, 14, 30, 45, DateTimeKind.Utc);

        var result = emitter.Emit(value, property);

        result.S.Should().Be("2024-01-15T14:30:45.0000000Z");
    }

    [Fact]
    public void Emit_BoolValue_ProducesBoolAttributeValue()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.IsActive))!;
        var value = true;

        var result = emitter.Emit(value, property);

        result.BOOL.Should().BeTrue();
        result.S.Should().BeNull();
        result.N.Should().BeNull();
    }

    [Fact]
    public void Emit_CustomTypeWithConverter_ProducesMapAttributeValue()
    {
        var registry = AttributeValueConverterRegistry.Default.Clone();
        registry.Register(new DefaultMoneyConverter());
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.PriceWithoutAttribute))!;
        var value = new Money(99.99m, "USD");

        var result = emitter.Emit(value, property);

        result.M.Should().NotBeNull();
        result.M.Should().ContainKey("amount");
        result.M.Should().ContainKey("currency");
    }

    [Fact]
    public void Emit_ByteArrayValue_ProducesBinaryAttributeValue()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var property = typeof(Product).GetProperty(nameof(Product.ImageData))!;
        var value = new byte[] { 1, 2, 3, 4, 5 };

        var result = emitter.Emit(value, property);

        result.B.Should().NotBeNull();
        result.B.ToArray().Should().Equal(value);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void Emit_ConcurrentCalls_NoRaceConditions()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);
        var property = typeof(Product).GetProperty(nameof(Product.Id))!;

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
        {
            var value = Guid.NewGuid();
            var result = emitter.Emit(value, property);
            result.S.Should().Be(value.ToString());
        })).ToArray();

        var act = () => Task.WaitAll(tasks);

        act.Should().NotThrow();
    }

    [Fact]
    public void Emit_ConcurrentCalls_DifferentTypes_NoRaceConditions()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var guidProperty = typeof(Product).GetProperty(nameof(Product.Id))!;
        var intProperty = typeof(Product).GetProperty(nameof(Product.Stock))!;
        var stringProperty = typeof(Product).GetProperty(nameof(Product.Name))!;
        var enumProperty = typeof(Product).GetProperty(nameof(Product.Status))!;

        var tasks = new List<Task>();

        // Mix different types being converted concurrently
        for (int i = 0; i < 25; i++)
        {
            tasks.Add(Task.Run(() => emitter.Emit(Guid.NewGuid(), guidProperty)));
            tasks.Add(Task.Run(() => emitter.Emit(42, intProperty)));
            tasks.Add(Task.Run(() => emitter.Emit("test", stringProperty)));
            tasks.Add(Task.Run(() => emitter.Emit(ProductStatus.Available, enumProperty)));
        }

        var act = () => Task.WaitAll(tasks.ToArray());

        act.Should().NotThrow();
    }

    [Fact]
    public void Emit_ConcurrentCalls_GenericCollectionResolution_NoRaceConditions()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);

        var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
        {
            var property = typeof(Product).GetProperty(nameof(Product.Tags))!;
            var value = new List<string> { "tag1", "tag2" };
            var result = emitter.Emit(value, property);
            result.L.Should().HaveCount(2);
        })).ToArray();

        var act = () => Task.WaitAll(tasks);

        act.Should().NotThrow();
    }

    [Fact]
    public void Emit_ConcurrentCalls_NullableResolution_NoRaceConditions()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var emitter = new ExpressionValueEmitter(registry);
        var property = typeof(Product).GetProperty(nameof(Product.Stock))!;

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
        {
            int? value = i % 2 == 0 ? i : null;
            var result = emitter.Emit(value!, property);

            if (value.HasValue)
                result.N.Should().Be(value.ToString());
            else
                result.NULL.Should().BeTrue();
        })).ToArray();

        var act = () => Task.WaitAll(tasks);

        act.Should().NotThrow();
    }

    #endregion

    #region Test Helper Types

    public enum ProductStatus
    {
        Available,
        OutOfStock,
        Discontinued
    }

    public record Money(decimal Amount, string Currency);

    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int? Stock { get; set; }
        public ProductStatus Status { get; set; }
        public List<string> Tags { get; set; } = new();
        public decimal Weight { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public byte[]? ImageData { get; set; }

        [DynamoDbConverter(typeof(CustomMoneyConverter))]
        public Money PriceWithCustomConverter { get; set; } = new(0, "USD");

        public Money PriceWithoutAttribute { get; set; } = new(0, "USD");
    }

    public class DefaultMoneyConverter : AttributeValueConverterBase<Money>
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

    public class CustomMoneyConverter : AttributeValueConverterBase<Money>
    {
        public override AttributeValue ToAttributeValue(Money value)
        {
            return new AttributeValue
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["amount"] = new AttributeValue { N = value.Amount.ToString() },
                    ["currency"] = new AttributeValue { S = value.Currency },
                    ["custom"] = new AttributeValue { S = "true" }
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

    #endregion
}
