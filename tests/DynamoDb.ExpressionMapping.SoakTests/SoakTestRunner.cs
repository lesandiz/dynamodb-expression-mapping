using System.Diagnostics;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;
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
    private long _operationCounter = 0;
    private long _lastErrorSummaryAt = 0;
    private readonly string _errorLogPath;
    private readonly object _errorLogLock = new object();

    public SoakTestRunner(SoakTestConfig config, IAmazonDynamoDB? dynamoDb = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _metricsCollector = new MetricsCollector();
        _memoryMonitor = new MemoryMonitor(_metricsCollector);

        // Create DynamoDB client if not provided (for testing)
        _dynamoDb = dynamoDb ?? CreateDynamoDbClient();
        _tableName = "SoakTestOrders";
        _sharedDependencies = new SharedDependencies();
        _errorLogPath = Path.Combine(Path.GetTempPath(), $"soak-test-errors-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
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

            // Force GC to reduce memory baseline after warm-up
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            // Sync memory reading immediately so baseline isn't stale from MemoryMonitor's 5s interval
            _metricsCollector.UpdateMemoryUsage(GC.GetTotalMemory(forceFullCollection: false));
            // Capture baseline memory after warm-up
            var baselineSnapshot = _metricsCollector.GetSnapshot();

            // Phase 2: Sustained Load
            await RunPhaseAsync(
                "Sustained Load",
                _config.SustainedDuration,
                cancellationToken
            );

            // Force GC between sustained and cool-down phases
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Phase 3: Cool-down
            await RunPhaseAsync(
                "Cool-down",
                _config.CooldownDuration,
                cancellationToken
            );

            overallStopwatch.Stop();

            // Force GC before final snapshot for symmetric comparison with baseline
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            _metricsCollector.UpdateMemoryUsage(GC.GetTotalMemory(forceFullCollection: false));

            // Collect final metrics
            var finalSnapshot = _metricsCollector.GetSnapshot();
            var memoryAnalysis = _memoryMonitor.Analyze(TimeSpan.FromMinutes(5));

            // Determine pass/fail
            var result = EvaluateResults(finalSnapshot, memoryAnalysis, baselineSnapshot);
            PrintResults(result, overallStopwatch.Elapsed);

            AnsiConsole.MarkupLine($"[dim]Error log: {_errorLogPath}[/]");

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

                // Add small delay to prevent tight loop (1-5ms)
                await Task.Delay(Random.Shared.Next(1, 6), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (AmazonDynamoDBException dbEx)
            {
                // DynamoDB service errors
                _metricsCollector.RecordFailure("DynamoDB");
                var errorType = dbEx.GetType().Name;
                Debug.WriteLine($"[Worker {workerId}] DynamoDB error ({errorType}): {dbEx.Message}");
                Debug.WriteLine($"[Worker {workerId}] Stack trace: {dbEx.StackTrace}");
                LogErrorToFile(workerId, dbEx);

                // Log to console for critical errors (throttling, service unavailable)
                if (dbEx.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                    dbEx.ErrorCode == "ProvisionedThroughputExceededException")
                {
                    Console.Error.WriteLine($"[CRITICAL] Worker {workerId} - {errorType}: {dbEx.Message}");
                }
            }
            catch (ExpressionMappingException expEx)
            {
                // Expression builder errors
                _metricsCollector.RecordFailure("ExpressionMapping");
                var errorType = expEx.GetType().Name;
                Debug.WriteLine($"[Worker {workerId}] Expression mapping error ({errorType}): {expEx.Message}");
                Debug.WriteLine($"[Worker {workerId}] Stack trace: {expEx.StackTrace}");
                Console.Error.WriteLine($"[CRITICAL] Worker {workerId} - {errorType}: {expEx.Message}");
            }
            catch (InvalidOperationException invEx)
            {
                // Invalid operation errors (likely workload issues)
                _metricsCollector.RecordFailure("InvalidOperation");
                Debug.WriteLine($"[Worker {workerId}] Invalid operation: {invEx.Message}");
                Debug.WriteLine($"[Worker {workerId}] Stack trace: {invEx.StackTrace}");
                LogErrorToFile(workerId, invEx);
            }
            catch (Exception ex)
            {
                // Unexpected errors
                _metricsCollector.RecordFailure("Other");
                var errorType = ex.GetType().Name;
                Debug.WriteLine($"[Worker {workerId}] Unexpected error ({errorType}): {ex.Message}");
                Debug.WriteLine($"[Worker {workerId}] Stack trace: {ex.StackTrace}");
                Console.Error.WriteLine($"[ERROR] Worker {workerId} - Unexpected {errorType}: {ex.Message}");
            }
            finally
            {
                // Periodic error summary every 1000 operations
                var currentOps = Interlocked.Increment(ref _operationCounter);
                if (currentOps % 1000 == 0)
                {
                    LogErrorSummary(currentOps);
                }
            }
        }
    }

    /// <summary>
    /// Logs detailed error information to file for post-run analysis.
    /// </summary>
    private void LogErrorToFile(int workerId, Exception ex)
    {
        lock (_errorLogLock)
        {
            try
            {
                var logEntry = $@"
================================================================================
Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}
Worker ID: {workerId}
Exception Type: {ex.GetType().FullName}
Message: {ex.Message}
Stack Trace:
{ex.StackTrace}
================================================================================
";
                File.AppendAllText(_errorLogPath, logEntry);
            }
            catch
            {
                // Suppress file I/O errors to avoid impacting test execution
            }
        }
    }

    /// <summary>
    /// Logs periodic error summary to Debug output.
    /// </summary>
    private void LogErrorSummary(long currentOps)
    {
        var lastSummary = Interlocked.Read(ref _lastErrorSummaryAt);
        if (currentOps - lastSummary < 1000)
        {
            return; // Avoid duplicate summaries from concurrent workers
        }

        if (Interlocked.CompareExchange(ref _lastErrorSummaryAt, currentOps, lastSummary) == lastSummary)
        {
            var errorsByCategory = _metricsCollector.GetErrorsByCategory();
            if (errorsByCategory.Count > 0)
            {
                Debug.WriteLine($"[Error Summary @ {currentOps} ops]");
                foreach (var (category, count) in errorsByCategory.OrderByDescending(kvp => kvp.Value))
                {
                    Debug.WriteLine($"  {category}: {count}");
                }
            }
        }
    }

    /// <summary>
    /// Initializes DynamoDB table for soak testing.
    /// </summary>
    private async Task InitializeTableAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[dim]Initializing DynamoDB table...[/]");

        // 1. Drop and recreate table for a clean state
        await RecreateTableAsync(cancellationToken);

        // 2. Seed with test data
        await SeedTestDataAsync(cancellationToken);

        AnsiConsole.MarkupLine("[dim]  ✓ Table initialized with 1000 test orders[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Drops and recreates the SoakTestOrders table for a clean starting state.
    /// </summary>
    private async Task RecreateTableAsync(CancellationToken cancellationToken)
    {
        // Drop existing table if present
        try
        {
            await _dynamoDb.DeleteTableAsync(_tableName, cancellationToken);
            // Wait for deletion to complete
            while (true)
            {
                try
                {
                    await _dynamoDb.DescribeTableAsync(_tableName, cancellationToken);
                    await Task.Delay(500, cancellationToken);
                }
                catch (ResourceNotFoundException)
                {
                    break; // Table is gone
                }
            }
        }
        catch (ResourceNotFoundException)
        {
            // Table doesn't exist — nothing to drop
        }

        // Create fresh table
        await _dynamoDb.CreateTableAsync(new CreateTableRequest
        {
            TableName = _tableName,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement { AttributeName = "PK", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "SK", KeyType = KeyType.RANGE }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition { AttributeName = "PK", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "SK", AttributeType = ScalarAttributeType.S }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        }, cancellationToken);

        // Wait for table to become active
        while (true)
        {
            var response = await _dynamoDb.DescribeTableAsync(_tableName, cancellationToken);
            if (response.Table.TableStatus == TableStatus.ACTIVE)
                break;

            await Task.Delay(500, cancellationToken);
        }
    }

    /// <summary>
    /// Seeds the table with 1000 test orders using Bogus.
    /// </summary>
    private async Task SeedTestDataAsync(CancellationToken cancellationToken)
    {
        var faker = new Bogus.Faker();
        var orders = new List<Dictionary<string, AttributeValue>>();

        var statuses = new[] { "Processing", "Shipped", "Delivered", "Cancelled" };
        var priorities = Enum.GetValues<OrderPriority>();
        var productNames = new[] { "Laptop", "Book", "Monitor", "Keyboard", "Mouse", "Desk", "Chair", "Headphones", "Tablet", "Phone" };
        var customerIds = Enumerable.Range(1, 100).Select(i => $"CUST{i:D4}").ToArray();

        // Generate 1000 orders
        for (int i = 0; i < 1000; i++)
        {
            // Deterministic assignment: 10 orders per customer (CUST0001: ORD0-9, CUST0002: ORD10-19, etc.)
            var customerNum = (i / 10) + 1;
            var customerId = $"CUST{customerNum:D4}";
            var orderId = $"ORD{i:D6}";
            var status = faker.PickRandom(statuses);
            var priority = faker.PickRandom(priorities);
            var createdAt = faker.Date.Between(DateTime.UtcNow.AddDays(-90), DateTime.UtcNow);

            var item = new Dictionary<string, AttributeValue>
            {
                ["CustomerId"] = new AttributeValue { S = customerId },
                ["OrderId"] = new AttributeValue { S = orderId },
                ["PK"] = new AttributeValue { S = $"CUSTOMER#{customerId}" },
                ["SK"] = new AttributeValue { S = $"ORDER#{orderId}" },
                ["Name"] = new AttributeValue { S = faker.PickRandom(productNames) },
                ["Status"] = new AttributeValue { S = status },
                ["TotalAmount"] = new AttributeValue { N = faker.Finance.Amount(10, 2000, 2).ToString() },
                ["TotalCurrency"] = new AttributeValue { S = "USD" },
                ["Quantity"] = new AttributeValue { N = faker.Random.Int(1, 10).ToString() },
                ["ShippingStreet"] = new AttributeValue { S = faker.Address.StreetAddress() },
                ["ShippingCity"] = new AttributeValue { S = faker.Address.City() },
                ["ShippingPostCode"] = new AttributeValue { S = faker.Address.ZipCode() },
                ["Tags"] = new AttributeValue { L = Enumerable.Range(0, faker.Random.Int(1, 5)).Select(_ => new AttributeValue { S = faker.Commerce.Categories(1)[0] }).Distinct(new AttributeValueComparer()).ToList() },
                ["CreatedAt"] = new AttributeValue { S = createdAt.ToString("O") },
                ["IsGift"] = new AttributeValue { BOOL = faker.Random.Bool() },
                ["Priority"] = new AttributeValue { N = ((int)priority).ToString() }
            };

            // Add optional fields
            if (faker.Random.Bool(0.7f))
            {
                item["Notes"] = new AttributeValue { S = faker.Lorem.Sentence() };
            }

            if (status == "Shipped" || status == "Delivered")
            {
                item["ShippedAt"] = new AttributeValue { S = createdAt.AddDays(faker.Random.Int(1, 5)).ToString("O") };
            }

            if (faker.Random.Bool(0.3f))
            {
                item["Metadata"] = new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["Source"] = new AttributeValue { S = faker.PickRandom("Web", "Mobile", "API") },
                        ["Campaign"] = new AttributeValue { S = faker.Lorem.Word() }
                    }
                };
            }

            orders.Add(item);
        }

        // Batch write in chunks of 25 (DynamoDB limit)
        foreach (var batch in orders.Chunk(25))
        {
            var writeRequests = batch.Select(item => new WriteRequest
            {
                PutRequest = new PutRequest { Item = item }
            }).ToList();

            await _dynamoDb.BatchWriteItemAsync(new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [_tableName] = writeRequests
                }
            }, cancellationToken);
        }
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
        // Use post-warm-up baseline instead of process start
        var memoryDelta = finalSnapshot.MemoryBytes - baselineSnapshot.MemoryBytes;
        var memoryDeltaPercent = baselineSnapshot.MemoryBytes == 0 ? 0 : (memoryDelta / (double)baselineSnapshot.MemoryBytes) * 100.0;

        if (memoryDeltaPercent > 20.0)
        {
            failures.Add($"Memory growth exceeded 20%: {memoryDeltaPercent:F1}%");
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

        // Display error breakdown if there are failures
        if (result.Metrics.FailedOperations > 0 && result.Metrics.ErrorsByCategory?.Count > 0)
        {
            AnsiConsole.MarkupLine($"  [bold]Error Breakdown:[/]");
            foreach (var (category, count) in result.Metrics.ErrorsByCategory.OrderByDescending(kvp => kvp.Value))
            {
                var percentage = (count / (double)result.Metrics.FailedOperations) * 100.0;
                AnsiConsole.MarkupLine($"    [yellow]{category}:[/] {count:N0} ({percentage:F1}%)");
            }
        }

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

/// <summary>
/// Comparer for AttributeValue to support Distinct() on List items.
/// </summary>
internal class AttributeValueComparer : IEqualityComparer<AttributeValue>
{
    public bool Equals(AttributeValue? x, AttributeValue? y)
    {
        if (x == null && y == null) return true;
        if (x == null || y == null) return false;
        return x.S == y.S;
    }

    public int GetHashCode(AttributeValue obj)
    {
        return obj.S?.GetHashCode() ?? 0;
    }
}
