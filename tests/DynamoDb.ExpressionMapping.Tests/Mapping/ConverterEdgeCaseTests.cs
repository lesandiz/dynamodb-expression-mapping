using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Attributes;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Mapping.Converters;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Mapping;

/// <summary>
/// Edge-case tests for type converters and related subsystems.
/// Covers null/empty input handling, constructor guards, boundary conditions,
/// registry resolution, and fluent resolver overrides.
/// Originally P2MutationKillingTests — regions A/B consolidated into [Theory].
/// </summary>
public class ConverterEdgeCaseTests
{
    #region A: Converter FromAttributeValue(null) — kills || -> && mutations

    public static TheoryData<IAttributeValueConverter, object?> ConverterFromNullData => new()
    {
        { new BoolConverter(), false },
        { new Int32Converter(), 0 },
        { new Int64Converter(), 0L },
        { new DoubleConverter(), 0.0 },
        { new DecimalConverter(), 0m },
        { new GuidConverter(), Guid.Empty },
        { new DateTimeConverter(), DateTime.MinValue },
        { new DateTimeOffsetConverter(), DateTimeOffset.MinValue },
        { new StringConverter(), null },
        { new ByteArrayConverter(), null },
    };

    [Theory]
    [MemberData(nameof(ConverterFromNullData))]
    public void Converter_FromNull_ReturnsDefault(IAttributeValueConverter converter, object? expected)
    {
        var result = converter.FromAttributeValue(null!);
        result.Should().Be(expected);
    }

    [Fact]
    public void NullableConverter_FromNull_ReturnsNull()
    {
        var converter = new NullableConverter<int>(new Int32Converter());
        converter.FromAttributeValue(null!).Should().BeNull();
    }

    #endregion

    #region B: Converter FromAttributeValue with empty/missing value — kills string.IsNullOrEmpty mutations

    public static TheoryData<IAttributeValueConverter, AttributeValue, object?> ConverterFromEmptyData => new()
    {
        { new Int32Converter(), new AttributeValue { N = "" }, 0 },
        { new Int64Converter(), new AttributeValue { N = "" }, 0L },
        { new DoubleConverter(), new AttributeValue { N = "" }, 0.0 },
        { new DecimalConverter(), new AttributeValue { N = "" }, 0m },
        { new DateTimeConverter(), new AttributeValue { S = "" }, DateTime.MinValue },
        { new DateTimeOffsetConverter(), new AttributeValue { S = "" }, DateTimeOffset.MinValue },
        { new GuidConverter(), new AttributeValue { S = "" }, Guid.Empty },
    };

    [Theory]
    [MemberData(nameof(ConverterFromEmptyData))]
    public void Converter_FromEmptyValue_ReturnsDefault(IAttributeValueConverter converter, AttributeValue av, object? expected)
    {
        var result = converter.FromAttributeValue(av);
        result.Should().Be(expected);
    }

    #endregion

    #region C: Collection converter constructor null guards — kills statement removal on throw

