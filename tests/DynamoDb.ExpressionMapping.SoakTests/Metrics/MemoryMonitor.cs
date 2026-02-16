using System.Diagnostics;

namespace DynamoDb.ExpressionMapping.SoakTests.Metrics;

/// <summary>
/// Monitors memory usage over time to detect memory leaks.
/// Samples GC.GetTotalMemory at regular intervals and computes linear regression
/// to detect sustained growth patterns.
/// </summary>
public sealed class MemoryMonitor : IDisposable
{
    private readonly MetricsCollector _metricsCollector;
    private readonly TimeSpan _sampleInterval;
    private readonly CancellationTokenSource _cts;
    private readonly Task _monitorTask;
    private readonly List<MemorySample> _samples = new();
    private readonly object _lock = new();
    private const int MaxSamples = 500; // Bounded retention: ~41 minutes at 5-second intervals

    public MemoryMonitor(MetricsCollector metricsCollector, TimeSpan? sampleInterval = null)
    {
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        _sampleInterval = sampleInterval ?? TimeSpan.FromSeconds(5);
        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
    }

    /// <summary>
    /// Background monitoring loop that samples memory at regular intervals.
    /// </summary>
    private async Task MonitorLoop(CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var memoryBytes = GC.GetTotalMemory(forceFullCollection: false);
                var elapsed = DateTime.UtcNow - startTime;

                lock (_lock)
                {
                    _samples.Add(new MemorySample(elapsed, memoryBytes));

                    // Bounded retention: keep only last MaxSamples samples
                    if (_samples.Count > MaxSamples)
                    {
                        _samples.RemoveAt(0);
                    }
                }

                // Update metrics collector
                _metricsCollector.UpdateMemoryUsage(memoryBytes);
                _metricsCollector.SampleGC();

                await Task.Delay(_sampleInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MemoryMonitor error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Analyzes memory samples to detect potential memory leaks.
    /// Returns true if sustained growth is detected.
    /// </summary>
    public MemoryAnalysis Analyze(TimeSpan sustainedThreshold)
    {
        lock (_lock)
        {
            if (_samples.Count < 2)
                return new MemoryAnalysis(false, 0, 0, 0, 0);

            var startMemory = _samples[0].MemoryBytes;
            var endMemory = _samples[^1].MemoryBytes;
            var totalDuration = _samples[^1].Elapsed.TotalSeconds;

            // Compute linear regression slope (bytes per second)
            var slope = ComputeLinearRegressionSlope(_samples);

            // Check for sustained growth over the threshold period
            var sustainedMinutes = sustainedThreshold.TotalMinutes;
            var sustainedGrowth = DetectSustainedGrowth(_samples, sustainedThreshold, slope);

            var deltaBytes = endMemory - startMemory;
            var deltaPercent = startMemory == 0 ? 0 : (deltaBytes / (double)startMemory) * 100.0;

            return new MemoryAnalysis(
                IsLeakDetected: sustainedGrowth,
                StartMemoryBytes: startMemory,
                EndMemoryBytes: endMemory,
                DeltaPercent: deltaPercent,
                SlopeBytesPerSecond: slope
            );
        }
    }

    /// <summary>
    /// Computes the slope of memory growth using linear regression.
    /// </summary>
    private static double ComputeLinearRegressionSlope(List<MemorySample> samples)
    {
        if (samples.Count < 2)
            return 0;

        var n = samples.Count;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumX2 = 0.0;

        for (int i = 0; i < n; i++)
        {
            var x = samples[i].Elapsed.TotalSeconds;
            var y = samples[i].MemoryBytes;

            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
        }

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        return slope;
    }

    /// <summary>
    /// Detects if memory growth is sustained over a minimum threshold period.
    /// Uses a sliding window to check if the slope remains positive for consecutive samples.
    /// </summary>
    private static bool DetectSustainedGrowth(List<MemorySample> samples, TimeSpan threshold, double overallSlope)
    {
        // Threshold for sustained growth detection (bytes/second)
        const double MinSlopeThreshold = 1024; // 1 KB/s minimum growth rate

        if (overallSlope < MinSlopeThreshold)
            return false;

        // Check for sustained growth over the threshold period
        var thresholdSeconds = threshold.TotalSeconds;
        var consecutiveSamples = 0;
        var requiredConsecutiveSamples = (int)(thresholdSeconds / 5); // Assuming 5-second intervals

        for (int i = 1; i < samples.Count; i++)
        {
            if (samples[i].MemoryBytes > samples[i - 1].MemoryBytes)
            {
                consecutiveSamples++;
                if (consecutiveSamples >= requiredConsecutiveSamples)
                    return true;
            }
            else
            {
                consecutiveSamples = 0;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets all memory samples collected so far.
    /// </summary>
    public IReadOnlyList<MemorySample> GetSamples()
    {
        lock (_lock)
        {
            return _samples.ToList();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _monitorTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        finally
        {
            _cts.Dispose();
        }
    }
}

/// <summary>
/// A single memory sample at a point in time.
/// </summary>
public record MemorySample(TimeSpan Elapsed, long MemoryBytes);

/// <summary>
/// Result of memory leak analysis.
/// </summary>
public record MemoryAnalysis(
    bool IsLeakDetected,
    long StartMemoryBytes,
    long EndMemoryBytes,
    double DeltaPercent,
    double SlopeBytesPerSecond
);
