# PR-02: Soak & Concurrency Testing

## Motivation

Unit and integration tests verify correctness at small scale. They do not expose:
- Memory leaks from cached delegates or expression results accumulating over time
- Thread-safety issues in `ExpressionCache`, `AttributeValueConverterRegistry`, or `DirectResultMapper` under concurrent access
- Resource exhaustion from repeated container startup/teardown
- Subtle race conditions in alias generation or re-aliasing during composition

A soak test harness simulates sustained production-like usage to surface these issues.

## Scope

A standalone test harness (console application) that exercises all library subsystems concurrently against DynamoDB Local over an extended period, collecting metrics on memory, throughput, errors, and latency.

## Project Structure

```
tests/
└── DynamoDb.ExpressionMapping.SoakTests/
    ├── DynamoDb.ExpressionMapping.SoakTests.csproj
    ├── Program.cs                          # Entry point, CLI args, reporting
    ├── SoakTestRunner.cs                   # Orchestrates workload phases
    ├── Workloads/
    │   ├── ProjectionWorkload.cs           # Projection build + DynamoDB query + result mapping
    │   ├── FilterWorkload.cs               # Filter build + composition + query
    │   ├── UpdateWorkload.cs               # Update build + execute
    │   ├── KeyConditionWorkload.cs         # Key condition build + query
    │   ├── MixedWorkload.cs                # Random mix of all operations
    │   └── CacheStressWorkload.cs          # Distinct expressions to stress cache growth
    ├── Metrics/
    │   ├── MetricsCollector.cs             # In-process metrics aggregation
    │   └── MemoryMonitor.cs                # Periodic GC.GetTotalMemory snapshots
    └── docker-compose.yml                  # DynamoDB Local for soak runs
```

## Dependencies

- `AWSSDK.DynamoDBv2`
- `Bogus` — diverse test data generation
- `System.Diagnostics.Metrics` — .NET Metrics API for collection
- `Spectre.Console` — terminal progress/reporting

## Workload Design

### PR-02.1: Workload Phases

Each soak run executes three phases:

| Phase          | Duration                 | Purpose                                                   |
| -------------- | ------------------------ | --------------------------------------------------------- |
| Warm-up        | 2 min                    | Populate caches, seed DynamoDB tables, establish baseline |
| Sustained load | 10-30 min (configurable) | Concurrent operations at target throughput                |
| Cool-down      | 1 min                    | Drain in-flight operations, collect final metrics         |

### PR-02.2: Concurrency Model

```
┌─────────────────────────────────────────────┐
│              SoakTestRunner                  │
│                                             │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐    │
│  │ Thread 1 │ │ Thread 2 │ │ Thread N │    │
│  │ Mixed    │ │ Filter   │ │ Cache    │    │
│  │ Workload │ │ Workload │ │ Stress   │    │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘    │
│       │            │            │           │
│       └────────────┼────────────┘           │
│                    │                        │
│         ┌──────────▼──────────┐             │
│         │  MetricsCollector   │             │
│         │  MemoryMonitor      │             │
│         └─────────────────────┘             │
└─────────────────────────────────────────────┘
```

- Default: 8 concurrent worker tasks (configurable via `--concurrency N`)
- Each worker randomly selects a workload per iteration
- Workers share the same `DynamoDbExpressionConfig`, `ExpressionCache`, and `AttributeValueConverterRegistry` instances — simulating real DI container scoping

### PR-02.3: Data Generation

Use `Bogus` to generate diverse entities per iteration:

- Randomised property values (strings, GUIDs, enums, dates, nested objects)
- Varying collection sizes (0, 1, 10, 100 elements in lists/sets)
- Nullable fields randomly present or absent
- Entity count: seed 1,000 items across 10 partition keys

### PR-02.4: Operation Mix

Default distribution per worker iteration:

| Operation                      | Weight | Description                                           |
| ------------------------------ | ------ | ----------------------------------------------------- |
| Query with projection + filter | 30%    | Build projection + filter, execute query, map results |
| Query with key condition       | 20%    | Build key condition + optional filter, query          |
| Scan with filter               | 15%    | Build filter, execute scan                            |
| Update                         | 15%    | Build update expression, execute update               |
| Filter composition             | 10%    | Build two filters independently, compose with And/Or  |
| Cache stress                   | 10%    | Build expression with unique selector (cache miss)    |

