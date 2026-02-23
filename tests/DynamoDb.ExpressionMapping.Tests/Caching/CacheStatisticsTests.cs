using DynamoDb.ExpressionMapping.Caching;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Caching;

/// <summary>
/// Tests for CacheStatistics hit-rate computation properties.
/// Consolidated from P4MutationKillingTests (Phase 3b mutation analysis).
/// </summary>
public class CacheStatisticsTests
{
    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(10, 0, 1.0)]
    [InlineData(0, 10, 0.0)]
    [InlineData(3, 1, 0.75)]
    [InlineData(1, 1, 0.5)]
    [InlineData(2, 3, 0.4)]
    public void ProjectionHitRate_ReturnsCorrectRate(int hits, int misses, double expected)
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = hits,
            ProjectionMisses = misses
        };

        stats.ProjectionHitRate.Should().BeApproximately(expected, 0.001);
    }

    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(5, 0, 1.0)]
    [InlineData(0, 5, 0.0)]
    [InlineData(2, 3, 0.4)]
    public void MapperHitRate_ReturnsCorrectRate(int hits, int misses, double expected)
    {
        var stats = new CacheStatistics
        {
            MapperHits = hits,
            MapperMisses = misses
        };

        stats.MapperHitRate.Should().BeApproximately(expected, 0.001);
    }

    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(7, 0, 1.0)]
    [InlineData(0, 7, 0.0)]
    [InlineData(4, 6, 0.4)]
    public void FilterHitRate_ReturnsCorrectRate(int hits, int misses, double expected)
    {
        var stats = new CacheStatistics
        {
            FilterHits = hits,
            FilterMisses = misses
        };

        stats.FilterHitRate.Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void OverallHitRate_ZeroAccesses_ReturnsZero()
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = 0,
            ProjectionMisses = 0,
            MapperHits = 0,
            MapperMisses = 0,
            FilterHits = 0,
            FilterMisses = 0
        };

        stats.OverallHitRate.Should().Be(0.0);
    }

    [Theory]
    [InlineData(5, 0, 3, 0, 2, 0, 1.0)]
    [InlineData(0, 5, 0, 3, 0, 2, 0.0)]
    [InlineData(3, 1, 2, 2, 1, 1, 0.6)]
    [InlineData(4, 1, 0, 0, 0, 0, 0.8)]
    [InlineData(1, 0, 1, 0, 1, 0, 1.0)]
    [InlineData(1, 1, 1, 1, 1, 1, 0.5)]
    public void OverallHitRate_ReturnsCorrectRate(
        int projHits, int projMisses,
        int mapperHits, int mapperMisses,
        int filterHits, int filterMisses,
        double expected)
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = projHits,
            ProjectionMisses = projMisses,
            MapperHits = mapperHits,
            MapperMisses = mapperMisses,
            FilterHits = filterHits,
            FilterMisses = filterMisses
        };

        stats.OverallHitRate.Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void HitRate_AdditionMutantKiller_HitsPlusMissesNotMinus()
    {
        // Kills: hits + misses → hits - misses
        var stats = new CacheStatistics
        {
            ProjectionHits = 2,
            ProjectionMisses = 1
        };

        // 2 / (2+1) = 0.667 vs 2 / (2-1) = 2.0
        stats.ProjectionHitRate.Should().BeApproximately(2.0 / 3.0, 0.001);
    }
}