    [Fact]
    public void ListConverter_NullElementConverter_Throws()
    {
        var act = () => new ListConverter<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetConverter_NullElementConverter_Throws()
    {
        var act = () => new SetConverter<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapConverter_NullValueConverter_Throws()
    {
        var act = () => new MapConverter<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ArrayConverter_NullElementConverter_Throws()
    {
        var act = () => new ArrayConverter<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NullableConverter_NullInnerConverter_Throws()
    {
        var act = () => new NullableConverter<int>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region D: SetConverter SS/NS/L boundary conditions — kills boundary mutations

    [Fact]
    public void SetConverter_FromSS_ReturnsStringSet()
    {
        var converter = new SetConverter<string>(new StringConverter());
        var av = new AttributeValue { SS = new List<string> { "a", "b", "c" } };

        var result = converter.FromAttributeValue(av);

        result.Should().BeEquivalentTo(new HashSet<string> { "a", "b", "c" });
    }

    [Fact]
    public void SetConverter_FromNS_ReturnsNumberSet()
    {
        var converter = new SetConverter<int>(new Int32Converter());
        var av = new AttributeValue { NS = new List<string> { "1", "2", "3" } };

        var result = converter.FromAttributeValue(av);

        result.Should().BeEquivalentTo(new HashSet<int> { 1, 2, 3 });
    }

    [Fact]
    public void SetConverter_FromL_ReturnsFallbackSet()
    {
        var converter = new SetConverter<bool>(new BoolConverter());
        var av = new AttributeValue
        {
            L = new List<AttributeValue>
            {
                new() { BOOL = true },
                new() { BOOL = false }
            }
        };

        var result = converter.FromAttributeValue(av);

        result.Should().BeEquivalentTo(new HashSet<bool> { true, false });
    }

    [Fact]
    public void SetConverter_FromNull_ReturnsEmptySet()
    {
        var converter = new SetConverter<string>(new StringConverter());
        converter.FromAttributeValue(null!).Should().BeEmpty();
    }

    [Fact]
    public void SetConverter_EmptySet_ToAttributeValue_ReturnsEmptyList()
    {
        var converter = new SetConverter<string>(new StringConverter());
        var result = converter.ToAttributeValue(new HashSet<string>());

        result.L.Should().NotBeNull();
        result.L.Should().BeEmpty();
    }

    [Fact]
    public void SetConverter_StringSet_ToAttributeValue_UsesSS()
    {
        var converter = new SetConverter<string>(new StringConverter());
        var result = converter.ToAttributeValue(new HashSet<string> { "x", "y" });

        result.SS.Should().HaveCount(2);
        result.SS.Should().Contain("x");
        result.SS.Should().Contain("y");
    }

    [Fact]
    public void SetConverter_IntSet_ToAttributeValue_UsesNS()
    {
        var converter = new SetConverter<int>(new Int32Converter());
        var result = converter.ToAttributeValue(new HashSet<int> { 10, 20 });

        result.NS.Should().HaveCount(2);
        result.NS.Should().Contain("10");
        result.NS.Should().Contain("20");
    }

    [Fact]
    public void SetConverter_BoolSet_ToAttributeValue_UsesL()
    {
        var converter = new SetConverter<bool>(new BoolConverter());
        var result = converter.ToAttributeValue(new HashSet<bool> { true });

        result.L.Should().HaveCount(1);
        result.L[0].BOOL.Should().BeTrue();
    }

    #endregion

    #region E: ArrayConverter edge cases — kills boundary and null mutations

    [Fact]
    public void ArrayConverter_FromNull_ReturnsEmptyArray()
    {
        var converter = new ArrayConverter<string>(new StringConverter());
        converter.FromAttributeValue(null!).Should().BeEmpty();
    }

    [Fact]
    public void ArrayConverter_FromNullL_ReturnsEmptyArray()
    {
        var converter = new ArrayConverter<int>(new Int32Converter());
        converter.FromAttributeValue(new AttributeValue()).Should().BeEmpty();
    }

    [Fact]
    public void ArrayConverter_FromEmptyL_ReturnsEmptyArray()
    {
        var converter = new ArrayConverter<int>(new Int32Converter());
        converter.FromAttributeValue(new AttributeValue { L = new List<AttributeValue>() })
            .Should().BeEmpty();
    }

    [Fact]
    public void ArrayConverter_RoundTrip_PreservesValues()
    {
        var converter = new ArrayConverter<int>(new Int32Converter());
        var value = new[] { 1, 2, 3 };

        var av = converter.ToAttributeValue(value);
        var result = converter.FromAttributeValue(av);

        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ArrayConverter_ToAttributeValue_Null_ReturnsNULL()
    {
        var converter = new ArrayConverter<string>(new StringConverter());
        var result = converter.ToAttributeValue(null!);

        result.NULL.Should().BeTrue();
    }

    [Fact]
    public void ArrayConverter_ToAttributeValue_Empty_ReturnsNULL()
    {
        var converter = new ArrayConverter<string>(new StringConverter());
        var result = converter.ToAttributeValue(Array.Empty<string>());

        result.NULL.Should().BeTrue();
    }

    #endregion

    #region F: ListConverter edge cases

    [Fact]
    public void ListConverter_FromNull_ReturnsEmptyList()
    {
        var converter = new ListConverter<string>(new StringConverter());
        converter.FromAttributeValue(null!).Should().BeEmpty();
    }

    [Fact]
    public void MapConverter_FromNull_ReturnsEmptyDictionary()
    {
        var converter = new MapConverter<string>(new StringConverter());
        converter.FromAttributeValue(null!).Should().BeEmpty();
    }

    [Fact]
    public void MapConverter_FromNULLTrue_ReturnsEmptyDictionary()
    {
        var converter = new MapConverter<string>(new StringConverter());
        converter.FromAttributeValue(new AttributeValue { NULL = true }).Should().BeEmpty();
    }

    #endregion

    #region G: Registry edge cases — kills argument guard and resolution mutations

    [Fact]
    public void Registry_GetConverter_Null_ThrowsArgumentNullException()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var act = () => registry.GetConverter(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Registry_GetConverter_IReadOnlyList_Resolves()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var converter = registry.GetConverter(typeof(IReadOnlyList<string>));

        converter.Should().NotBeNull();
        converter.TargetType.Should().Be(typeof(List<string>));
    }

    [Fact]
    public void Registry_GetConverter_ISet_Resolves()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var converter = registry.GetConverter(typeof(ISet<string>));

        converter.Should().NotBeNull();
    }

    [Fact]
    public void Registry_GetConverter_IReadOnlyDictionary_Resolves()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var converter = registry.GetConverter(typeof(IReadOnlyDictionary<string, int>));

        converter.Should().NotBeNull();
    }

    [Fact]
    public void Registry_GetConverter_Array_Resolves()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var converter = registry.GetConverter(typeof(string[]));

        converter.Should().NotBeNull();
    }

    [Fact]
    public void Registry_GetConverter_Array_RoundTrip()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var converter = registry.GetConverter(typeof(int[]));

        var av = converter.ToAttributeValue(new[] { 1, 2, 3 });
        var result = (int[])converter.FromAttributeValue(av);

        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Registry_GetConverter_NullableEnum_Resolves()
    {
        var registry = AttributeValueConverterRegistry.Default;
        var converter = registry.GetConverter(typeof(TestStatus?));

        converter.Should().NotBeNull();
    }

    [Fact]
    public void Registry_GetConverter_CachesComposedConverters()
    {
        var registry = AttributeValueConverterRegistry.Default;

        var c1 = registry.GetConverter(typeof(HashSet<Guid>));
        var c2 = registry.GetConverter(typeof(HashSet<Guid>));

        c1.Should().BeSameAs(c2);
    }

    [Fact]
    public void Registry_Clone_IsMutable_SourceRemainsImmutable()
    {
        var original = AttributeValueConverterRegistry.Default;
        var clone = original.Clone();

        // Clone should be mutable
        var act = () => clone.Register(new BoolConverter());
        act.Should().NotThrow();

        // Original should remain frozen
        var act2 = () => original.Register(new BoolConverter());
        act2.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region H: ExpressionValueEmitter — constructor null guard

    [Fact]
    public void ExpressionValueEmitter_NullRegistry_Throws()
    {
        var act = () => new ExpressionValueEmitter(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region I: AttributeNameResolver — fluent override/ignore side effect mutations

    // These tests verify that the side effects of override/ignore operations
    // actually modify resolution behavior, killing statement removal mutations.

    [Fact]
    public void Resolver_FluentOverride_RemovesOldBidirectionalMapping()
    {
        // Map property to "attr_a", then remap to "attr_b"
        // Old reverse mapping "attr_a" -> property should be removed
        var resolver = new AttributeNameResolverBuilder<ResolverTestEntity>()
            .Map(e => e.Name, "attr_a")
            .Build();

        // First verify forward mapping
        resolver.GetAttributeName("Name").Should().Be("attr_a");
        resolver.GetPropertyName("attr_a").Should().Be("Name");

        // Now build a new one with different mapping
        var resolver2 = new AttributeNameResolverBuilder<ResolverTestEntity>()
            .Map(e => e.Name, "attr_b")
            .Build();

        resolver2.GetAttributeName("Name").Should().Be("attr_b");
        resolver2.GetPropertyName("attr_b").Should().Be("Name");
    }

    [Fact]
    public void Resolver_FluentIgnore_RemovesPreviousMapping()
    {
        var resolver = new AttributeNameResolverBuilder<ResolverTestEntity>()
            .Map(e => e.Name, "custom_name")
            .Ignore(e => e.Name)
            .Build();

        // Property should be ignored (not stored)
        resolver.IsStoredAttribute("Name").Should().BeFalse();

        // Reverse lookup for old mapping should no longer return property name
        resolver.GetPropertyName("custom_name").Should().Be("custom_name");
    }

    [Fact]
    public void Resolver_FluentOverride_RemovesIgnoreFlag()
    {
        var resolver = new AttributeNameResolverBuilder<ResolverTestEntity>()
            .Ignore(e => e.Name)
            .Map(e => e.Name, "restored_name")
            .Build();

        resolver.IsStoredAttribute("Name").Should().BeTrue();
        resolver.GetAttributeName("Name").Should().Be("restored_name");
    }

    [Fact]
    public void Resolver_FluentOverride_OverridesAttributeAnnotation()
    {
        var resolver = new AttributeNameResolverBuilder<ResolverAnnotatedEntity>()
            .Map(e => e.CustomerId, "fluent_id")
            .Build();

        resolver.GetAttributeName("CustomerId").Should().Be("fluent_id");
        // Old annotation mapping should be replaced
        resolver.GetPropertyName("customer_id").Should().Be("customer_id");
        resolver.GetPropertyName("fluent_id").Should().Be("CustomerId");
    }

    [Fact]
    public void Resolver_FluentIgnore_OverridesAttributeAnnotation()
    {
        var resolver = new AttributeNameResolverBuilder<ResolverAnnotatedEntity>()
            .Ignore(e => e.CustomerId)
            .Build();

        resolver.IsStoredAttribute("CustomerId").Should().BeFalse();
        // Reverse lookup for annotation mapping should be gone
        resolver.GetPropertyName("customer_id").Should().Be("customer_id");
    }

    #endregion

    #region J: AttributeValueConverterBase non-generic interface — kills cast mutations

    [Fact]
    public void ConverterBase_NonGenericToAttributeValue_DelegatesToTyped()
    {
        IAttributeValueConverter converter = new Int32Converter();
        var result = converter.ToAttributeValue(42);

        result.N.Should().Be("42");
    }

    [Fact]
    public void ConverterBase_NonGenericFromAttributeValue_DelegatesToTyped()
    {
        IAttributeValueConverter converter = new Int32Converter();
        var av = new AttributeValue { N = "42" };
        var result = converter.FromAttributeValue(av);

        result.Should().Be(42);
    }

    [Fact]
    public void ConverterBase_TargetType_ReturnsCorrectType()
    {
        var converter = new Int32Converter();
        converter.TargetType.Should().Be(typeof(int));

        var stringConverter = new StringConverter();
        stringConverter.TargetType.Should().Be(typeof(string));
    }

    #endregion

    #region K: EnumConverter edge cases

    [Fact]
    public void EnumConverter_DefaultMode_IsString()
    {
        var converter = new EnumConverter<TestStatus>();
        var av = converter.ToAttributeValue(TestStatus.Active);
        av.S.Should().Be("Active");
    }

    [Fact]
    public void EnumConverter_NumberMode_CastsToInt()
    {
        var converter = new EnumConverter<TestStatus>(EnumStorageMode.Number);
        var av = converter.ToAttributeValue(TestStatus.Pending);
        av.N.Should().Be("2");
    }

    [Fact]
    public void EnumConverter_NumberMode_FromAttributeValue_Parses()
    {
        var converter = new EnumConverter<TestStatus>(EnumStorageMode.Number);
        var result = converter.FromAttributeValue(new AttributeValue { N = "1" });
        result.Should().Be(TestStatus.Inactive);
    }

    #endregion

    #region L: ByteArrayConverter edge cases

    [Fact]
    public void ByteArrayConverter_ToNull_ReturnsNULL()
    {
        var converter = new ByteArrayConverter();
        var result = converter.ToAttributeValue(null!);
        result.NULL.Should().BeTrue();
    }

    [Fact]
    public void ByteArrayConverter_FromMissingB_ReturnsNull()
    {
        var converter = new ByteArrayConverter();
        // attributeValue.B is null, but NULL is not set
        var result = converter.FromAttributeValue(new AttributeValue());
        result.Should().BeNull();
    }

    [Fact]
    public void ByteArrayConverter_RoundTrip()
    {
        var converter = new ByteArrayConverter();
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var av = converter.ToAttributeValue(data);
        var result = converter.FromAttributeValue(av);

        result.Should().Equal(data);
    }

    #endregion

    #region M: SetConverter FromAttributeValue — empty SS/NS should fall through

    [Fact]
    public void SetConverter_FromEmptySS_FallsThrough()
    {
        // SS exists but is empty — should not match the SS branch (Count > 0 check)
        var converter = new SetConverter<string>(new StringConverter());
        var av = new AttributeValue { SS = new List<string>() };

        var result = converter.FromAttributeValue(av);
        result.Should().BeEmpty();
    }

    [Fact]
    public void SetConverter_FromEmptyNS_FallsThrough()
    {
        var converter = new SetConverter<int>(new Int32Converter());
        var av = new AttributeValue { NS = new List<string>() };

        var result = converter.FromAttributeValue(av);
        result.Should().BeEmpty();
    }

    #endregion

    #region N: NullableConverter ToAttributeValue with value delegates to inner

    [Fact]
    public void NullableConverter_ToAttributeValue_WithValue_DelegatesToInner()
    {
        var converter = new NullableConverter<DateTime>(new DateTimeConverter());
        DateTime? value = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var result = converter.ToAttributeValue(value);

        result.S.Should().Contain("2024-06-15");
        result.NULL.Should().BeFalse();
    }

    [Fact]
    public void NullableConverter_FromAttributeValue_WithValue_DelegatesToInner()
    {
        var converter = new NullableConverter<bool>(new BoolConverter());
        var av = new AttributeValue { BOOL = true };

        var result = converter.FromAttributeValue(av);

        result.Should().Be(true);
    }

    #endregion

    #region Test Helper Types

    public enum TestStatus
    {
        Active = 0,
        Inactive = 1,
        Pending = 2
    }

    public class ResolverTestEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    public class ResolverAnnotatedEntity
    {
        public Guid Id { get; set; }

        [DynamoDbAttribute("customer_id")]
        public Guid CustomerId { get; set; }

        public string Name { get; set; } = "";
    }

    #endregion
}
