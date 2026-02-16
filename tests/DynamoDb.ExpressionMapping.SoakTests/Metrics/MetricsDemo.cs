using System.Diagnostics;

namespace DynamoDb.ExpressionMapping.SoakTests.Metrics;

/// <summary>
/// Demo program to verify MetricsCollector and MemoryMonitor functionality.
/// </summary>
internal static class MetricsDemo
{
    public static async Task RunDemo()
    {
        using var collector = new MetricsCollector();
        using var monitor = new MemoryMonitor(collector, sampleInterval: TimeSpan.FromMilliseconds(500));

        Console.WriteLine("Running metrics demo for 5 seconds...");

        // Simulate some work
        var random = new Random();
        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();
            await Task.Delay(random.Next(10, 50)); // Simulate operation
            sw.Stop();

            collector.RecordOperation(sw.Elapsed.TotalMilliseconds);

            // Simulate occasional failures
            if (i % 20 == 0)
                collector.RecordFailure();

            // Simulate cache stats
            collector.UpdateCacheStats(entries: i, hits: i * 8, misses: i * 2);
            collector.UpdateActiveThreads(random.Next(1, 9));
        }

        // Wait for a few memory samples
        await Task.Delay(3000);

        // Get snapshot
        var snapshot = collector.GetSnapshot();
        Console.WriteLine($"\nMetrics Snapshot:");
        Console.WriteLine($"  Total Operations: {snapshot.TotalOperations}");
        Console.WriteLine($"  Failed Operations: {snapshot.FailedOperations}");
        Console.WriteLine($"  Memory: {snapshot.MemoryBytes / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"  Cache Entries: {snapshot.CacheEntries}");
        Console.WriteLine($"  Cache Hit Ratio: {snapshot.CacheHitRatio:F1}%");
        Console.WriteLine($"  GC: Gen0={snapshot.GC.Gen0}, Gen1={snapshot.GC.Gen1}, Gen2={snapshot.GC.Gen2}");
        Console.WriteLine($"  Latency: P50={snapshot.Latency.P50:F2}ms, P95={snapshot.Latency.P95:F2}ms, P99={snapshot.Latency.P99:F2}ms, Max={snapshot.Latency.Max:F2}ms");

        // Analyze memory
        var analysis = monitor.Analyze(TimeSpan.FromSeconds(2));
        Console.WriteLine($"\nMemory Analysis:");
        Console.WriteLine($"  Start: {analysis.StartMemoryBytes / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"  End: {analysis.EndMemoryBytes / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"  Delta: {analysis.DeltaPercent:F2}%");
        Console.WriteLine($"  Slope: {analysis.SlopeBytesPerSecond:F2} bytes/sec");
        Console.WriteLine($"  Leak Detected: {analysis.IsLeakDetected}");

        Console.WriteLine($"\nSamples collected: {monitor.GetSamples().Count}");
    }
}
