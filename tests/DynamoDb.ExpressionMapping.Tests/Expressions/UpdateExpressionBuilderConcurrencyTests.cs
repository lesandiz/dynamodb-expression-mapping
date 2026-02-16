using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

/// <summary>
/// Concurrency and thread-safety tests for UpdateExpressionBuilder.
/// Validates ADR-001 clone-on-use pattern implementation.
/// </summary>
public class UpdateExpressionBuilderConcurrencyTests
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;

    public UpdateExpressionBuilderConcurrencyTests()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
    }

    [Fact]
    public async Task ConcurrentUpdates_DoNotShareState()
    {
        // Arrange: shared singleton builder instance
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        // Act: concurrent Set operations on the same builder
        var task1 = Task.Run(() => builder.Set(x => x.Title, "Alice").Build());
        var task2 = Task.Run(() => builder.Set(x => x.Title, "Bob").Build());

        var results = await Task.WhenAll(task1, task2);

        // Assert: results are independent — no cross-contamination
        // Both expressions should follow the same pattern
        results[0].Expression.Should().Be("SET Title = :upd_v0");
        results[1].Expression.Should().Be("SET Title = :upd_v0");

        // But values should be different
        results[0].ExpressionAttributeValues.Should().HaveCount(1);
        results[1].ExpressionAttributeValues.Should().HaveCount(1);

        var value1 = results[0].ExpressionAttributeValues[":upd_v0"].S;
        var value2 = results[1].ExpressionAttributeValues[":upd_v0"].S;

        // One should be Alice, one should be Bob (order not guaranteed)
        new[] { value1, value2 }.Should().BeEquivalentTo(new[] { "Alice", "Bob" });
    }

    [Fact]
    public async Task ConcurrentFluentChains_ProduceIndependentResults()
    {
        // Arrange: shared singleton builder instance
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        // Act: concurrent fluent chains with multiple operations
        var task1 = Task.Run(() => builder
            .Set(x => x.Title, "Chain1-Title")
            .Increment(x => x.ViewCount, 50)
            .Build());

        var task2 = Task.Run(() => builder
            .Set(x => x.Title, "Chain2-Title")
            .Decrement(x => x.ViewCount, 25)
            .Build());

        var results = await Task.WhenAll(task1, task2);

        // Assert: each chain produces its own independent result
        results[0].Expression.Should().Contain("+ :upd_v1"); // Increment
        results[0].ExpressionAttributeValues.Should().HaveCount(2); // Title + increment amount

        results[1].Expression.Should().Contain("- :upd_v1"); // Decrement
        results[1].ExpressionAttributeValues.Should().HaveCount(2); // Title + decrement amount

        // Check the actual values
        var title1 = results[0].ExpressionAttributeValues[":upd_v0"].S;
        var title2 = results[1].ExpressionAttributeValues[":upd_v0"].S;
        new[] { title1, title2 }.Should().BeEquivalentTo(new[] { "Chain1-Title", "Chain2-Title" });
    }

    [Fact]
    public async Task ConcurrentMixedOperations_IsolateState()
    {
        // Arrange
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        // Act: different operation types concurrently
        var task1 = Task.Run(() => builder
            .Set(x => x.Title, "SetOperation")
            .Build());

        var task2 = Task.Run(() => builder
            .Remove(x => x.Title)
            .Build());

        var task3 = Task.Run(() => builder
            .Add(x => x.ViewCount, 10)
            .Build());

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert: each operation type produces correct isolated result
        results[0].Expression.Should().Be("SET Title = :upd_v0");
        results[0].ExpressionAttributeValues.Should().HaveCount(1);

        results[1].Expression.Should().Be("REMOVE Title");
        results[1].ExpressionAttributeValues.Should().BeEmpty();

        results[2].Expression.Should().Be("ADD ViewCount :upd_v0");
        results[2].ExpressionAttributeValues.Should().HaveCount(1);
    }

    [Fact]
    public async Task HighConcurrencyLoad_MaintainsCorrectness()
    {
        // Arrange: simulate high concurrent load
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);
        const int taskCount = 100;

        // Act: spawn 100 concurrent update operations
        var tasks = Enumerable.Range(0, taskCount)
            .Select(i => Task.Run(() => builder
                .Set(x => x.Title, $"Title-{i}")
                .Set(x => x.ViewCount, i)
                .Build()))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: all results have the correct structure
        results.Should().HaveCount(taskCount);

        // All should have the same expression pattern
        results.Should().AllSatisfy(r => r.Expression.Should().Be("SET Title = :upd_v0, ViewCount = :upd_v1"));

        // All should have 2 values
        results.Should().AllSatisfy(r => r.ExpressionAttributeValues.Should().HaveCount(2));

        // Extract all titles and view counts
        var titles = results.Select(r => r.ExpressionAttributeValues[":upd_v0"].S).ToArray();
        var viewCounts = results.Select(r => int.Parse(r.ExpressionAttributeValues[":upd_v1"].N)).ToArray();

        // All titles and view counts should be in the expected range (order not guaranteed)
        titles.Should().AllSatisfy(t => t.Should().MatchRegex(@"^Title-\d+$"));
        viewCounts.Should().AllSatisfy(v => v.Should().BeInRange(0, taskCount - 1));

        // Each value should appear exactly once
        titles.Should().OnlyHaveUniqueItems();
        viewCounts.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ConcurrentBuildsFromSameChain_ProduceSameResult()
    {
        // Arrange: build a chain once, then call Build() concurrently
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);
        var chain = builder
            .Set(x => x.Title, "Shared")
            .Set(x => x.ViewCount, 42);

        // Act: call Build() concurrently on the same chain
        var task1 = Task.Run(() => chain.Build());
        var task2 = Task.Run(() => chain.Build());
        var task3 = Task.Run(() => chain.Build());

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert: all builds produce identical results (Build is read-only)
        results[0].Expression.Should().Be(results[1].Expression);
        results[1].Expression.Should().Be(results[2].Expression);
        results[0].ExpressionAttributeValues.Should().BeEquivalentTo(results[1].ExpressionAttributeValues);
        results[1].ExpressionAttributeValues.Should().BeEquivalentTo(results[2].ExpressionAttributeValues);
    }

    [Fact]
    public async Task ConcurrentDifferentPropertyUpdates_NoCollisions()
    {
        // Arrange
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        // Act: concurrent updates to different properties
        var task1 = Task.Run(() => builder.Set(x => x.Title, "TitleValue").Build());
        var task2 = Task.Run(() => builder.Set(x => x.ViewCount, 999).Build());
        var task3 = Task.Run(() => builder.Set(x => x.Price, 99.99m).Build());

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert: each result updates only its property
        results[0].Expression.Should().Be("SET Title = :upd_v0");
        results[0].ExpressionAttributeValues.Should().HaveCount(1);

        results[1].Expression.Should().Be("SET ViewCount = :upd_v0");
        results[1].ExpressionAttributeValues.Should().HaveCount(1);

        results[2].Expression.Should().Be("SET Price = :upd_v0");
        results[2].ExpressionAttributeValues.Should().HaveCount(1);
    }

    [Fact]
    public async Task NestedConcurrentChains_MaintainIndependence()
    {
        // Arrange
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        // Act: build chains from a shared partial chain
        var baseChain = builder.Set(x => x.Title, "Base");

        var task1 = Task.Run(() => baseChain.Set(x => x.ViewCount, 1).Build());
        var task2 = Task.Run(() => baseChain.Set(x => x.ViewCount, 2).Build());
        var task3 = Task.Run(() => baseChain.Set(x => x.ViewCount, 3).Build());

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert: all results have the base operation + their own ViewCount
        results[0].Expression.Should().Contain("Title = :upd_v0");
        results[0].Expression.Should().Contain("ViewCount = :upd_v1");
        results[0].ExpressionAttributeValues[":upd_v1"].N.Should().Be("1");

        results[1].Expression.Should().Contain("Title = :upd_v0");
        results[1].Expression.Should().Contain("ViewCount = :upd_v1");
        results[1].ExpressionAttributeValues[":upd_v1"].N.Should().Be("2");

        results[2].Expression.Should().Contain("Title = :upd_v0");
        results[2].Expression.Should().Contain("ViewCount = :upd_v1");
        results[2].ExpressionAttributeValues[":upd_v1"].N.Should().Be("3");
    }

    [Fact]
    public async Task ConcurrentComplexOperations_NoStateLeak()
    {
        // Arrange
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        // Act: complex operations with multiple clause types
        var task1 = Task.Run(() => builder
            .Set(x => x.Title, "Task1")
            .Increment(x => x.ViewCount, 10)
            .AppendToList(x => x.Tags, new List<string> { "tag1" })
            .Build());

        var task2 = Task.Run(() => builder
            .Set(x => x.Title, "Task2")
            .Decrement(x => x.ViewCount, 5)
            .Remove(x => x.TempFlag)
            .Build());

        var results = await Task.WhenAll(task1, task2);

        // Assert: no state leakage between complex operations
        results[0].Expression.Should().Contain("ViewCount + :upd_v1");
        results[0].Expression.Should().Contain("list_append");
        results[0].Expression.Should().NotContain("REMOVE");
        results[0].ExpressionAttributeValues[":upd_v0"].S.Should().Be("Task1");

        results[1].Expression.Should().Contain("ViewCount - :upd_v1");
        results[1].Expression.Should().Contain("REMOVE TempFlag");
        results[1].Expression.Should().NotContain("list_append");
        results[1].ExpressionAttributeValues[":upd_v0"].S.Should().Be("Task2");
    }
}
