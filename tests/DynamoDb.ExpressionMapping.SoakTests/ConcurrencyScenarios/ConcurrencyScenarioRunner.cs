using Spectre.Console;

namespace DynamoDb.ExpressionMapping.SoakTests.ConcurrencyScenarios;

/// <summary>
/// Executes all PR-02.7 concurrency test scenarios.
/// </summary>
public class ConcurrencyScenarioRunner
{
    private readonly SharedDependencies _sharedDependencies;
    private readonly List<IConcurrencyScenario> _scenarios;

    public ConcurrencyScenarioRunner(SharedDependencies sharedDependencies)
    {
        _sharedDependencies = sharedDependencies ?? throw new ArgumentNullException(nameof(sharedDependencies));

        // Initialize all scenarios
        _scenarios = new List<IConcurrencyScenario>
        {
            new SameExpressionCacheConcurrencyScenario(_sharedDependencies),
            new DistinctExpressionCacheConcurrencyScenario(_sharedDependencies),
            new FilterCompositionConcurrencyScenario(_sharedDependencies),
            new ConverterRegistryConcurrencyScenario(_sharedDependencies),
            new DirectResultMapperConcurrencyScenario(_sharedDependencies)
        };
    }

    /// <summary>
    /// Executes all concurrency scenarios with the specified number of concurrent workers.
    /// Returns true if all scenarios passed, false if any failed.
    /// </summary>
    public async Task<ConcurrencyScenarioResult> RunAllAsync(int concurrentWorkers, CancellationToken cancellationToken = default)
    {
        AnsiConsole.MarkupLine("[bold yellow]▶ Running Concurrency Test Scenarios[/] [dim](PR-02.7)[/]");
        AnsiConsole.WriteLine();

        var results = new List<ScenarioExecutionResult>();

        foreach (var scenario in _scenarios)
        {
            var scenarioResult = await RunScenarioAsync(scenario, concurrentWorkers, cancellationToken);
            results.Add(scenarioResult);

            // Display result
            var statusIcon = scenarioResult.Passed ? "[green]✓[/]" : "[red]✗[/]";
            var statusText = scenarioResult.Passed ? "[green]PASS[/]" : "[red]FAIL[/]";
            AnsiConsole.MarkupLine($"  {statusIcon} {scenario.Name}: {statusText}");

            if (!scenarioResult.Passed && !string.IsNullOrEmpty(scenarioResult.ErrorMessage))
            {
                AnsiConsole.MarkupLine($"    [dim red]{scenarioResult.ErrorMessage}[/]");
            }

            AnsiConsole.WriteLine();
        }

        var allPassed = results.All(r => r.Passed);

        // Summary
        var passedCount = results.Count(r => r.Passed);
        var failedCount = results.Count(r => !r.Passed);

        AnsiConsole.MarkupLine($"[bold]Concurrency Scenarios Summary:[/] {passedCount}/{_scenarios.Count} passed");
        if (failedCount > 0)
        {
            AnsiConsole.MarkupLine($"  [red]{failedCount} scenario(s) failed[/]");
        }

        AnsiConsole.WriteLine();

        return new ConcurrencyScenarioResult(
            TotalScenarios: _scenarios.Count,
            PassedScenarios: passedCount,
            FailedScenarios: failedCount,
            AllPassed: allPassed,
            Results: results.AsReadOnly()
        );
    }

    private async Task<ScenarioExecutionResult> RunScenarioAsync(
        IConcurrencyScenario scenario,
        int concurrentWorkers,
        CancellationToken cancellationToken)
    {
        try
        {
            await scenario.ExecuteAsync(concurrentWorkers, cancellationToken);
            return new ScenarioExecutionResult(
                ScenarioName: scenario.Name,
                Passed: true,
                ErrorMessage: null
            );
        }
        catch (Exception ex)
        {
            return new ScenarioExecutionResult(
                ScenarioName: scenario.Name,
                Passed: false,
                ErrorMessage: ex.Message
            );
        }
    }
}

/// <summary>
/// Result of running all concurrency scenarios.
/// </summary>
public record ConcurrencyScenarioResult(
    int TotalScenarios,
    int PassedScenarios,
    int FailedScenarios,
    bool AllPassed,
    IReadOnlyList<ScenarioExecutionResult> Results
);

/// <summary>
/// Result of executing a single scenario.
/// </summary>
public record ScenarioExecutionResult(
    string ScenarioName,
    bool Passed,
    string? ErrorMessage
);
