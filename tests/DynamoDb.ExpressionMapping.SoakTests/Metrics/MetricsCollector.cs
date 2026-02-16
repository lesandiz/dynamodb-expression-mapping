using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DynamoDb.ExpressionMapping.SoakTests.Metrics;

/// <summary>
/// Collects in-process metrics during soak test execution.
/// Thread-safe for concurrent workload access.
/// </summary>
public sealed class MetricsCollector : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _operationsTotal;
    private readonly Counter<long> _operationsFailed;
    private readonly Histogram<double> _operationDuration;
    private readonly Counter<long> _gcGen0Collections;
    private readonly Counter<long> _gcGen1Collections;
    private readonly Counter<long> _gcGen2Collections;

    // Observable gauges
    private long _cacheEntries;
    private long _cacheHits;
    private long _cacheMisses;
    private long _memoryBytes;
    private int _activeThreads;

    // Thread-safe counters
    private long _totalOperations;
    private long _failedOperations;
    private readonly ConcurrentBag<double> _latencySamples = new();

    // GC baseline tracking
    private int _baselineGen0;
    private int _baselineGen1;
    private int _baselineGen2;

    public MetricsCollector()
    {
        _meter = new Meter("DynamoDb.ExpressionMapping.SoakTests");

        // Counters
        _operationsTotal = _meter.CreateCounter<long>("operations_total", description: "Total operations executed");
        _operationsFailed = _meter.CreateCounter<long>("operations_failed", description: "Failed operations");
        _operationDuration = _meter.CreateHistogram<double>("operation_duration_ms", unit: "ms", description: "Operation duration in milliseconds");
        _gcGen0Collections = _meter.CreateCounter<long>("gc_gen0_collections", description: "Generation 0 GC collections");
        _gcGen1Collections = _meter.CreateCounter<long>("gc_gen1_collections", description: "Generation 1 GC collections");
        _gcGen2Collections = _meter.CreateCounter<long>("gc_gen2_collections", description: "Generation 2 GC collections");

        // Observable gauges
        _meter.CreateObservableGauge("memory_bytes", () => _memoryBytes, unit: "bytes", description: "Current memory allocation");
        _meter.CreateObservableGauge("cache_entries", () => _cacheEntries, description: "Current cache entry count");
        _meter.CreateObservableGauge("cache_hit_ratio", () => CalculateCacheHitRatio(), description: "Cache hit ratio percentage");
        _meter.CreateObservableGauge("active_threads", () => _activeThreads, description: "Active worker threads");

        // Set GC baseline
        _baselineGen0 = GC.CollectionCount(0);
        _baselineGen1 = GC.CollectionCount(1);
        _baselineGen2 = GC.CollectionCount(2);
    }

    /// <summary>
    /// Records a successful operation execution.
    /// </summary>
    public void RecordOperation(double durationMs)
    {
        Interlocked.Increment(ref _totalOperations);
        _operationsTotal.Add(1);
        _operationDuration.Record(durationMs);
        _latencySamples.Add(durationMs);
    }

    /// <summary>
    /// Records a failed operation.
    /// </summary>
    public void RecordFailure()
    {
        Interlocked.Increment(ref _failedOperations);
        _operationsFailed.Add(1);
    }

    /// <summary>
    /// Updates memory allocation gauge (called by MemoryMonitor).
    /// </summary>
    public void UpdateMemoryUsage(long bytes)
    {
        Interlocked.Exchange(ref _memoryBytes, bytes);
    }

    /// <summary>
    /// Updates cache statistics.
    /// </summary>
    public void UpdateCacheStats(long entries, long hits, long misses)
    {
        Interlocked.Exchange(ref _cacheEntries, entries);
        Interlocked.Exchange(ref _cacheHits, hits);
        Interlocked.Exchange(ref _cacheMisses, misses);
    }

    /// <summary>
    /// Updates active thread count.
    /// </summary>
    public void UpdateActiveThreads(int count)
    {
        _activeThreads = count;
    }

    /// <summary>
    /// Samples GC collections since baseline.
    /// </summary>
    public void SampleGC()
    {
        var gen0Delta = GC.CollectionCount(0) - _baselineGen0;
        var gen1Delta = GC.CollectionCount(1) - _baselineGen1;
        var gen2Delta = GC.CollectionCount(2) - _baselineGen2;

        if (gen0Delta > 0)
        {
            _gcGen0Collections.Add(gen0Delta);
            _baselineGen0 = GC.CollectionCount(0);
        }

        if (gen1Delta > 0)
        {
            _gcGen1Collections.Add(gen1Delta);
            _baselineGen1 = GC.CollectionCount(1);
        }

        if (gen2Delta > 0)
        {
            _gcGen2Collections.Add(gen2Delta);
            _baselineGen2 = GC.CollectionCount(2);
        }
    }

    /// <summary>
    /// Gets a snapshot of current metrics for reporting.
    /// </summary>
    public MetricsSnapshot GetSnapshot()
    {
        var latencies = _latencySamples.OrderBy(x => x).ToArray();
        var totalGc = new GCStats(
            GC.CollectionCount(0) - _baselineGen0,
            GC.CollectionCount(1) - _baselineGen1,
            GC.CollectionCount(2) - _baselineGen2
        );

        return new MetricsSnapshot(
            TotalOperations: _totalOperations,
            FailedOperations: _failedOperations,
            MemoryBytes: _memoryBytes,
            CacheEntries: _cacheEntries,
            CacheHitRatio: CalculateCacheHitRatio(),
            GC: totalGc,
            Latency: CalculateLatencyPercentiles(latencies)
        );
    }

    private double CalculateCacheHitRatio()
    {
        var total = _cacheHits + _cacheMisses;
        return total == 0 ? 0.0 : (_cacheHits / (double)total) * 100.0;
    }

    private static LatencyStats CalculateLatencyPercentiles(double[] sortedLatencies)
    {
        if (sortedLatencies.Length == 0)
            return new LatencyStats(0, 0, 0, 0);

        return new LatencyStats(
            P50: Percentile(sortedLatencies, 0.50),
            P95: Percentile(sortedLatencies, 0.95),
            P99: Percentile(sortedLatencies, 0.99),
            Max: sortedLatencies[^1]
        );
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
            return 0;

        var index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        index = Math.Max(0, Math.Min(sortedValues.Length - 1, index));
        return sortedValues[index];
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}

/// <summary>
/// Immutable snapshot of metrics at a point in time.
/// </summary>
public record MetricsSnapshot(
    long TotalOperations,
    long FailedOperations,
    long MemoryBytes,
    long CacheEntries,
    double CacheHitRatio,
    GCStats GC,
    LatencyStats Latency
);

public record GCStats(int Gen0, int Gen1, int Gen2);

public record LatencyStats(double P50, double P95, double P99, double Max);
