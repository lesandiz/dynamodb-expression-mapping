using Spectre.Console;

namespace DynamoDb.ExpressionMapping.SoakTests;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        AnsiConsole.MarkupLine("[bold green]DynamoDB Expression Mapping - Soak Test Harness[/]");
        AnsiConsole.MarkupLine("[dim]Starting...[/]");

        // TODO: Implement CLI argument parsing and runner orchestration

        return 0;
    }
}