## Metrics Collection

### PR-02.5: Tracked Metrics

| Metric                  | Type                     | Alert Threshold                    |
| ----------------------- | ------------------------ | ---------------------------------- |
| `operations_total`      | Counter                  | —                                  |
| `operations_failed`     | Counter                  | > 0 fails soak                     |
| `operation_duration_ms` | Histogram                | p99 > 50ms (expression build only) |
| `memory_bytes`          | Gauge (sampled every 5s) | Sustained growth > 20% over 10min  |
| `gc_gen0_collections`   | Counter                  | — (baseline comparison)            |
| `gc_gen2_collections`   | Counter                  | > 10 in sustained phase            |
| `cache_entries`         | Gauge                    | Unbounded growth fails soak        |
| `cache_hit_ratio`       | Gauge                    | — (informational)                  |
| `active_threads`        | Gauge                    | — (informational)                  |

### PR-02.6: Memory Leak Detection

```csharp
public class MemoryMonitor
{
    // Sample every 5 seconds during sustained phase
    // Compute linear regression on GC.GetTotalMemory(forceFullGC: false)
    // If slope > threshold_bytes_per_second for >5 consecutive minutes → flag leak

    // Also track:
    // - GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)
    // - ExpressionCache.GetStatistics().TotalEntries over time
}
```

## Concurrency-Specific Tests

### PR-02.7: Thread-Safety Scenarios

These are targeted concurrent access patterns, run as part of the soak:

```csharp
// 1. Concurrent cache access with same expression
// Multiple threads build the same projection simultaneously
// Assert: all get identical results, no exceptions

// 2. Concurrent cache access with distinct expressions
// Multiple threads build unique expressions simultaneously
// Assert: cache grows correctly, no entries lost or corrupted

// 3. Concurrent filter composition
// Multiple threads compose filters using And()/Or() simultaneously
// Assert: no alias collisions, no shared mutable state corruption

// 4. Concurrent converter registry reads
// Multiple threads resolve converters for different types simultaneously
// Assert: correct converter returned for each type, no exceptions

// 5. Concurrent DirectResultMapper creation + execution
// Multiple threads create mappers for different projections and map results
// Assert: correct mapping, no delegate corruption
```

## CLI Interface

```bash
# Default: 10min sustained load, 8 workers
dotnet run --project tests/DynamoDb.ExpressionMapping.SoakTests

# Custom duration and concurrency
dotnet run --project tests/DynamoDb.ExpressionMapping.SoakTests -- \
    --duration 30 \
    --concurrency 16 \
    --workload mixed

# Specific workload only
dotnet run --project tests/DynamoDb.ExpressionMapping.SoakTests -- \
    --workload cache-stress \
    --duration 15
```

## Reporting

At completion, the harness prints a summary:

```
═══════════════════════════════════════════════
  Soak Test Results — 30 min, 16 workers
═══════════════════════════════════════════════
  Operations:     142,857 total, 0 failed
  Throughput:     79.4 ops/sec
  Latency:        p50=0.8ms  p95=2.1ms  p99=4.7ms
  Memory:         Start=48MB  End=52MB  Delta=+4MB
  GC:             Gen0=1,204  Gen1=89  Gen2=3
  Cache:          Entries=347  HitRatio=94.2%
  Result:         PASS
═══════════════════════════════════════════════
```

Exit code: `0` = pass, `1` = failures detected.

## CI Integration

- Run as a scheduled nightly workflow (not on every PR — too slow)
- Duration: 10 minutes in CI
- Failure blocks the release pipeline (not the PR pipeline)

```yaml
# .github/workflows/soak-tests.yml
name: Soak Tests
on:
  schedule:
    - cron: '0 3 * * *'  # 3am daily
  workflow_dispatch:

jobs:
  soak:
    runs-on: ubuntu-latest
    services:
      dynamodb:
        image: amazon/dynamodb-local:latest
        ports:
          - 8000:8000
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet run --project tests/DynamoDb.ExpressionMapping.SoakTests -- --duration 10 --concurrency 8
```

## Success Criteria

- Zero operation failures across 30-minute run with 16 workers
- Memory delta < 20% of starting allocation after warm-up
- No Gen2 GC collections > 10 during sustained phase
- Cache entry count stabilises (does not grow unboundedly)
- All thread-safety scenarios (PR-02.7) pass without exceptions
