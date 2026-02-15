# PR-04: Benchmarking

## Motivation

The library uses compiled expression trees and caching (Spec 09) for performance, but there are no measurements proving this. Without benchmarks:
- We cannot detect performance regressions between releases
- We cannot validate that caching provides meaningful speedup
- We cannot identify allocation-heavy paths that cause GC pressure in high-throughput scenarios
- Consumer teams cannot make informed decisions about library overhead

## Scope

A dedicated benchmark project measuring expression building, type conversion, result mapping, and caching across cold and warm paths.

## Project Structure

```
tests/
└── DynamoDb.ExpressionMapping.Benchmarks/
    ├── DynamoDb.ExpressionMapping.Benchmarks.csproj
    ├── Program.cs
    ├── Benchmarks/
    │   ├── ProjectionBuilderBenchmarks.cs
    │   ├── FilterExpressionBenchmarks.cs
    │   ├── UpdateExpressionBenchmarks.cs
    │   ├── KeyConditionBenchmarks.cs
    │   ├── DirectResultMapperBenchmarks.cs
    │   ├── TypeConverterBenchmarks.cs
    │   ├── FilterCompositionBenchmarks.cs
    │   ├── ExpressionCacheBenchmarks.cs
    │   └── EndToEndBenchmarks.cs
    ├── Fixtures/
    │   └── BenchmarkEntities.cs
    └── README.md
```

## Dependencies

