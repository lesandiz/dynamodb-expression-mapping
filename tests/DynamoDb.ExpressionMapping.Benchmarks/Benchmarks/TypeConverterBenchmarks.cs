using Amazon.DynamoDBv2.Model;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DynamoDb.ExpressionMapping.Benchmarks.Fixtures;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for type converter performance — per-type conversion overhead
/// and converter resolution through the registry resolution chain.
/// PR-04.6
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class TypeConverterBenchmarks
{
    // Pre-resolved converters for per-type conversion benchmarks
    private IAttributeValueConverter _stringConverter = null!;
    private IAttributeValueConverter _guidConverter = null!;
    private IAttributeValueConverter _dateTimeConverter = null!;
    private IAttributeValueConverter _enumConverter = null!;
    private IAttributeValueConverter _listOfStringConverter = null!;
    private IAttributeValueConverter _dictionaryConverter = null!;

    // Registry for resolution benchmarks — fresh clone per iteration
    // to avoid cached resolution results from prior invocations
    private AttributeValueConverterRegistry _registryForResolution = null!;

    // Test values
    private readonly string _stringValue = "benchmark-value";
    private readonly Guid _guidValue = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private readonly DateTime _dateTimeValue = new(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
    private readonly OrderPriority _enumValue = OrderPriority.High;
    private readonly List<string> _listValue = new() { "alpha", "beta", "gamma" };
    private readonly Dictionary<string, string> _dictValue = new()
    {
        ["key1"] = "val1",
        ["key2"] = "val2"
    };

    [GlobalSetup]
    public void Setup()
    {
        var registry = AttributeValueConverterRegistry.Default;

        // Pre-resolve converters for per-type benchmarks (measure conversion, not resolution)
        _stringConverter = registry.GetConverter(typeof(string));
        _guidConverter = registry.GetConverter(typeof(Guid));
        _dateTimeConverter = registry.GetConverter(typeof(DateTime));
        _enumConverter = registry.GetConverter(typeof(OrderPriority));
        _listOfStringConverter = registry.GetConverter(typeof(List<string>));
        _dictionaryConverter = registry.GetConverter(typeof(Dictionary<string, string>));
    }

    [IterationSetup(Target = nameof(Resolve_ExactType))]
    public void SetupResolution_Exact()
    {
        // Fresh clone each iteration — exact types (string, int, etc.) are pre-registered
        // in the default registry, so the clone has them. Measures dictionary lookup only.
        _registryForResolution = AttributeValueConverterRegistry.Default.Clone();
    }

    [IterationSetup(Targets = new[]
    {
        nameof(Resolve_Nullable),
        nameof(Resolve_Enum),
        nameof(Resolve_GenericCollection)
    })]
    public void SetupResolution_Dynamic()
    {
        // Fresh clone each iteration — dynamic types (Nullable, Enum, generic collection)
        // are NOT pre-registered, so each resolution triggers the fallback chain
        // (reflection + Activator.CreateInstance + cache insertion).
        _registryForResolution = AttributeValueConverterRegistry.Default.Clone();
    }

    // --- Per-type conversion overhead ---

    [Benchmark(Baseline = true)]
    public AttributeValue Convert_String()
        => _stringConverter.ToAttributeValue(_stringValue);

    [Benchmark]
    public AttributeValue Convert_Guid()
        => _guidConverter.ToAttributeValue(_guidValue);

    [Benchmark]
    public AttributeValue Convert_DateTime()
        => _dateTimeConverter.ToAttributeValue(_dateTimeValue);

    [Benchmark]
    public AttributeValue Convert_Enum_String()
        => _enumConverter.ToAttributeValue(_enumValue);

    [Benchmark]
    public AttributeValue Convert_ListOfString()
        => _listOfStringConverter.ToAttributeValue(_listValue);

    [Benchmark]
    public AttributeValue Convert_Dictionary()
        => _dictionaryConverter.ToAttributeValue(_dictValue);

    // --- Converter resolution overhead ---

    [Benchmark]
    public IAttributeValueConverter Resolve_ExactType()
        => _registryForResolution.GetConverter(typeof(string));

    [Benchmark]
    public IAttributeValueConverter Resolve_Nullable()
        => _registryForResolution.GetConverter(typeof(int?));

    [Benchmark]
    public IAttributeValueConverter Resolve_Enum()
        => _registryForResolution.GetConverter(typeof(OrderPriority));

    [Benchmark]
    public IAttributeValueConverter Resolve_GenericCollection()
        => _registryForResolution.GetConverter(typeof(List<Guid>));
}
