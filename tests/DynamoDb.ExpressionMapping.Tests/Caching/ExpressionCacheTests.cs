using DynamoDb.ExpressionMapping.Caching;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Caching;

public class ExpressionCacheTests
{
    [Fact]
    public void Default_IsSingleton()
    {
        // Act
        var instance1 = ExpressionCache.Default;
        var instance2 = ExpressionCache.Default;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void GetOrAdd_WithNullCategory_ThrowsArgumentNullException()
    {
        // Arrange
        var cache = new ExpressionCache();

        // Act
        var act = () => cache.GetOrAdd<string>(null!, "key", k => "value");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cacheCategory");
    }

    [Fact]
    public void GetOrAdd_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var cache = new ExpressionCache();

        // Act
        var act = () => cache.GetOrAdd<string>("projection", null!, k => "value");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("key");
    }

    [Fact]
    public void GetOrAdd_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var cache = new ExpressionCache();

        // Act
        var act = () => cache.GetOrAdd<string>("projection", "key", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("factory");
    }

    [Fact]
    public void GetOrAdd_WithUnknownCategory_ThrowsArgumentException()
    {
        // Arrange
        var cache = new ExpressionCache();

        // Act
        var act = () => cache.GetOrAdd<string>("unknown", "key", k => "value");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Unknown cache category: unknown*");
    }

    [Fact]
    public void GetOrAdd_CachesProjectionResults()
    {
        // Arrange
        var cache = new ExpressionCache();
        var invocationCount = 0;

        string Factory(string key)
        {
            invocationCount++;
            return $"value-{invocationCount}";
        }

        // Act
        var result1 = cache.GetOrAdd("projection", "key1", Factory);
        var result2 = cache.GetOrAdd("projection", "key1", Factory); // Same key
        var result3 = cache.GetOrAdd("projection", "key1", Factory); // Same key

        // Assert
        invocationCount.Should().Be(1); // Factory invoked only once
        result1.Should().Be("value-1");
        result2.Should().Be("value-1"); // Cached
        result3.Should().Be("value-1"); // Cached
    }

    [Fact]
    public void GetOrAdd_CachesMapperResults()
    {
        // Arrange
        var cache = new ExpressionCache();
        var invocationCount = 0;

        Func<string, string> Factory(string key)
        {
            invocationCount++;
            return s => $"mapped-{s}";
        }

        // Act
        var result1 = cache.GetOrAdd("mapper", "key1", Factory);
        var result2 = cache.GetOrAdd("mapper", "key1", Factory);

        // Assert
        invocationCount.Should().Be(1);
        result1.Should().BeSameAs(result2); // Same delegate instance
    }

    [Fact]
    public void GetOrAdd_CachesFilterResults()
    {
        // Arrange
        var cache = new ExpressionCache();
        var invocationCount = 0;

        string Factory(string key)
        {
            invocationCount++;
            return $"filter-{invocationCount}";
        }

        // Act
        var result1 = cache.GetOrAdd("filter", "key1", Factory);
        var result2 = cache.GetOrAdd("filter", "key1", Factory);

        // Assert
        invocationCount.Should().Be(1);
        result1.Should().Be(result2);
    }

    [Fact]
    public void GetOrAdd_DifferentKeysInvokesFactory()
    {
        // Arrange
        var cache = new ExpressionCache();
        var invocationCount = 0;

        string Factory(string key)
        {
            invocationCount++;
            return $"value-{key}";
        }

        // Act
        var result1 = cache.GetOrAdd("projection", "key1", Factory);
        var result2 = cache.GetOrAdd("projection", "key2", Factory);
        var result3 = cache.GetOrAdd("projection", "key3", Factory);

        // Assert
        invocationCount.Should().Be(3);
        result1.Should().Be("value-key1");
        result2.Should().Be("value-key2");
        result3.Should().Be("value-key3");
    }

    [Fact]
    public void GetOrAdd_DifferentCategoriesAreSeparate()
    {
        // Arrange
        var cache = new ExpressionCache();

        // Act
        var projectionResult = cache.GetOrAdd("projection", "key1", k => "projection-value");
        var mapperResult = cache.GetOrAdd("mapper", "key1", k => "mapper-value");
        var filterResult = cache.GetOrAdd("filter", "key1", k => "filter-value");

        // Assert - Same key but different categories should store different values
        projectionResult.Should().Be("projection-value");
        mapperResult.Should().Be("mapper-value");
        filterResult.Should().Be("filter-value");

        // Verify cached value is returned, not the new factory's value
        var cachedResult = cache.GetOrAdd<string>("projection", "key1", _ => "different-value");
        cachedResult.Should().Be("projection-value");
    }

    [Fact]
    public void GetOrAdd_CategoryNamesAreCaseInsensitive()
    {
        // Arrange
        var cache = new ExpressionCache();
        var invocationCount = 0;

        string Factory(string key)
        {
            invocationCount++;
            return "value";
        }

        // Act
        var result1 = cache.GetOrAdd("Projection", "key1", Factory);
        var result2 = cache.GetOrAdd("PROJECTION", "key1", Factory);
        var result3 = cache.GetOrAdd("projection", "key1", Factory);

        // Assert
        invocationCount.Should().Be(1); // All reference the same cache
        result1.Should().Be(result2);
        result2.Should().Be(result3);
    }