- **[BenchmarkDotNet](https://www.nuget.org/packages/BenchmarkDotNet)** (>= 0.14.x) — .NET benchmarking framework
- `DynamoDb.ExpressionMapping` (project reference)

## Benchmark Design

### PR-04.1: Projection Builder

```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class ProjectionBuilderBenchmarks
{
    // Cold path — first invocation, no cache
    [Benchmark(Baseline = true)]
    public ProjectionResult BuildProjection_Cold_SingleProperty()

    [Benchmark]
    public ProjectionResult BuildProjection_Cold_FiveProperties()

    [Benchmark]
    public ProjectionResult BuildProjection_Cold_TwentyProperties()

    [Benchmark]
    public ProjectionResult BuildProjection_Cold_NestedProperties()

    // Warm path — cached result
    [Benchmark]
    public ProjectionResult BuildProjection_Warm_SingleProperty()

    [Benchmark]
    public ProjectionResult BuildProjection_Warm_TwentyProperties()

    // With reserved keywords (alias generation overhead)
    [Benchmark]
    public ProjectionResult BuildProjection_ReservedKeywords_Five()
}
```

### PR-04.2: Filter Expression Builder

```csharp
[MemoryDiagnoser]
public class FilterExpressionBenchmarks
{
    // Complexity tiers
    [Benchmark(Baseline = true)]
    public FilterExpressionResult SimpleEquality()           // x => x.Id == guid

    [Benchmark]
    public FilterExpressionResult ThreePredicates_And()      // x => A && B && C

    [Benchmark]
    public FilterExpressionResult ComplexPredicate()          // Nested AND/OR/NOT + functions

    [Benchmark]
    public FilterExpressionResult WithNestedProperty()       // x => x.Address.City == "London"

    // Captured variable evaluation
    [Benchmark]
    public FilterExpressionResult CapturedVariable_String()

    [Benchmark]
    public FilterExpressionResult CapturedVariable_Enum()

    [Benchmark]
    public FilterExpressionResult CapturedVariable_DateTime()
}
```

### PR-04.3: Filter Composition

```csharp
[MemoryDiagnoser]
public class FilterCompositionBenchmarks
{
    // Re-aliasing overhead
    [Benchmark(Baseline = true)]
    public FilterExpressionResult And_TwoSimpleFilters()

    [Benchmark]
    public FilterExpressionResult And_TwoComplexFilters()

    [Benchmark]
    public FilterExpressionResult Chain_FiveFilters_And()    // f1.And(f2).And(f3).And(f4).And(f5)

    [Benchmark]
    public FilterExpressionResult Or_TwoFilters()
}
```

### PR-04.4: Update Expression Builder

```csharp
[MemoryDiagnoser]
public class UpdateExpressionBenchmarks
{
    [Benchmark(Baseline = true)]
    public UpdateExpressionResult SingleSet()

    [Benchmark]
    public UpdateExpressionResult FiveSets()

    [Benchmark]
    public UpdateExpressionResult MixedClauses()             // SET + REMOVE + ADD

    [Benchmark]
    public UpdateExpressionResult WithFunctions()            // if_not_exists, list_append
}
```

### PR-04.5: Direct Result Mapper

```csharp
[MemoryDiagnoser]
public class DirectResultMapperBenchmarks
{
    // Delegate compilation (cold)
    [Benchmark]
    public Func<Dictionary<string, AttributeValue>, T> CreateMapper_AnonymousType()

    [Benchmark]
    public Func<Dictionary<string, AttributeValue>, T> CreateMapper_NamedType_FiveProps()

    [Benchmark]
    public Func<Dictionary<string, AttributeValue>, T> CreateMapper_Record()

    // Mapping execution (warm — reusing compiled delegate)
    [Benchmark(Baseline = true)]
    public object Map_AnonymousType_ThreeProps()

    [Benchmark]
    public object Map_NamedType_TenProps()

    [Benchmark]
    public object Map_NestedType()

    [Benchmark]
    public object Map_WithCustomConverter()

    // Comparison: direct mapping vs manual deserialization
    [Benchmark]
    public object Map_Manual_Baseline()                      // Hand-written mapping code
}
```

### PR-04.6: Type Converter Performance

```csharp
[MemoryDiagnoser]
public class TypeConverterBenchmarks
{
    // Per-type conversion overhead
    [Benchmark] public AttributeValue Convert_String()
    [Benchmark] public AttributeValue Convert_Guid()
    [Benchmark] public AttributeValue Convert_DateTime()
    [Benchmark] public AttributeValue Convert_Enum_String()
    [Benchmark] public AttributeValue Convert_Enum_Number()
    [Benchmark] public AttributeValue Convert_ListOfString()
    [Benchmark] public AttributeValue Convert_Dictionary()

    // Converter resolution overhead
    [Benchmark] public IAttributeValueConverter Resolve_ExactType()
    [Benchmark] public IAttributeValueConverter Resolve_Nullable()
    [Benchmark] public IAttributeValueConverter Resolve_Enum()
    [Benchmark] public IAttributeValueConverter Resolve_GenericCollection()
}
```

### PR-04.7: Expression Cache

```csharp
[MemoryDiagnoser]
public class ExpressionCacheBenchmarks
{
    // Cache lookup performance at various sizes
    [Params(10, 100, 1000)]
    public int CacheSize { get; set; }

    [Benchmark(Baseline = true)]
    public ProjectionResult CacheHit()

    [Benchmark]
    public ProjectionResult CacheMiss()

    // Key generation overhead
    [Benchmark]
    public string GenerateKey_SimpleExpression()

    [Benchmark]
    public string GenerateKey_ComplexExpression()
}
```

### PR-04.8: End-to-End Pipeline

```csharp
[MemoryDiagnoser]
public class EndToEndBenchmarks
{
    // Full pipeline: build expressions → apply to request → map results
    [Benchmark]
    public object QueryWithProjectionAndFilter_BuildOnly()

    [Benchmark]
    public object QueryWithProjectionAndFilter_BuildAndApply()

    // Compare cold vs warm for full pipeline
    [Benchmark]
    public object FullPipeline_Cold()

    [Benchmark]
    public object FullPipeline_Warm()
}
```

## Execution

```bash
# Run all benchmarks
dotnet run -c Release --project tests/DynamoDb.ExpressionMapping.Benchmarks

# Run specific benchmark class
dotnet run -c Release --project tests/DynamoDb.ExpressionMapping.Benchmarks -- --filter "*ProjectionBuilder*"

# Export results
dotnet run -c Release --project tests/DynamoDb.ExpressionMapping.Benchmarks -- --exporters json csv
```

## Baseline Tracking

### PR-04.9: Baseline Workflow

1. Run benchmarks on `main` branch → save results as baseline JSON
2. Run benchmarks on PR branch → compare against baseline
3. Flag regressions exceeding thresholds

Regression thresholds:

| Metric              | Threshold                 |
| ------------------- | ------------------------- |
| Mean execution time | > 20% regression          |
| Memory allocation   | > 50% regression          |
| Gen0 GC collections | Any increase on hot paths |

### PR-04.10: CI Integration

```yaml
# .github/workflows/benchmarks.yml
name: Benchmarks
on:
  pull_request:
    paths:
      - 'src/DynamoDb.ExpressionMapping/**'
  workflow_dispatch:

jobs:
  benchmark:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet run -c Release --project tests/DynamoDb.ExpressionMapping.Benchmarks -- --exporters json
      - uses: actions/upload-artifact@v4
        with:
          name: benchmark-results
          path: BenchmarkDotNet.Artifacts/
```

## Success Criteria

- All benchmark classes run without errors on .NET 8
- Warm path is measurably faster than cold path (validates caching)
- Direct result mapping is within 2x of hand-written mapping (validates compiled delegates)
- No single expression build operation allocates more than 10KB on the warm path
- Baseline results committed and tracked across releases
