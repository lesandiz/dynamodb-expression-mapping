using System.Diagnostics;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.SoakTests.Metrics;
using DynamoDb.ExpressionMapping.SoakTests.Workloads;
using Spectre.Console;

namespace DynamoDb.ExpressionMapping.SoakTests;

/// <summary>
/// Orchestrates soak test execution across three phases: warm-up, sustained load, and cool-down.
/// Manages concurrent worker tasks and collects metrics throughout the test run.
/// </summary>
public sealed class SoakTestRunner
{
    private readonly SoakTestConfig _config;
    private readonly MetricsCollector _metricsCollector;
    private readonly MemoryMonitor _memoryMonitor;
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly SharedDependencies _sharedDependencies;

    public SoakTestRunner(SoakTestConfig config, IAmazonDynamoDB? dynamoDb = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _metricsCollector = new MetricsCollector();
        _memoryMonitor = new MemoryMonitor(_metricsCollector);

        // Create DynamoDB client if not provided (for testing)
        _dynamoDb = dynamoDb ?? CreateDynamoDbClient();
        _tableName = "SoakTestOrders";
        _sharedDependencies = new SharedDependencies();
    }

    /// <summary>
    /// Runs the complete soak test: warm-up, sustained load, and cool-down phases.
    /// Returns true if the test passed all success criteria.
    /// </summary>
    public async Task<SoakTestResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();