    [Fact]
    public void GetStatistics_InitiallyReturnsZeros()
    {
        // Arrange
        var cache = new ExpressionCache();

        // Act
        var stats = cache.GetStatistics();

        // Assert
        stats.ProjectionHits.Should().Be(0);
        stats.ProjectionMisses.Should().Be(0);
        stats.MapperHits.Should().Be(0);
        stats.MapperMisses.Should().Be(0);
        stats.FilterHits.Should().Be(0);
        stats.FilterMisses.Should().Be(0);
        stats.TotalEntries.Should().Be(0);
    }

    [Fact]
    public void GetStatistics_TracksProjectionAccess()
    {
        // Arrange
        var cache = new ExpressionCache();

        // Act
        cache.GetOrAdd("projection", "key1", k => "value1"); // Miss
        cache.GetOrAdd("projection", "key1", k => "value1"); // Hit
        cache.GetOrAdd("projection", "key2", k => "value2"); // Miss
        cache.GetOrAdd("projection", "key1", k => "value1"); // Hit

        var stats = cache.GetStatistics();

        // Assert
        stats.ProjectionMisses.Should().Be(2);
        stats.ProjectionHits.Should().Be(2);
        stats.TotalEntries.Should().Be(2);
    }

    [Fact]
    public void GetStatistics_TracksMapperAccess()
    {
        // Arrange
        var cache = new ExpressionCache();

        // Act
        cache.GetOrAdd("mapper", "key1", k => "value1");
        cache.GetOrAdd("mapper", "key1", k => "value1");
        cache.GetOrAdd("mapper", "key2", k => "value2");

        var stats = cache.GetStatistics();

        // Assert
        stats.MapperMisses.Should().Be(2);
        stats.MapperHits.Should().Be(1);
    }

    [Fact]
    public void GetStatistics_TracksFilterAccess()
    {
        // Arrange
        var cache = new ExpressionCache();

        // Act
        cache.GetOrAdd("filter", "key1", k => "value1");
        cache.GetOrAdd("filter", "key1", k => "value1");

        var stats = cache.GetStatistics();

        // Assert
        stats.FilterMisses.Should().Be(1);
        stats.FilterHits.Should().Be(1);
    }

    [Fact]
    public void GetStatistics_CalculatesHitRates()
    {
        // Arrange
        var cache = new ExpressionCache();

        // Act
        cache.GetOrAdd("projection", "key1", k => "value1"); // Miss
        cache.GetOrAdd("projection", "key1", k => "value1"); // Hit
        cache.GetOrAdd("projection", "key1", k => "value1"); // Hit
        cache.GetOrAdd("projection", "key1", k => "value1"); // Hit

        var stats = cache.GetStatistics();

        // Assert
        stats.ProjectionHitRate.Should().BeApproximately(0.75, 0.01); // 3/4
        stats.MapperHitRate.Should().Be(0.0);
        stats.FilterHitRate.Should().Be(0.0);
        stats.OverallHitRate.Should().BeApproximately(0.75, 0.01);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        // Arrange
        var cache = new ExpressionCache();
        cache.GetOrAdd("projection", "key1", k => "value1");
        cache.GetOrAdd("mapper", "key2", k => "value2");
        cache.GetOrAdd("filter", "key3", k => "value3");

        // Act
        cache.Clear();

        // Assert
        var stats = cache.GetStatistics();
        stats.TotalEntries.Should().Be(0);
        stats.ProjectionHits.Should().Be(0);
        stats.ProjectionMisses.Should().Be(0);
    }

    [Fact]
    public void Clear_ResetsStatistics()
    {
        // Arrange
        var cache = new ExpressionCache();
        cache.GetOrAdd("projection", "key1", k => "value1");
        cache.GetOrAdd("projection", "key1", k => "value1");

        // Act
        cache.Clear();
        var stats = cache.GetStatistics();

        // Assert
        stats.ProjectionHits.Should().Be(0);
        stats.ProjectionMisses.Should().Be(0);
    }

    [Fact]
    public async Task GetOrAdd_IsThreadSafe()
    {
        // Arrange
        var cache = new ExpressionCache();
        var invocationCount = 0;

        string Factory(string key)
        {
            Interlocked.Increment(ref invocationCount);
            Thread.Sleep(10); // Simulate work
            return "value";
        }

        // Act - Concurrent access to same key
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => cache.GetOrAdd("projection", "key1", Factory)))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - All tasks should resolve to the same cached value
        var results = tasks.Select(t => t.Result).Distinct().ToList();
        results.Should().HaveCount(1, "all concurrent calls should resolve to the same cached value");
    }

    [Fact]
    public void GetOrAdd_SupportsComplexTypes()
    {
        // Arrange
        var cache = new ExpressionCache();

        // Act
        var result = cache.GetOrAdd("projection", "key1", k => new
        {
            Name = "Test",
            Value = 42
        });

        var cachedResult = cache.GetOrAdd("projection", "key1", k => new
        {
            Name = "Different",
            Value = 99
        });

        // Assert
        result.Should().BeSameAs(cachedResult);
        cachedResult.Name.Should().Be("Test"); // Original value
        cachedResult.Value.Should().Be(42);
    }

}
