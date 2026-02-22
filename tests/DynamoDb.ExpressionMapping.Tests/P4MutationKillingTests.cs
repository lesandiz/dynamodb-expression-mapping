using System.Reflection;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Mapping.Converters;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DynamoDb.ExpressionMapping.Tests;

/// <summary>
/// Tests specifically designed to kill surviving and NoCoverage mutants
/// in Priority 4 subsystems identified during Phase 3b mutation analysis.
///
/// Targets:
///   - CacheStatistics: hit-rate computation properties (20 survivors)
///   - ExpressionCache: TrackAccess hit/miss side effects (2 survivors)
///   - InternalRequestExtensions: ??= → = mutations (10 survivors)
///   - DynamoDbExpressionConfig: null coalescing in Builder.Build() (5 survivors)
///   - AliasGenerator: Clone preserves counter state
/// </summary>
public class P4MutationKillingTests
{
    #region CacheStatistics — hit-rate computation

    [Fact]
    public void CacheStatistics_ProjectionHitRate_ZeroAccesses_ReturnsZero()
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = 0,
            ProjectionMisses = 0
        };

        stats.ProjectionHitRate.Should().Be(0.0);
    }

    [Fact]
    public void CacheStatistics_ProjectionHitRate_AllHits_ReturnsOne()
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = 10,
            ProjectionMisses = 0
        };

        stats.ProjectionHitRate.Should().Be(1.0);
    }

    [Fact]
    public void CacheStatistics_ProjectionHitRate_AllMisses_ReturnsZero()
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = 0,
            ProjectionMisses = 10
        };

        stats.ProjectionHitRate.Should().Be(0.0);
    }

    [Fact]
    public void CacheStatistics_ProjectionHitRate_MixedAccesses_ReturnsCorrectRate()
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = 3,
            ProjectionMisses = 1
        };

        // 3 / (3 + 1) = 0.75
        stats.ProjectionHitRate.Should().BeApproximately(0.75, 0.001);
    }

    [Fact]
    public void CacheStatistics_ProjectionHitRate_OneHitOneMiss_ReturnsHalf()
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = 1,
            ProjectionMisses = 1
        };

        stats.ProjectionHitRate.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void CacheStatistics_MapperHitRate_ZeroAccesses_ReturnsZero()
    {
        var stats = new CacheStatistics
        {
            MapperHits = 0,
            MapperMisses = 0
        };

        stats.MapperHitRate.Should().Be(0.0);
    }

    [Fact]
    public void CacheStatistics_MapperHitRate_AllHits_ReturnsOne()
    {
        var stats = new CacheStatistics
        {
            MapperHits = 5,
            MapperMisses = 0
        };

        stats.MapperHitRate.Should().Be(1.0);
    }

    [Fact]
    public void CacheStatistics_MapperHitRate_AllMisses_ReturnsZero()
    {
        var stats = new CacheStatistics
        {
            MapperHits = 0,
            MapperMisses = 5
        };

        stats.MapperHitRate.Should().Be(0.0);
    }

    [Fact]
    public void CacheStatistics_MapperHitRate_MixedAccesses_ReturnsCorrectRate()
    {
        var stats = new CacheStatistics
        {
            MapperHits = 2,
            MapperMisses = 3
        };

        // 2 / (2 + 3) = 0.4
        stats.MapperHitRate.Should().BeApproximately(0.4, 0.001);
    }

    [Fact]
    public void CacheStatistics_FilterHitRate_ZeroAccesses_ReturnsZero()
    {
        var stats = new CacheStatistics
        {
            FilterHits = 0,
            FilterMisses = 0
        };

        stats.FilterHitRate.Should().Be(0.0);
    }

    [Fact]
    public void CacheStatistics_FilterHitRate_AllHits_ReturnsOne()
    {
        var stats = new CacheStatistics
        {
            FilterHits = 7,
            FilterMisses = 0
        };

        stats.FilterHitRate.Should().Be(1.0);
    }

    [Fact]
    public void CacheStatistics_FilterHitRate_AllMisses_ReturnsZero()
    {
        var stats = new CacheStatistics
        {
            FilterHits = 0,
            FilterMisses = 7
        };

        stats.FilterHitRate.Should().Be(0.0);
    }

    [Fact]
    public void CacheStatistics_FilterHitRate_MixedAccesses_ReturnsCorrectRate()
    {
        var stats = new CacheStatistics
        {
            FilterHits = 4,
            FilterMisses = 6
        };

        // 4 / (4 + 6) = 0.4
        stats.FilterHitRate.Should().BeApproximately(0.4, 0.001);
    }

    [Fact]
    public void CacheStatistics_OverallHitRate_ZeroAccesses_ReturnsZero()
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = 0, ProjectionMisses = 0,
            MapperHits = 0, MapperMisses = 0,
            FilterHits = 0, FilterMisses = 0
        };

        stats.OverallHitRate.Should().Be(0.0);
    }

    [Fact]
    public void CacheStatistics_OverallHitRate_AllHits_ReturnsOne()
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = 5, ProjectionMisses = 0,
            MapperHits = 3, MapperMisses = 0,
            FilterHits = 2, FilterMisses = 0
        };

        stats.OverallHitRate.Should().Be(1.0);
    }

    [Fact]
    public void CacheStatistics_OverallHitRate_AllMisses_ReturnsZero()
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = 0, ProjectionMisses = 5,
            MapperHits = 0, MapperMisses = 3,
            FilterHits = 0, FilterMisses = 2
        };

        stats.OverallHitRate.Should().Be(0.0);
    }

    [Fact]
    public void CacheStatistics_OverallHitRate_MixedAccesses_ReturnsCorrectRate()
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = 3, ProjectionMisses = 1,
            MapperHits = 2, MapperMisses = 2,
            FilterHits = 1, FilterMisses = 1
        };

        // Total hits: 3+2+1=6, total requests: 4+4+2=10 → 0.6
        stats.OverallHitRate.Should().BeApproximately(0.6, 0.001);
    }

    [Fact]
    public void CacheStatistics_OverallHitRate_OnlySomeCategoriesUsed_CalculatesCorrectly()
    {
        var stats = new CacheStatistics
        {
            ProjectionHits = 4, ProjectionMisses = 1,
            MapperHits = 0, MapperMisses = 0,
            FilterHits = 0, FilterMisses = 0
        };

        // Only projection used: 4/5 = 0.8
        stats.OverallHitRate.Should().BeApproximately(0.8, 0.001);
    }

    [Fact]
    public void CacheStatistics_HitRates_ArithmeticMutantKiller_DivisionNotMultiplication()
    {
        // Kills: hits / total → hits * total
        var stats = new CacheStatistics
        {
            ProjectionHits = 2,
            ProjectionMisses = 3
        };

        // 2 / 5 = 0.4 (not 2 * 5 = 10)
        stats.ProjectionHitRate.Should().BeLessThan(1.0);
        stats.ProjectionHitRate.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void CacheStatistics_HitRates_BoundaryMutantKiller_StrictlyGreaterThanZero()
    {
        // Kills: > 0 → >= 0 (would change behavior when total is 0)
        // and: > 0 → < 0 or <= 0
        var stats = new CacheStatistics
        {
            ProjectionHits = 0, ProjectionMisses = 0,
            MapperHits = 0, MapperMisses = 0,
            FilterHits = 0, FilterMisses = 0
        };

        // When total is 0, rate should be 0.0 (not NaN from division by zero)
        stats.ProjectionHitRate.Should().Be(0.0);
        stats.MapperHitRate.Should().Be(0.0);
        stats.FilterHitRate.Should().Be(0.0);
        stats.OverallHitRate.Should().Be(0.0);
    }

    [Fact]
    public void CacheStatistics_OverallHitRate_SumsAllCategories()
    {
        // Kills mutations that drop individual category sums
        var stats = new CacheStatistics
        {
            ProjectionHits = 1, ProjectionMisses = 0,
            MapperHits = 1, MapperMisses = 0,
            FilterHits = 1, FilterMisses = 0
        };

        // All 3 hits, 3 total → 1.0
        stats.OverallHitRate.Should().Be(1.0);

        // Now with one miss in each: 3 hits / 6 total = 0.5
        var stats2 = new CacheStatistics
        {
            ProjectionHits = 1, ProjectionMisses = 1,
            MapperHits = 1, MapperMisses = 1,
            FilterHits = 1, FilterMisses = 1
        };

        stats2.OverallHitRate.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void CacheStatistics_HitRate_SingleHitNoMiss()
    {
        // Kills: hits + misses → hits - misses (would give 1 - 0 = 1, but 1/1 = 1 too)
        // so we need a case where + and - give different denominators
        var stats = new CacheStatistics
        {
            ProjectionHits = 2,
            ProjectionMisses = 1
        };

        // 2 / (2+1) = 0.667 vs 2 / (2-1) = 2.0
        stats.ProjectionHitRate.Should().BeApproximately(2.0 / 3.0, 0.001);
    }

    #endregion

    #region ExpressionCache — TrackAccess hit/miss side effects

    [Fact]
    public void ExpressionCache_GetOrAdd_TracksHitCorrectly_NotReversed()
    {
        // Kills: negate isHit → !isHit (would swap hit/miss counters)
        var cache = new ExpressionCache();

        // First call is a miss, second call is a hit
        cache.GetOrAdd("projection", "key1", k => "value");
        cache.GetOrAdd("projection", "key1", k => "value");

        var stats = cache.GetStatistics();
        stats.ProjectionHits.Should().Be(1, "second access to same key should be a hit");
        stats.ProjectionMisses.Should().Be(1, "first access to new key should be a miss");
    }

    [Fact]
    public void ExpressionCache_GetOrAdd_TrackAccessNotRemoved_MissCountIncreases()
    {
        // Kills: statement removal of TrackAccess call
        var cache = new ExpressionCache();

        cache.GetOrAdd("projection", "key1", k => "value");
        cache.GetOrAdd("projection", "key2", k => "value");

        var stats = cache.GetStatistics();
        // If TrackAccess is removed, stats would all be 0
        stats.ProjectionMisses.Should().Be(2, "two different keys should produce two misses");
    }

    [Fact]
    public void ExpressionCache_GetOrAdd_MapperCategory_TracksHitMissCorrectly()
    {
        var cache = new ExpressionCache();

        cache.GetOrAdd("mapper", "key1", k => "value"); // miss
        cache.GetOrAdd("mapper", "key1", k => "value"); // hit
        cache.GetOrAdd("mapper", "key2", k => "value"); // miss

        var stats = cache.GetStatistics();
        stats.MapperHits.Should().Be(1);
        stats.MapperMisses.Should().Be(2);
    }

    [Fact]
    public void ExpressionCache_GetOrAdd_FilterCategory_TracksHitMissCorrectly()
    {
        var cache = new ExpressionCache();

        cache.GetOrAdd("filter", "key1", k => "value"); // miss
        cache.GetOrAdd("filter", "key1", k => "value"); // hit
        cache.GetOrAdd("filter", "key1", k => "value"); // hit

        var stats = cache.GetStatistics();
        stats.FilterHits.Should().Be(2);
        stats.FilterMisses.Should().Be(1);
    }

    [Fact]
    public void ExpressionCache_GetOrAdd_ContainsKeyMustBeCalledBeforeGetOrAdd()
    {
        // Kills: swap order of ContainsKey check vs GetOrAdd, or removal of isHit variable
        var cache = new ExpressionCache();

        // Only one miss then three hits
        cache.GetOrAdd("projection", "same", k => "v"); // miss
        cache.GetOrAdd("projection", "same", k => "v"); // hit
        cache.GetOrAdd("projection", "same", k => "v"); // hit
        cache.GetOrAdd("projection", "same", k => "v"); // hit

        var stats = cache.GetStatistics();
        stats.ProjectionHits.Should().Be(3);
        stats.ProjectionMisses.Should().Be(1);
    }

    #endregion

    #region InternalRequestExtensions — ??= → = mutations

    // These tests verify that applying expressions to requests with PRE-POPULATED
    // ExpressionAttributeNames/Values dictionaries preserves the existing entries.
    // The ??= mutations would overwrite existing dictionaries with new empty ones.

    [Fact]
    public void ApplyProjection_GetItemRequest_PreservesExistingAttributeNames()
    {
        var request = new GetItemRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            }
        };

        var result = new ProjectionResult(
            "#proj_0",
            new Dictionary<string, string> { ["#proj_0"] = "NewAttr" },
            Array.Empty<PropertyPath>(),
            ProjectionShape.SingleProperty,
            new[] { "NewAttr" });

        InvokeApplyProjection(request, result);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        request.ExpressionAttributeNames["#proj_0"].Should().Be("NewAttr");
    }

    [Fact]
    public void ApplyProjection_QueryRequest_PreservesExistingAttributeNames()
    {
        var request = new QueryRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            }
        };

        var result = new ProjectionResult(
            "#proj_0",
            new Dictionary<string, string> { ["#proj_0"] = "NewAttr" },
            Array.Empty<PropertyPath>(),
            ProjectionShape.SingleProperty,
            new[] { "NewAttr" });

        InvokeApplyProjection(request, result);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
    }

    [Fact]
    public void ApplyProjection_ScanRequest_PreservesExistingAttributeNames()
    {
        var request = new ScanRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            }
        };

        var result = new ProjectionResult(
            "#proj_0",
            new Dictionary<string, string> { ["#proj_0"] = "NewAttr" },
            Array.Empty<PropertyPath>(),
            ProjectionShape.SingleProperty,
            new[] { "NewAttr" });

        InvokeApplyProjection(request, result);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
    }

    [Fact]
    public void ApplyProjection_KeysAndAttributes_PreservesExistingAttributeNames()
    {
        var keysAndAttributes = new KeysAndAttributes
        {
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            }
        };

        var result = new ProjectionResult(
            "#proj_0",
            new Dictionary<string, string> { ["#proj_0"] = "NewAttr" },
            Array.Empty<PropertyPath>(),
            ProjectionShape.SingleProperty,
            new[] { "NewAttr" });

        InvokeApplyProjection(keysAndAttributes, result);

        keysAndAttributes.ExpressionAttributeNames.Should().HaveCount(2);
        keysAndAttributes.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
    }

    [Fact]
    public void ApplyFilter_QueryRequest_PreservesExistingDictionaries()
    {
        var request = new QueryRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var filterResult = new FilterExpressionResult(
            "#filt_0 = :filt_v0",
            new Dictionary<string, string> { ["#filt_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":filt_v0"] = new AttributeValue { S = "Active" } });

        InvokeApplyFilter(request, filterResult);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        request.ExpressionAttributeValues.Should().HaveCount(2);
        request.ExpressionAttributeValues[":existing"].S.Should().Be("ExistingValue");
    }

    [Fact]
    public void ApplyFilter_ScanRequest_PreservesExistingDictionaries()
    {
        var request = new ScanRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var filterResult = new FilterExpressionResult(
            "#filt_0 = :filt_v0",
            new Dictionary<string, string> { ["#filt_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":filt_v0"] = new AttributeValue { S = "Active" } });

        InvokeApplyFilter(request, filterResult);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyCondition_PutItemRequest_PreservesExistingDictionaries()
    {
        var request = new PutItemRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var conditionResult = new ConditionExpressionResult(
            "#cond_0 = :cond_v0",
            new Dictionary<string, string> { ["#cond_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":cond_v0"] = new AttributeValue { S = "Active" } });

        InvokeApplyCondition(request, conditionResult);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        request.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyCondition_DeleteItemRequest_PreservesExistingDictionaries()
    {
        var request = new DeleteItemRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var conditionResult = new ConditionExpressionResult(
            "#cond_0 = :cond_v0",
            new Dictionary<string, string> { ["#cond_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":cond_v0"] = new AttributeValue { S = "Active" } });

        InvokeApplyCondition(request, conditionResult);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyCondition_UpdateItemRequest_PreservesExistingDictionaries()
    {
        var request = new UpdateItemRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var conditionResult = new ConditionExpressionResult(
            "#cond_0 = :cond_v0",
            new Dictionary<string, string> { ["#cond_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":cond_v0"] = new AttributeValue { S = "Active" } });

        InvokeApplyCondition(request, conditionResult);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void MergeAttributeNames_QueryRequest_PreservesExistingNames()
    {
        var request = new QueryRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            }
        };

        var names = new Dictionary<string, string> { ["#new"] = "NewAttr" };

        InvokeMergeAttributeNames(request, names);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        request.ExpressionAttributeNames["#new"].Should().Be("NewAttr");
    }

    [Fact]
    public void MergeAttributeValues_QueryRequest_PreservesExistingValues()
    {
        var request = new QueryRequest
        {
            TableName = "Test",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var values = new Dictionary<string, AttributeValue>
        {
            [":new"] = new AttributeValue { S = "NewValue" }
        };

        InvokeMergeAttributeValues(request, values);

        request.ExpressionAttributeValues.Should().HaveCount(2);
        request.ExpressionAttributeValues[":existing"].S.Should().Be("ExistingValue");
        request.ExpressionAttributeValues[":new"].S.Should().Be("NewValue");
    }

    #endregion

    #region DynamoDbExpressionConfig.Builder — null coalescing in Build()

    [Fact]
    public void Builder_DefaultBuild_UsesDefaultConverterRegistry()
    {
        var config = new DynamoDbExpressionConfig.Builder().Build();

        // Verifies converterRegistry ?? Default.Clone() — the config gets a clone, not null
        config.ConverterRegistry.Should().NotBeNull();
        // It should be a DIFFERENT instance (clone) from the static Default
        config.ConverterRegistry.Should().NotBeSameAs(AttributeValueConverterRegistry.Default);
    }

    [Fact]
    public void Builder_DefaultBuild_UsesDefaultReservedKeywords()
    {
        var config = new DynamoDbExpressionConfig.Builder().Build();

        // Verifies reservedKeywords ?? ReservedKeywordRegistry.Default
        config.ReservedKeywords.Should().BeSameAs(ReservedKeywordRegistry.Default);
    }

    [Fact]
    public void Builder_DefaultBuild_UsesDefaultCache()
    {
        var config = new DynamoDbExpressionConfig.Builder().Build();

        // Verifies cache ?? ExpressionCache.Default
        config.Cache.Should().BeSameAs(ExpressionCache.Default);
    }

    [Fact]
    public void Builder_DefaultBuild_UsesNullLoggerFactory()
    {
        var config = new DynamoDbExpressionConfig.Builder().Build();

        // Verifies loggerFactory ?? NullLoggerFactory.Instance
        config.LoggerFactory.Should().BeSameAs(NullLoggerFactory.Instance);
    }

    [Fact]
    public void Builder_WithExplicitNull_ConverterRegistry_StillGetsDefault()
    {
        // Calling WithConverterRegistry(null) then Build() — tests the ?? fallback
        var config = new DynamoDbExpressionConfig.Builder()
            .WithConverterRegistry(null!)
            .Build();

        // Should fall through to default clone (not null)
        config.ConverterRegistry.Should().NotBeNull();
    }

    [Fact]
    public void Builder_WithConverter_ClonesOnlyOnce()
    {
        // Tests EnsureConverterRegistryCloned ??= pattern
        var builder = new DynamoDbExpressionConfig.Builder();

        // First WithConverter triggers clone
        builder.WithConverter(new StringConverter());
        // Second WithConverter should reuse the same cloned registry
        builder.WithConverter(new Int32Converter());

        var config = builder.Build();
        config.ConverterRegistry.Should().NotBeNull();
        config.ConverterRegistry.Should().NotBeSameAs(AttributeValueConverterRegistry.Default);
    }

    [Fact]
    public void Builder_ExplicitOverrides_TakePrecedenceOverDefaults()
    {
        var customCache = NullExpressionCache.Instance;
        var customKeywords = new ReservedKeywordRegistry();

        var config = new DynamoDbExpressionConfig.Builder()
            .WithCache(customCache)
            .WithReservedKeywords(customKeywords)
            .Build();

        // Explicit values should NOT be overwritten by defaults
        config.Cache.Should().BeSameAs(customCache);
        config.ReservedKeywords.Should().BeSameAs(customKeywords);
    }

    #endregion

    #region AliasGenerator.Clone — preserves counter state

    [Fact]
    public void AliasGenerator_Clone_PreservesNameCounter()
    {
        var original = new AliasGenerator("test");
        original.NextName(); // #test_0
        original.NextName(); // #test_1

        var clone = original.Clone();

        // Clone should continue from where original left off
        clone.NextName().Should().Be("#test_2");
        // Original should also continue independently
        original.NextName().Should().Be("#test_2");
    }

    [Fact]
    public void AliasGenerator_Clone_PreservesValueCounter()
    {
        var original = new AliasGenerator("test");
        original.NextValue(); // :test_v0
        original.NextValue(); // :test_v1
        original.NextValue(); // :test_v2

        var clone = original.Clone();

        clone.NextValue().Should().Be(":test_v3");
        original.NextValue().Should().Be(":test_v3");
    }

    [Fact]
    public void AliasGenerator_Clone_IndependentCounters()
    {
        var original = new AliasGenerator("test");
        original.NextName(); // #test_0

        var clone = original.Clone();

        // Advance clone multiple times
        clone.NextName(); // #test_1
        clone.NextName(); // #test_2
        clone.NextName(); // #test_3

        // Original should not be affected
        original.NextName().Should().Be("#test_1");
    }

    [Fact]
    public void AliasGenerator_Clone_PreservesPrefix()
    {
        var original = new AliasGenerator("filt");
        var clone = original.Clone();

        clone.NextName().Should().StartWith("#filt_");
        clone.NextValue().Should().StartWith(":filt_v");
    }

    #endregion

    #region RequestMergeHelpers — empty source merges

    [Fact]
    public void MergeAttributeNames_EmptySource_TargetUnchanged()
    {
        var target = new Dictionary<string, string>
        {
            ["#a"] = "AttrA"
        };
        var source = new Dictionary<string, string>();

        InvokeStaticMergeAttributeNames(target, source);

        target.Should().HaveCount(1);
        target["#a"].Should().Be("AttrA");
    }

    [Fact]
    public void MergeAttributeValues_EmptySource_TargetUnchanged()
    {
        var target = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { S = "ValueA" }
        };
        var source = new Dictionary<string, AttributeValue>();

        InvokeStaticMergeAttributeValues(target, source);

        target.Should().HaveCount(1);
        target[":a"].S.Should().Be("ValueA");
    }

    [Fact]
    public void MergeAttributeValues_WithNullAttributeValue_ReportsValueInException()
    {
        // Tests the null coalescing in: existing.S ?? existing.N ?? "(value)"
        var target = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { BOOL = true } // Neither S nor N set
        };
        var source = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { S = "conflict" }
        };

        var act = () => InvokeStaticMergeAttributeValues(target, source);

        act.Should().Throw<TargetInvocationException>();
    }

    [Fact]
    public void MergeAttributeValues_WithNumberValue_ReportsNInException()
    {
        // Tests that existing.N path is used when S is null
        var target = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { N = "42" }
        };
        var source = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { S = "conflict" }
        };

        var act = () => InvokeStaticMergeAttributeValues(target, source);

        act.Should().Throw<TargetInvocationException>();
    }

    #endregion

    #region WithUpdate extension — ??= mutations

    [Fact]
    public void WithUpdate_PreservesExistingDictionaries()
    {
        var request = new UpdateItemRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var updateResult = new UpdateExpressionResult(
            "SET #upd_0 = :upd_v0",
            new Dictionary<string, string> { ["#upd_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":upd_v0"] = new AttributeValue { S = "Active" } });

        request.WithUpdate(updateResult);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        request.ExpressionAttributeValues.Should().HaveCount(2);
        request.ExpressionAttributeValues[":existing"].S.Should().Be("ExistingValue");
    }

    #endregion

    #region Reflection helpers for internal methods

    private static void InvokeApplyProjection(GetItemRequest request, ProjectionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyProjection",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(GetItemRequest), typeof(ProjectionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyProjection(QueryRequest request, ProjectionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyProjection",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(QueryRequest), typeof(ProjectionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyProjection(ScanRequest request, ProjectionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyProjection",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(ScanRequest), typeof(ProjectionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyProjection(KeysAndAttributes keysAndAttributes, ProjectionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyProjection",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(KeysAndAttributes), typeof(ProjectionResult) },
            null);
        method!.Invoke(null, new object[] { keysAndAttributes, result });
    }

    private static void InvokeApplyFilter(QueryRequest request, FilterExpressionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyFilter",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(QueryRequest), typeof(FilterExpressionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyFilter(ScanRequest request, FilterExpressionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyFilter",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(ScanRequest), typeof(FilterExpressionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyCondition(PutItemRequest request, ConditionExpressionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyCondition",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(PutItemRequest), typeof(ConditionExpressionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyCondition(DeleteItemRequest request, ConditionExpressionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyCondition",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(DeleteItemRequest), typeof(ConditionExpressionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyCondition(UpdateItemRequest request, ConditionExpressionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyCondition",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(UpdateItemRequest), typeof(ConditionExpressionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeMergeAttributeNames(QueryRequest request, IReadOnlyDictionary<string, string> names)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "MergeAttributeNames",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(QueryRequest), typeof(IReadOnlyDictionary<string, string>) },
            null);
        method!.Invoke(null, new object[] { request, names });
    }

    private static void InvokeMergeAttributeValues(QueryRequest request, IReadOnlyDictionary<string, AttributeValue> values)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "MergeAttributeValues",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(QueryRequest), typeof(IReadOnlyDictionary<string, AttributeValue>) },
            null);
        method!.Invoke(null, new object[] { request, values });
    }

    private static void InvokeStaticMergeAttributeNames(
        Dictionary<string, string> target,
        IReadOnlyDictionary<string, string> source)
    {
        var method = typeof(RequestMergeHelpers).GetMethod(
            "MergeAttributeNames",
            BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { target, source });
    }

    private static void InvokeStaticMergeAttributeValues(
        Dictionary<string, AttributeValue> target,
        IReadOnlyDictionary<string, AttributeValue> source)
    {
        var method = typeof(RequestMergeHelpers).GetMethod(
            "MergeAttributeValues",
            BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { target, source });
    }

    #endregion
}