        try
        {
            AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════[/]");
            AnsiConsole.MarkupLine($"[bold cyan]  Soak Test — {_config.SustainedDuration.TotalMinutes:F0} min, {_config.ConcurrentWorkers} workers[/]");
            AnsiConsole.MarkupLine($"[bold cyan]  Workload: {_config.WorkloadType}[/]");
            AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════[/]");
            AnsiConsole.WriteLine();

            // Initialize DynamoDB table (in real implementation, this would create/seed table)
            await InitializeTableAsync(cancellationToken);

            // Phase 1: Warm-up
            await RunPhaseAsync(
                "Warm-up",
                _config.WarmupDuration,
                cancellationToken
            );

            // Capture baseline memory after warm-up
            var baselineSnapshot = _metricsCollector.GetSnapshot();

            // Phase 2: Sustained Load
            await RunPhaseAsync(
                "Sustained Load",
                _config.SustainedDuration,
                cancellationToken
            );

            // Phase 3: Cool-down
            await RunPhaseAsync(
                "Cool-down",
                _config.CooldownDuration,
                cancellationToken
            );

            overallStopwatch.Stop();

            // Collect final metrics
            var finalSnapshot = _metricsCollector.GetSnapshot();
            var memoryAnalysis = _memoryMonitor.Analyze(TimeSpan.FromMinutes(5));

            // Determine pass/fail
            var result = EvaluateResults(finalSnapshot, memoryAnalysis, baselineSnapshot);
            PrintResults(result, overallStopwatch.Elapsed);

            return result;
        }
        finally
        {
            _memoryMonitor.Dispose();
            _metricsCollector.Dispose();
            _dynamoDb?.Dispose();
        }
    }

    /// <summary>
    /// Runs a single phase of the soak test.
    /// </summary>
    private async Task RunPhaseAsync(string phaseName, TimeSpan duration, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[bold yellow]▶ Phase: {phaseName}[/] [dim]({duration.TotalMinutes:F1} min)[/]");

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            )
            .StartAsync(async ctx =>
            {
                var progressTask = ctx.AddTask($"[green]{phaseName}[/]");
                progressTask.MaxValue = duration.TotalSeconds;

                var phaseStopwatch = Stopwatch.StartNew();
                var workerTasks = new List<Task>();

                // Start concurrent workers
                for (int i = 0; i < _config.ConcurrentWorkers; i++)
                {
                    var workerId = i;
                    workerTasks.Add(Task.Run(async () =>
                    {
                        await WorkerLoop(workerId, phaseStopwatch, duration, cancellationToken);
                    }, cancellationToken));
                }

                // Update active thread count
                _metricsCollector.UpdateActiveThreads(_config.ConcurrentWorkers);

                // Update progress bar while workers run
                while (phaseStopwatch.Elapsed < duration && !cancellationToken.IsCancellationRequested)
                {
                    progressTask.Value = phaseStopwatch.Elapsed.TotalSeconds;
                    await Task.Delay(100, cancellationToken);
                }

                // Signal workers to stop
                await Task.WhenAll(workerTasks);

                // Mark phase complete
                progressTask.Value = progressTask.MaxValue;
                _metricsCollector.UpdateActiveThreads(0);
            });

        AnsiConsole.MarkupLine($"[dim]  ✓ {phaseName} complete[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Worker loop that executes operations for the duration of a phase.
    /// </summary>
    private async Task WorkerLoop(int workerId, Stopwatch phaseStopwatch, TimeSpan duration, CancellationToken cancellationToken)
    {
        // Create workload instance for this worker
        var workload = WorkloadFactory.CreateWorkload(
            _config.WorkloadType,
            _dynamoDb,
            _tableName,
            _metricsCollector,
            _sharedDependencies);

        while (phaseStopwatch.Elapsed < duration && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var operationStopwatch = Stopwatch.StartNew();

                // Execute workload
                await workload.ExecuteAsync(cancellationToken);

                operationStopwatch.Stop();
                _metricsCollector.RecordOperation(operationStopwatch.Elapsed.TotalMilliseconds);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                // Record failure but continue
                _metricsCollector.RecordFailure();
                Debug.WriteLine($"Worker {workerId} error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Initializes DynamoDB table for soak testing.
    /// </summary>
    private async Task InitializeTableAsync(CancellationToken cancellationToken)
    {
        // In a real implementation, this would:
        // 1. Create the table if it doesn't exist
        // 2. Seed it with test data
        // For now, just a placeholder
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a DynamoDB client configured for local testing.
    /// </summary>
    private static IAmazonDynamoDB CreateDynamoDbClient()
    {
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = "http://localhost:8004"  // Port from docker-compose.yml
        };

        return new AmazonDynamoDBClient(config);
    }

    /// <summary>
    /// Evaluates test results against success criteria.
    /// </summary>
    private SoakTestResult EvaluateResults(
        MetricsSnapshot finalSnapshot,
        MemoryAnalysis memoryAnalysis,
        MetricsSnapshot baselineSnapshot)
    {
        var failures = new List<string>();

        // Criterion 1: Zero operation failures
        if (finalSnapshot.FailedOperations > 0)
        {
            failures.Add($"Operation failures detected: {finalSnapshot.FailedOperations}");
        }

        // Criterion 2: Memory delta < 20% after warm-up
        if (memoryAnalysis.DeltaPercent > 20.0)
        {
            failures.Add($"Memory growth exceeded 20%: {memoryAnalysis.DeltaPercent:F1}%");
        }

        // Criterion 3: Gen2 GC collections < 10 during sustained phase
        if (finalSnapshot.GC.Gen2 > 10)
        {
            failures.Add($"Excessive Gen2 GC collections: {finalSnapshot.GC.Gen2}");
        }

        // Criterion 4: Memory leak detection
        if (memoryAnalysis.IsLeakDetected)
        {
            failures.Add($"Memory leak detected: {memoryAnalysis.SlopeBytesPerSecond:F0} bytes/sec sustained growth");
        }

        var passed = failures.Count == 0;

        return new SoakTestResult(
            Passed: passed,
            Metrics: finalSnapshot,
            MemoryAnalysis: memoryAnalysis,
            Failures: failures.AsReadOnly()
        );
    }

    /// <summary>
    /// Prints formatted test results to the console.
    /// </summary>
    private void PrintResults(SoakTestResult result, TimeSpan totalDuration)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine($"[bold cyan]  Soak Test Results — {totalDuration.TotalMinutes:F1} min, {_config.ConcurrentWorkers} workers[/]");
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════[/]");

        var throughput = totalDuration.TotalSeconds > 0
            ? result.Metrics.TotalOperations / totalDuration.TotalSeconds
            : 0;

        AnsiConsole.MarkupLine($"  [bold]Operations:[/]     {result.Metrics.TotalOperations:N0} total, {result.Metrics.FailedOperations:N0} failed");
        AnsiConsole.MarkupLine($"  [bold]Throughput:[/]     {throughput:F1} ops/sec");
        AnsiConsole.MarkupLine($"  [bold]Latency:[/]        p50={result.Metrics.Latency.P50:F1}ms  p95={result.Metrics.Latency.P95:F1}ms  p99={result.Metrics.Latency.P99:F1}ms");

        var startMB = result.MemoryAnalysis.StartMemoryBytes / (1024.0 * 1024.0);
        var endMB = result.MemoryAnalysis.EndMemoryBytes / (1024.0 * 1024.0);
        var deltaMB = (result.MemoryAnalysis.EndMemoryBytes - result.MemoryAnalysis.StartMemoryBytes) / (1024.0 * 1024.0);

        AnsiConsole.MarkupLine($"  [bold]Memory:[/]         Start={startMB:F1}MB  End={endMB:F1}MB  Delta={deltaMB:+0.0;-0.0;0.0}MB");
        AnsiConsole.MarkupLine($"  [bold]GC:[/]             Gen0={result.Metrics.GC.Gen0:N0}  Gen1={result.Metrics.GC.Gen1:N0}  Gen2={result.Metrics.GC.Gen2:N0}");
        AnsiConsole.MarkupLine($"  [bold]Cache:[/]          Entries={result.Metrics.CacheEntries:N0}  HitRatio={result.Metrics.CacheHitRatio:F1}%");

        if (result.Passed)
        {
            AnsiConsole.MarkupLine("  [bold green]Result:[/]         PASS");
        }
        else
        {
            AnsiConsole.MarkupLine("  [bold red]Result:[/]         FAIL");
            foreach (var failure in result.Failures)
            {
                AnsiConsole.MarkupLine($"    [red]✗[/] {failure}");
            }
        }

        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════[/]");
    }
}

/// <summary>
/// Configuration for a soak test run.
/// </summary>
public record SoakTestConfig(
    TimeSpan WarmupDuration,
    TimeSpan SustainedDuration,
    TimeSpan CooldownDuration,
    int ConcurrentWorkers,
    string WorkloadType = "mixed"
)
{
    public static SoakTestConfig Default => new(
        WarmupDuration: TimeSpan.FromMinutes(2),
        SustainedDuration: TimeSpan.FromMinutes(10),
        CooldownDuration: TimeSpan.FromMinutes(1),
        ConcurrentWorkers: 8
    );
}

/// <summary>
/// Result of a soak test run.
/// </summary>
public record SoakTestResult(
    bool Passed,
    MetricsSnapshot Metrics,
    MemoryAnalysis MemoryAnalysis,
    IReadOnlyList<string> Failures
);
