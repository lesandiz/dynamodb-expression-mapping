using DynamoDb.ExpressionMapping.SoakTests.ConcurrencyScenarios;
using DynamoDb.ExpressionMapping.SoakTests.Metrics;
using Spectre.Console;

namespace DynamoDb.ExpressionMapping.SoakTests;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        AnsiConsole.MarkupLine("[bold green]DynamoDB Expression Mapping - Soak Test Harness[/]");
        AnsiConsole.WriteLine();

        // Temporary: Run metrics demo to verify implementation
        if (args.Length > 0 && args[0] == "--demo")
        {
            await MetricsDemo.RunDemo();
            return 0;
        }

        // Run concurrency scenarios if requested
        if (args.Length > 0 && args[0] == "--concurrency-scenarios")
        {
            var concurrency = 8; // Default
            if (args.Length > 1 && int.TryParse(args[1], out var parsedConcurrency))
            {
                concurrency = parsedConcurrency;
            }

            var sharedDeps = new SharedDependencies();
            var scenarioRunner = new ConcurrencyScenarioRunner(sharedDeps);
            var scenarioResult = await scenarioRunner.RunAllAsync(concurrency);
            return scenarioResult.AllPassed ? 0 : 1;
        }

        // Parse CLI arguments
        var config = ParseArguments(args);

        // Run soak test
        var runner = new SoakTestRunner(config);
        var result = await runner.RunAsync();

        // Return exit code: 0 = pass, 1 = fail
        return result.Passed ? 0 : 1;
    }

    /// <summary>
    /// Parses CLI arguments and returns a SoakTestConfig.
    /// Supports: --duration N, --concurrency N, --workload TYPE
    /// </summary>
    private static SoakTestConfig ParseArguments(string[] args)
    {
        var durationMinutes = 10.0; // Default: 10 minutes
        var concurrency = 8; // Default: 8 workers
        var workload = "mixed"; // Default: mixed workload

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--duration" when i + 1 < args.Length:
                    if (double.TryParse(args[i + 1], out var parsedDuration))
                    {
                        durationMinutes = parsedDuration;
                    }
                    i++;
                    break;

                case "--concurrency" when i + 1 < args.Length:
                    if (int.TryParse(args[i + 1], out var parsedConcurrency))
                    {
                        concurrency = parsedConcurrency;
                    }
                    i++;
                    break;

                case "--workload" when i + 1 < args.Length:
                    workload = args[i + 1];
                    i++;
                    break;
            }
        }

        return new SoakTestConfig(
            WarmupDuration: TimeSpan.FromMinutes(2),
            SustainedDuration: TimeSpan.FromMinutes(durationMinutes),
            CooldownDuration: TimeSpan.FromMinutes(1),
            ConcurrentWorkers: concurrency,
            WorkloadType: workload
        );
    }
}
