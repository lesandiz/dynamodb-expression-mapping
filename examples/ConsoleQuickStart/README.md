# Console Quick Start

The simplest possible demo of DynamoDb.ExpressionMapping — all code in a single
`Program.cs` with no dependency injection.

## What's Demonstrated

- Manual builder instantiation (no DI container)
- All 5 expression types: Projection, Filter, KeyCondition, Update, Condition
- Reserved keyword auto-aliasing (`Name`, `Status`)
- Custom type converter (`MoneyConverter`)
- Filter composition with `And()` / `Or()`
- Direct result mapping to anonymous types and named DTOs
- Nested property paths in result mapping
- Extension methods on AWS SDK request types

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later
- [Docker](https://www.docker.com/get-started)

## Run

```bash
docker compose up -d
dotnet run
```

## Clean Up

```bash
docker compose down
```
