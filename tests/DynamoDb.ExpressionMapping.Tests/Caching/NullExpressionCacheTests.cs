using DynamoDb.ExpressionMapping.Caching;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Caching;

public class NullExpressionCacheTests
{
    [Fact]
    public void Instance_IsSingleton()
    {
        // Act
        var instance1 = NullExpressionCache.Instance;
        var instance2 = NullExpressionCache.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void GetOrAdd_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var cache = NullExpressionCache.Instance;

        // Act
        var act = () => cache.GetOrAdd<string>("projection", "key", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("factory");
    }

    [Fact]
    public void GetOrAdd_AlwaysInvokesFactory()
    {
        // Arrange
        var cache = NullExpressionCache.Instance;
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
        invocationCount.Should().Be(3); // Factory invoked every time
        result1.Should().Be("value-1");
        result2.Should().Be("value-2");
        result3.Should().Be("value-3");
    }

    [Fact]
    public void GetOrAdd_NeverCachesResults()
    {
        // Arrange
        var cache = NullExpressionCache.Instance;

        // Act
        var result1 = cache.GetOrAdd("projection", "key1", k => "first");
        var result2 = cache.GetOrAdd("projection", "key1", k => "second");

        // Assert
        result1.Should().Be("first");
        result2.Should().Be("second"); // Different value for same key
    }

    [Fact]
    public void GetOrAdd_PassesKeyToFactory()
    {
        // Arrange
        var cache = NullExpressionCache.Instance;
        string? capturedKey = null;

        // Act
        cache.GetOrAdd("projection", "test-key", k =>
        {
            capturedKey = k;
            return "value";
        });

        // Assert
        capturedKey.Should().Be("test-key");
    }

    [Fact]
    public void GetOrAdd_WorksWithDifferentCategories()
    {
        // Arrange
        var cache = NullExpressionCache.Instance;
        var invocationCount = 0;

        string Factory(string key)
        {
            invocationCount++;
            return $"value-{invocationCount}";
        }

        // Act
        var result1 = cache.GetOrAdd("projection", "key1", Factory);
        var result2 = cache.GetOrAdd("mapper", "key1", Factory);
        var result3 = cache.GetOrAdd("filter", "key1", Factory);

        // Assert
        invocationCount.Should().Be(3); // Factory invoked for each category
        result1.Should().Be("value-1");
        result2.Should().Be("value-2");
        result3.Should().Be("value-3");
    }

    [Fact]
    public void GetOrAdd_SupportsGenericTypes()
    {
        // Arrange
        var cache = NullExpressionCache.Instance;

        // Act
        var stringResult = cache.GetOrAdd<string>("projection", "key1", k => "test");
        var intResult = cache.GetOrAdd<int>("mapper", "key2", k => 42);
        var boolResult = cache.GetOrAdd<bool>("filter", "key3", k => true);

        // Assert
        stringResult.Should().Be("test");
        intResult.Should().Be(42);
        boolResult.Should().BeTrue();
    }
}
