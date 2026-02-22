using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;
using FsCheck;
using FsCheck.Fluent;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased;

/// <summary>
/// Property-based tests for Type Converter System.
/// Verifies invariants PR-01.5: round-trip conversion and nullable semantics.
/// </summary>
[Trait("Category", "Property")]
public class TypeConverterProperties
{
    private readonly IAttributeValueConverterRegistry _registry;
    private readonly Config _config;

    public TypeConverterProperties()
    {
        _registry = AttributeValueConverterRegistry.Default;
        _config = Config.Quick.WithMaxTest(PropertyTestConfig.MaxTest);
    }

    #region PR-01.5 Invariant 1: Built-In Converter Round-Trips

    /// <summary>
    /// Invariant: For every supported .NET type,
    /// converter.FromAttributeValue(converter.ToAttributeValue(value)) == value
    /// (Round-trip identity)
    /// </summary>
    [Fact]
    public void StringConverterRoundTrips()
    {
        var stringGen = Gen.Frequency(
            (1, Gen.Constant("")),
            (9, Gen.Elements("a", "test", "hello", "world", "DynamoDB", "attribute-value", "123", "special!@#")));

        var property = Prop.ForAll(
            Arb.From(stringGen),
            value =>
            {
                var converter = _registry.GetConverter<string>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);
                return (roundTripped == value).Label($"Expected: {value}, Got: {roundTripped}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void Int32ConverterRoundTrips()
    {
        var int32Gen = Gen.Choose(int.MinValue, int.MaxValue);

        var property = Prop.ForAll(
            Arb.From(int32Gen),
            value =>
            {
                var converter = _registry.GetConverter<int>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);
                return (roundTripped == value).Label($"Expected: {value}, Got: {roundTripped}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void Int64ConverterRoundTrips()
    {
        var int64Gen = Gen.Elements(0L, 1L, -1L, 100L, -100L, long.MaxValue, long.MinValue);

        var property = Prop.ForAll(
            Arb.From(int64Gen),
            value =>
            {
                var converter = _registry.GetConverter<long>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);
                return (roundTripped == value).Label($"Expected: {value}, Got: {roundTripped}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void DecimalConverterRoundTrips()
    {
        var decimalGen = Gen.Elements(0m, 1m, -1m, 123.45m, -999.99m, 1000000.5m, decimal.MaxValue, decimal.MinValue);

        var property = Prop.ForAll(
            Arb.From(decimalGen),
            value =>
            {
                var converter = _registry.GetConverter<decimal>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);
                return (roundTripped == value).Label($"Expected: {value}, Got: {roundTripped}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void DoubleConverterRoundTrips()
    {
        var doubleGen = Gen.Elements(0.0, 1.0, -1.0, 123.45, -999.99, 1000000.5, double.MaxValue, double.MinValue, double.Epsilon);

        var property = Prop.ForAll(
            Arb.From(doubleGen),
            value =>
            {
                var converter = _registry.GetConverter<double>();

                // Skip NaN and Infinity as DynamoDB doesn't support them
                if (double.IsNaN(value) || double.IsInfinity(value))
                    return Prop.Label(true, "Skipping NaN/Infinity");

                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                // Use approximate equality for floating point
                var equal = Math.Abs(roundTripped - value) < 1e-10;
                return equal.Label($"Expected: {value}, Got: {roundTripped}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void BoolConverterRoundTrips()
    {
        var boolGen = Gen.Elements(true, false);

        var property = Prop.ForAll(
            Arb.From(boolGen),
            value =>
            {
                var converter = _registry.GetConverter<bool>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);
                return (roundTripped == value).Label($"Expected: {value}, Got: {roundTripped}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void GuidConverterRoundTrips()
    {
        var guidGen = Gen.Elements(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            Guid.Parse("12345678-1234-1234-1234-123456789012"));

        var property = Prop.ForAll(
            Arb.From(guidGen),
            value =>
            {
                var converter = _registry.GetConverter<Guid>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);
                return (roundTripped == value).Label($"Expected: {value}, Got: {roundTripped}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void DateTimeConverterRoundTrips()
    {
        var dateTimeGen = Gen.Elements(
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddYears(1),
            new DateTime(2020, 1, 1),
            new DateTime(2025, 12, 31),
            DateTime.MinValue,
            DateTime.MaxValue);

        var property = Prop.ForAll(
            Arb.From(dateTimeGen),
            value =>
            {
                var converter = _registry.GetConverter<DateTime>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                // DateTime stores as ISO8601 string, may lose sub-millisecond precision
                var equal = Math.Abs((roundTripped - value).TotalMilliseconds) < 1;
                return equal.Label($"Expected: {value:O}, Got: {roundTripped:O}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void DateTimeOffsetConverterRoundTrips()
    {
        var dateTimeOffsetGen = Gen.Elements(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(-30),
            DateTimeOffset.UtcNow.AddYears(1),
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero),
            DateTimeOffset.MinValue,
            DateTimeOffset.MaxValue);

        var property = Prop.ForAll(
            Arb.From(dateTimeOffsetGen),
            value =>
            {
                var converter = _registry.GetConverter<DateTimeOffset>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                // DateTimeOffset stores as ISO8601 string, may lose sub-millisecond precision
                var equal = Math.Abs((roundTripped - value).TotalMilliseconds) < 1;
                return equal.Label($"Expected: {value:O}, Got: {roundTripped:O}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void ByteArrayConverterRoundTrips()
    {
        var byteGen = Gen.Choose(0, 255).Select(i => (byte)i);
        var byteArrayGen = Gen.Frequency(
            (1, Gen.Constant(Array.Empty<byte>())),
            (9, Gen.ArrayOf(byteGen)));

        var property = Prop.ForAll(
            Arb.From(byteArrayGen),
            value =>
            {
                if (value == null)
                    return Prop.Label(true, "Null handled by nullable test");

                var converter = _registry.GetConverter<byte[]>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                var equal = value.SequenceEqual(roundTripped);
                return equal.Label($"Expected length: {value.Length}, Got length: {roundTripped?.Length ?? -1}");
            });

        Check.One(_config, property);
    }

    #endregion

    #region PR-01.5 Invariant 2: Nullable Converter Preserves Null Semantics

    /// <summary>
    /// Invariant: Nullable converter preserves null semantics:
    /// - null → NULL AttributeValue → null
    /// - non-null → typed AttributeValue → original value
    /// </summary>
    [Fact]
    public void NullableInt32ConverterPreservesNullSemantics()
    {
        var int32Gen = Gen.Choose(int.MinValue, int.MaxValue);
        var nullableInt32Gen = Gen.Frequency(
            (1, Gen.Constant<int?>(null)),
            (9, int32Gen.Select(i => (int?)i)));

        var property = Prop.ForAll(
            Arb.From(nullableInt32Gen),
            value =>
            {
                var converter = _registry.GetConverter<int?>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                if (value == null)
                {
                    if (attributeValue.NULL != true)
                        return Prop.Label(false, "Null value should produce NULL=true AttributeValue");
                    if (roundTripped != null)
                        return Prop.Label(false, "NULL AttributeValue should round-trip to null");
                    return Prop.Label(true, "Null semantics preserved");
                }

                // Non-null value
                if (attributeValue.NULL == true)
                    return Prop.Label(false, "Non-null value should not produce NULL=true");
                if (roundTripped != value)
                    return Prop.Label(false, $"Expected: {value}, Got: {roundTripped}");

                return Prop.Label(true, "Non-null value round-tripped correctly");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void NullableInt64ConverterPreservesNullSemantics()
    {
        var int64Gen = Gen.Elements(0L, 1L, -1L, 100L, -100L, long.MaxValue, long.MinValue);
        var nullableInt64Gen = Gen.Frequency(
            (1, Gen.Constant<long?>(null)),
            (9, int64Gen.Select(i => (long?)i)));

        var property = Prop.ForAll(
            Arb.From(nullableInt64Gen),
            value =>
            {
                var converter = _registry.GetConverter<long?>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                if (value == null)
                {
                    if (attributeValue.NULL != true)
                        return Prop.Label(false, "Null value should produce NULL=true AttributeValue");
                    if (roundTripped != null)
                        return Prop.Label(false, "NULL AttributeValue should round-trip to null");
                    return Prop.Label(true, "Null semantics preserved");
                }

                // Non-null value
                if (attributeValue.NULL == true)
                    return Prop.Label(false, "Non-null value should not produce NULL=true");
                if (roundTripped != value)
                    return Prop.Label(false, $"Expected: {value}, Got: {roundTripped}");

                return Prop.Label(true, "Non-null value round-tripped correctly");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void NullableBoolConverterPreservesNullSemantics()
    {
        var boolGen = Gen.Elements(true, false);
        var nullableBoolGen = Gen.Frequency(
            (1, Gen.Constant<bool?>(null)),
            (9, boolGen.Select(b => (bool?)b)));

        var property = Prop.ForAll(
            Arb.From(nullableBoolGen),
            value =>
            {
                var converter = _registry.GetConverter<bool?>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                if (value == null)
                {
                    if (attributeValue.NULL != true)
                        return Prop.Label(false, "Null value should produce NULL=true AttributeValue");
                    if (roundTripped != null)
                        return Prop.Label(false, "NULL AttributeValue should round-trip to null");
                    return Prop.Label(true, "Null semantics preserved");
                }

                // Non-null value
                if (attributeValue.NULL == true)
                    return Prop.Label(false, "Non-null value should not produce NULL=true");
                if (roundTripped != value)
                    return Prop.Label(false, $"Expected: {value}, Got: {roundTripped}");

                return Prop.Label(true, "Non-null value round-tripped correctly");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void NullableDateTimeConverterPreservesNullSemantics()
    {
        var dateTimeGen = Gen.Elements(
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-30),
            new DateTime(2020, 1, 1),
            DateTime.MinValue,
            DateTime.MaxValue);
        var nullableDateTimeGen = Gen.Frequency(
            (1, Gen.Constant<DateTime?>(null)),
            (9, dateTimeGen.Select(d => (DateTime?)d)));

        var property = Prop.ForAll(
            Arb.From(nullableDateTimeGen),
            value =>
            {
                var converter = _registry.GetConverter<DateTime?>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                if (value == null)
                {
                    if (attributeValue.NULL != true)
                        return Prop.Label(false, "Null value should produce NULL=true AttributeValue");
                    if (roundTripped != null)
                        return Prop.Label(false, "NULL AttributeValue should round-trip to null");
                    return Prop.Label(true, "Null semantics preserved");
                }

                // Non-null value
                if (attributeValue.NULL == true)
                    return Prop.Label(false, "Non-null value should not produce NULL=true");

                // DateTime stores as ISO8601 string, may lose sub-millisecond precision
                var equal = roundTripped.HasValue && Math.Abs((roundTripped.Value - value.Value).TotalMilliseconds) < 1;
                if (!equal)
                    return Prop.Label(false, $"Expected: {value:O}, Got: {roundTripped:O}");

                return Prop.Label(true, "Non-null value round-tripped correctly");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void NullableGuidConverterPreservesNullSemantics()
    {
        var guidGen = Gen.Elements(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            Guid.Parse("12345678-1234-1234-1234-123456789012"));
        var nullableGuidGen = Gen.Frequency(
            (1, Gen.Constant<Guid?>(null)),
            (9, guidGen.Select(g => (Guid?)g)));

        var property = Prop.ForAll(
            Arb.From(nullableGuidGen),
            value =>
            {
                var converter = _registry.GetConverter<Guid?>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                if (value == null)
                {
                    if (attributeValue.NULL != true)
                        return Prop.Label(false, "Null value should produce NULL=true AttributeValue");
                    if (roundTripped != null)
                        return Prop.Label(false, "NULL AttributeValue should round-trip to null");
                    return Prop.Label(true, "Null semantics preserved");
                }

                // Non-null value
                if (attributeValue.NULL == true)
                    return Prop.Label(false, "Non-null value should not produce NULL=true");
                if (roundTripped != value)
                    return Prop.Label(false, $"Expected: {value}, Got: {roundTripped}");

                return Prop.Label(true, "Non-null value round-tripped correctly");
            });

        Check.One(_config, property);
    }

    #endregion

    #region PR-01.5 Invariant 3: Collection Converters Round-Trip

    [Fact]
    public void ListOfStringConverterRoundTrips()
    {
        var stringGen = Gen.Elements("a", "test", "hello", "world", "DynamoDB");
        var listGen = Gen.ListOf(stringGen).Select(list => new List<string>(list));

        var property = Prop.ForAll(
            Arb.From(listGen),
            value =>
            {
                if (value == null)
                    return Prop.Label(true, "Null handled separately");

                var converter = _registry.GetConverter<List<string>>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                var equal = value.SequenceEqual(roundTripped);
                return equal.Label($"Expected count: {value.Count}, Got count: {roundTripped?.Count ?? -1}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void ListOfInt32ConverterRoundTrips()
    {
        var int32Gen = Gen.Choose(-1000, 1000);
        var listGen = Gen.ListOf(int32Gen).Select(list => new List<int>(list));

        var property = Prop.ForAll(
            Arb.From(listGen),
            value =>
            {
                if (value == null)
                    return Prop.Label(true, "Null handled separately");

                var converter = _registry.GetConverter<List<int>>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                var equal = value.SequenceEqual(roundTripped);
                return equal.Label($"Expected count: {value.Count}, Got count: {roundTripped?.Count ?? -1}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void HashSetOfStringConverterRoundTrips()
    {
        var stringGen = Gen.Elements("a", "b", "c", "test", "hello", "world");
        var hashSetGen = Gen.ListOf(stringGen).Select(list => new HashSet<string>(list));

        var property = Prop.ForAll(
            Arb.From(hashSetGen),
            value =>
            {
                if (value == null)
                    return Prop.Label(true, "Null handled separately");

                var converter = _registry.GetConverter<HashSet<string>>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                var equal = value.SetEquals(roundTripped);
                return equal.Label($"Expected count: {value.Count}, Got count: {roundTripped?.Count ?? -1}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void ArrayOfStringConverterRoundTrips()
    {
        var stringGen = Gen.Elements("a", "test", "hello", "world", "DynamoDB");
        var arrayGen = Gen.ArrayOf(stringGen);

        var property = Prop.ForAll(
            Arb.From(arrayGen),
            value =>
            {
                if (value == null)
                    return Prop.Label(true, "Null handled separately");

                var converter = _registry.GetConverter<string[]>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                var equal = value.SequenceEqual(roundTripped);
                return equal.Label($"Expected length: {value.Length}, Got length: {roundTripped?.Length ?? -1}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void DictionaryOfStringConverterRoundTrips()
    {
        var keyGen = Gen.Elements("key1", "key2", "key3", "name", "type");
        var valueGen = Gen.Elements("val1", "val2", "test", "data");
        var kvpGen = from key in keyGen
                     from value in valueGen
                     select (key, value);
        var dictGen = Gen.ListOf(kvpGen).Select(pairs =>
        {
            var dict = new Dictionary<string, string>();
            foreach (var (key, value) in pairs)
            {
                dict[key] = value; // Overwrites if duplicate key
            }
            return dict;
        });

        var property = Prop.ForAll(
            Arb.From(dictGen),
            value =>
            {
                if (value == null)
                    return Prop.Label(true, "Null handled separately");

                var converter = _registry.GetConverter<Dictionary<string, string>>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                if (value.Count != roundTripped.Count)
                    return Prop.Label(false, $"Expected count: {value.Count}, Got count: {roundTripped.Count}");

                foreach (var kvp in value)
                {
                    if (!roundTripped.TryGetValue(kvp.Key, out var roundTrippedValue))
                        return Prop.Label(false, $"Key '{kvp.Key}' missing in round-tripped dictionary");
                    if (kvp.Value != roundTrippedValue)
                        return Prop.Label(false, $"Key '{kvp.Key}': Expected '{kvp.Value}', Got '{roundTrippedValue}'");
                }

                return Prop.Label(true, "Dictionary round-tripped correctly");
            });

        Check.One(_config, property);
    }

    #endregion

    #region PR-01.5 Invariant 4: Enum Converter Round-Trips

    public enum TestEnumStatus
    {
        Pending,
        Active,
        Completed,
        Cancelled
    }

    [Fact]
    public void EnumConverterRoundTrips()
    {
        var enumGen = Gen.Elements(
            TestEnumStatus.Pending,
            TestEnumStatus.Active,
            TestEnumStatus.Completed,
            TestEnumStatus.Cancelled);

        var property = Prop.ForAll(
            Arb.From(enumGen),
            value =>
            {
                var converter = _registry.GetConverter<TestEnumStatus>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                return (roundTripped == value).Label($"Expected: {value}, Got: {roundTripped}");
            });

        Check.One(_config, property);
    }

    [Fact]
    public void NullableEnumConverterPreservesNullSemantics()
    {
        var enumGen = Gen.Elements(
            TestEnumStatus.Pending,
            TestEnumStatus.Active,
            TestEnumStatus.Completed,
            TestEnumStatus.Cancelled);
        var nullableEnumGen = Gen.Frequency(
            (1, Gen.Constant<TestEnumStatus?>(null)),
            (9, enumGen.Select(e => (TestEnumStatus?)e)));

        var property = Prop.ForAll(
            Arb.From(nullableEnumGen),
            value =>
            {
                var converter = _registry.GetConverter<TestEnumStatus?>();
                var attributeValue = converter.ToAttributeValue(value);
                var roundTripped = converter.FromAttributeValue(attributeValue);

                if (value == null)
                {
                    if (attributeValue.NULL != true)
                        return Prop.Label(false, "Null value should produce NULL=true AttributeValue");
                    if (roundTripped != null)
                        return Prop.Label(false, "NULL AttributeValue should round-trip to null");
                    return Prop.Label(true, "Null semantics preserved");
                }

                // Non-null value
                if (attributeValue.NULL == true)
                    return Prop.Label(false, "Non-null value should not produce NULL=true");
                if (roundTripped != value)
                    return Prop.Label(false, $"Expected: {value}, Got: {roundTripped}");

                return Prop.Label(true, "Non-null value round-tripped correctly");
            });

        Check.One(_config, property);
    }

    #endregion
}
