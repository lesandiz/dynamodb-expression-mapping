using DynamoDb.ExpressionMapping.SoakTests.Metrics;
using Spectre.Console;

namespace DynamoDb.ExpressionMapping.SoakTests;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        AnsiConsole.MarkupLine("[bold green]DynamoDB Expression Mapping - Soak Test Harness[/]");
        AnsiConsole.MarkupLine("[dim]Starting...[/]");

        // Temporary: Run metrics demo to verify implementation
        if (args.Length > 0 && args[0] == "--demo")
        {
            await MetricsDemo.RunDemo();
            return 0;
        }

        // TODO: Implement CLI argument parsing and runner orchestration

        return 0;
    }
}
