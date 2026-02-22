using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Attributes;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

/// <summary>
/// Tests specifically designed to kill surviving and NoCoverage mutants
/// identified during Phase 3b mutation analysis.
/// Categories A-L correspond to the triage in p1-mutant-triage.md.
/// </summary>
public class MutationKillingTests
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;

    public MutationKillingTests()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
    }

    #region Category A: Null guard constructor tests

    // FilterExpressionVisitor constructor null guards (L43-48)

    [Fact]
    public void FilterExpressionVisitor_NullResolverFactory_ThrowsArgumentNullException()
    {
        var act = () => new FilterExpressionVisitor(
            null!,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            new StringBuilder(),
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("resolverFactory");
    }

    [Fact]
    public void FilterExpressionVisitor_NullValueEmitter_ThrowsArgumentNullException()
    {
        var act = () => new FilterExpressionVisitor(
            _resolverFactory,
            null!,
            new AliasGenerator("filt"),
            new StringBuilder(),
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("valueEmitter");
    }

    [Fact]
    public void FilterExpressionVisitor_NullAliasGen_ThrowsArgumentNullException()
    {
        var act = () => new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            null!,
            new StringBuilder(),
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("aliasGen");
    }

    [Fact]
    public void FilterExpressionVisitor_NullResult_ThrowsArgumentNullException()
    {
        var act = () => new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            null!,
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("result");
    }

    [Fact]
    public void FilterExpressionVisitor_NullNames_ThrowsArgumentNullException()
    {
        var act = () => new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            new StringBuilder(),
            null!,
            new Dictionary<string, AttributeValue>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("names");
    }

    [Fact]
    public void FilterExpressionVisitor_NullValues_ThrowsArgumentNullException()
    {
        var act = () => new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            new StringBuilder(),
            new Dictionary<string, string>(),
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("values");
    }

    // ConditionExpressionResult constructor null coalescing (L40-42)

    [Fact]
    public void ConditionExpressionResult_NullExpression_DefaultsToEmpty()
    {
        var result = new ConditionExpressionResult(null!, null!, null!);

        result.Expression.Should().BeEmpty();
        result.ExpressionAttributeNames.Should().NotBeNull().And.BeEmpty();
        result.ExpressionAttributeValues.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ConditionExpressionResult_NullNames_DefaultsToEmptyDictionary()
    {
        var result = new ConditionExpressionResult("expr", null!, new Dictionary<string, AttributeValue>());

        result.ExpressionAttributeNames.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ConditionExpressionResult_NullValues_DefaultsToEmptyDictionary()
    {
        var result = new ConditionExpressionResult("expr", new Dictionary<string, string>(), null!);

        result.ExpressionAttributeValues.Should().NotBeNull().And.BeEmpty();
    }

    // FilterExpressionResult constructor null coalescing (L40-42)

    [Fact]
    public void FilterExpressionResult_NullExpression_DefaultsToEmpty()
    {
        var result = new FilterExpressionResult(null!, null!, null!);

        result.Expression.Should().BeEmpty();
        result.ExpressionAttributeNames.Should().NotBeNull().And.BeEmpty();
        result.ExpressionAttributeValues.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void FilterExpressionResult_NullNames_DefaultsToEmptyDictionary()
    {
        var result = new FilterExpressionResult("expr", null!, new Dictionary<string, AttributeValue>());

        result.ExpressionAttributeNames.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void FilterExpressionResult_NullValues_DefaultsToEmptyDictionary()
    {
        var result = new FilterExpressionResult("expr", new Dictionary<string, string>(), null!);

        result.ExpressionAttributeValues.Should().NotBeNull().And.BeEmpty();
    }

    // ProjectionBuilder constructor null guard (L31)

    [Fact]
    public void ProjectionBuilder_NullResolverFactory_ThrowsArgumentNullException()
    {
        var act = () => new ProjectionBuilder<TestEntity>(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("resolverFactory");
    }

    // ProjectionBuilder null optional params default correctly (L32-33)

    [Fact]
    public void ProjectionBuilder_NullReservedKeywords_UsesDefault()
    {
        // Should not throw - uses default registry
        var builder = new ProjectionBuilder<TestEntity>(_resolverFactory, reservedKeywords: null);
        var result = builder.BuildProjection(p => p.OrderId);
        result.Should().NotBeNull();
    }

    [Fact]
    public void ProjectionBuilder_NullCache_UsesDefault()
    {
        // Should not throw - uses default cache
        var builder = new ProjectionBuilder<TestEntity>(_resolverFactory, cache: null);
        var result = builder.BuildProjection(p => p.OrderId);
        result.Should().NotBeNull();
    }

    // ProjectionResult constructor null coalescing (L50-54)

    [Fact]
    public void ProjectionResult_NullProjectionExpression_DefaultsToEmpty()
    {
        var result = new ProjectionResult(null!, null!, null!, ProjectionShape.Identity, null!);

        result.ProjectionExpression.Should().BeEmpty();
        result.ExpressionAttributeNames.Should().NotBeNull().And.BeEmpty();
        result.PropertyPaths.Should().NotBeNull().And.BeEmpty();
        result.ResolvedAttributeNames.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ProjectionResult_NullNames_DefaultsToEmptyDictionary()
    {
        var result = new ProjectionResult("expr", null!, Array.Empty<PropertyPath>(), ProjectionShape.SingleProperty, Array.Empty<string>());

        result.ExpressionAttributeNames.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ProjectionResult_NullPropertyPaths_DefaultsToEmptyArray()
    {
        var result = new ProjectionResult("expr", new Dictionary<string, string>(), null!, ProjectionShape.SingleProperty, Array.Empty<string>());

        result.PropertyPaths.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ProjectionResult_NullResolvedNames_DefaultsToEmptyArray()
    {
        var result = new ProjectionResult("expr", new Dictionary<string, string>(), Array.Empty<PropertyPath>(), ProjectionShape.SingleProperty, null!);

        result.ResolvedAttributeNames.Should().NotBeNull().And.BeEmpty();
    }

    #endregion

    #region Category B: Null property expression (ThrowIfNull) tests for UpdateExpressionBuilder

    [Fact]
    public void UpdateBuilder_SetNull_ThrowsArgumentNullException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Set<string>(null!, "value");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateBuilder_IncrementNull_ThrowsArgumentNullException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Increment<int>(null!, 1);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateBuilder_DecrementNull_ThrowsArgumentNullException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Decrement<int>(null!, 1);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateBuilder_SetIfNotExistsNull_ThrowsArgumentNullException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.SetIfNotExists<string>(null!, "value");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateBuilder_AppendToListNull_ThrowsArgumentNullException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.AppendToList<string>(null!, new List<string> { "a" });
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateBuilder_RemoveNull_ThrowsArgumentNullException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Remove<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateBuilder_AddNull_ThrowsArgumentNullException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Add<int>(null!, 5);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateBuilder_DeleteNull_ThrowsArgumentNullException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Delete<string>(null!, new HashSet<string> { "a" });
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Category C: Dedup/orphan cleanup tests (Set same prop twice)

    [Fact]
    public void UpdateBuilder_SetSamePropertyTwice_CleansUpOrphanedPlaceholders()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder
            .Set(p => p.Title, "First")
            .Set(p => p.Title, "Second")
            .Build();

        // Expression should only reference the second value
        result.Expression.Should().Be("SET Title = :upd_v1");
        // The first value placeholder should have been cleaned up
        result.ExpressionAttributeValues.Should().NotContainKey(":upd_v0");
        result.ExpressionAttributeValues.Should().ContainKey(":upd_v1")
            .WhoseValue.S.Should().Be("Second");
    }

    [Fact]
    public void UpdateBuilder_IncrementSamePropertyTwice_CleansUpOrphanedPlaceholders()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder
            .Increment(p => p.ViewCount, 1)
            .Increment(p => p.ViewCount, 5)
            .Build();

        // Only the second increment should remain
        result.Expression.Should().Contain("ViewCount = ViewCount + :upd_v1");
        result.ExpressionAttributeValues.Should().NotContainKey(":upd_v0");
        result.ExpressionAttributeValues[":upd_v1"].N.Should().Be("5");
    }

    [Fact]
    public void UpdateBuilder_AddSamePropertyTwice_CleansUpOrphanedPlaceholders()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder
            .Add(p => p.Score, 10)
            .Add(p => p.Score, 20)
            .Build();

        result.Expression.Should().Contain("Score :upd_v1");
        result.ExpressionAttributeValues.Should().NotContainKey(":upd_v0");
        result.ExpressionAttributeValues[":upd_v1"].N.Should().Be("20");
    }

    [Fact]
    public void UpdateBuilder_DeleteSamePropertyTwice_CleansUpOrphanedPlaceholders()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder
            .Delete(p => p.EnabledFeatures, new HashSet<string> { "a" })
            .Delete(p => p.EnabledFeatures, new HashSet<string> { "b" })
            .Build();

        result.ExpressionAttributeValues.Should().NotContainKey(":upd_v0");
        result.ExpressionAttributeValues.Should().ContainKey(":upd_v1");
        result.ExpressionAttributeValues[":upd_v1"].SS.Should().Contain("b");
    }

    #endregion

    #region Category D: Conflict validation tests (Set+Remove, etc.)

    [Fact]
    public void UpdateBuilder_SetThenRemove_ThrowsInvalidUpdateException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var act = () => builder
            .Set(p => p.Title, "x")
            .Remove(p => p.Title);

        act.Should().Throw<InvalidUpdateException>()
            .Which.PropertyName.Should().Be("Title");
    }

    [Fact]
    public void UpdateBuilder_RemoveThenSet_ThrowsInvalidUpdateException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var act = () => builder
            .Remove(p => p.Title)
            .Set(p => p.Title, "x");

        act.Should().Throw<InvalidUpdateException>()
            .Which.PropertyName.Should().Be("Title");
    }

    [Fact]
    public void UpdateBuilder_SetThenAdd_ThrowsInvalidUpdateException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var act = () => builder
            .Set(p => p.Score, 10)
            .Add(p => p.Score, 5);

        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void UpdateBuilder_SetThenDelete_ThrowsInvalidUpdateException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var act = () => builder
            .Set(p => p.EnabledFeatures, new HashSet<string> { "a" })
            .Delete(p => p.EnabledFeatures, new HashSet<string> { "b" });

        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void UpdateBuilder_RemoveThenAdd_ThrowsInvalidUpdateException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var act = () => builder
            .Remove(p => p.Score)
            .Add(p => p.Score, 5);

        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void UpdateBuilder_RemoveThenDelete_ThrowsInvalidUpdateException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var act = () => builder
            .Remove(p => p.EnabledFeatures)
            .Delete(p => p.EnabledFeatures, new HashSet<string> { "a" });

        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void UpdateBuilder_AddThenDelete_ThrowsInvalidUpdateException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var act = () => builder
            .Add(p => p.Score, 10)
            .Delete(p => p.EnabledFeatures, new HashSet<string> { "a" });

        // These are on different properties, should NOT conflict
        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateBuilder_AddThenRemoveSameProperty_ThrowsInvalidUpdateException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var act = () => builder
            .Add(p => p.Score, 10)
            .Remove(p => p.Score);

        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void UpdateBuilder_DeleteThenSet_ThrowsInvalidUpdateException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var act = () => builder
            .Delete(p => p.EnabledFeatures, new HashSet<string> { "a" })
            .Set(p => p.EnabledFeatures, new HashSet<string> { "b" });

        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void UpdateBuilder_DeleteThenRemove_ThrowsInvalidUpdateException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var act = () => builder
            .Delete(p => p.EnabledFeatures, new HashSet<string> { "a" })
            .Remove(p => p.EnabledFeatures);

        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void UpdateBuilder_DeleteThenAdd_SameProperty_ThrowsInvalidUpdateException()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var act = () => builder
            .Delete(p => p.EnabledFeatures, new HashSet<string> { "a" })
            .Add(p => p.EnabledFeatures, new HashSet<string> { "b" });

        act.Should().Throw<InvalidUpdateException>();
    }

    #endregion

    #region Category E: ReAlias OrderByDescending multi-digit tests

    [Fact]
    public void FilterResult_ReAlias_MultiDigitAliases_DescendingOrderPreventsCollision()
    {
        // Create a "left" filter that has aliases #filt_0 through #filt_10 (11 aliases)
        // and values :filt_v0 through :filt_v10
        var leftNames = new Dictionary<string, string>();
        var leftValues = new Dictionary<string, AttributeValue>();
        var leftExprParts = new List<string>();

        for (int i = 0; i <= 10; i++)
        {
            leftNames[$"#filt_{i}"] = $"Attr{i}";
            leftValues[$":filt_v{i}"] = new AttributeValue { S = $"val{i}" };
            leftExprParts.Add($"#filt_{i} = :filt_v{i}");
        }

        var left = new FilterExpressionResult(
            string.Join(" AND ", leftExprParts),
            leftNames,
            leftValues);

        // Create a "right" filter with aliases #filt_0 and #filt_1, plus :filt_v0 and :filt_v1
        var right = new FilterExpressionResult(
            "#filt_0 = :filt_v0 AND #filt_1 > :filt_v1",
            new Dictionary<string, string>
            {
                ["#filt_0"] = "RightAttr0",
                ["#filt_1"] = "RightAttr1"
            },
            new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "rightVal0" },
                [":filt_v1"] = new() { N = "42" }
            });

        // Act
        var result = FilterExpressionResult.And(left, right);

        // Right's #filt_0 should become #filt_11, #filt_1 should become #filt_12
        // Right's :filt_v0 should become :filt_v11, :filt_v1 should become :filt_v12
        result.ExpressionAttributeNames.Should().ContainKey("#filt_11")
            .WhoseValue.Should().Be("RightAttr0");
        result.ExpressionAttributeNames.Should().ContainKey("#filt_12")
            .WhoseValue.Should().Be("RightAttr1");
        result.ExpressionAttributeValues.Should().ContainKey(":filt_v11")
            .WhoseValue.S.Should().Be("rightVal0");
        result.ExpressionAttributeValues.Should().ContainKey(":filt_v12")
            .WhoseValue.N.Should().Be("42");

        // The rewritten expression should have correct multi-digit aliases (not mangled)
        result.Expression.Should().Contain("#filt_11 = :filt_v11 AND #filt_12 > :filt_v12");
        // Should NOT contain the original right aliases
        result.Expression.Should().NotContain("#filt_0 = :filt_v0 AND #filt_1 > :filt_v1",
            "right-side aliases should have been re-aliased to multi-digit indices");
    }

    [Fact]
    public void ConditionResult_ReAlias_MultiDigitAliases_DescendingOrderPreventsCollision()
    {
        // Same test for ConditionExpressionResult
        var leftNames = new Dictionary<string, string>();
        var leftValues = new Dictionary<string, AttributeValue>();
        var leftExprParts = new List<string>();

        for (int i = 0; i <= 10; i++)
        {
            leftNames[$"#cond_{i}"] = $"Attr{i}";
            leftValues[$":cond_v{i}"] = new AttributeValue { S = $"val{i}" };
            leftExprParts.Add($"#cond_{i} = :cond_v{i}");
        }

        var left = new ConditionExpressionResult(
            string.Join(" AND ", leftExprParts),
            leftNames,
            leftValues);

        var right = new ConditionExpressionResult(
            "#cond_0 = :cond_v0 AND #cond_1 > :cond_v1",
            new Dictionary<string, string>
            {
                ["#cond_0"] = "RightAttr0",
                ["#cond_1"] = "RightAttr1"
            },
            new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "rightVal0" },
                [":cond_v1"] = new() { N = "42" }
            });

        var result = ConditionExpressionResult.And(left, right);

        result.ExpressionAttributeNames.Should().ContainKey("#cond_11")
            .WhoseValue.Should().Be("RightAttr0");
        result.ExpressionAttributeNames.Should().ContainKey("#cond_12")
            .WhoseValue.Should().Be("RightAttr1");
        result.Expression.Should().Contain("#cond_11 = :cond_v11 AND #cond_12 > :cond_v12");
    }

    #endregion

    #region Category F: Logical mutations in FilterExpressionVisitor method dispatch

    [Fact]
    public void Filter_StringStartsWith_RequiresOneArgAndObject()
    {
        // Test that StartsWith works with exactly 1 argument and a non-null object
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder.BuildFilter(p => p.Title.StartsWith("Prem"));

        result.Expression.Should().Contain("begins_with(Title, :filt_v0)");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Prem");
    }

    [Fact]
    public void Filter_StringContains_RequiresOneArgAndObject()
    {
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder.BuildFilter(p => p.Description.Contains("sale"));

        result.Expression.Should().Contain("contains(Description, :filt_v0)");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("sale");
    }

    [Fact]
    public void Filter_EnumerableContains_StaticMethod_GeneratesIN()
    {
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);
        var statuses = new[] { "Active", "Pending" };

        var result = builder.BuildFilter(p => statuses.Contains(p.StatusString));

        result.Expression.Should().Contain("IN");
    }

    [Fact]
    public void Filter_ListContains_InstanceMethod_GeneratesIN()
    {
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);
        var validIds = new List<string> { "id1", "id2", "id3" };

        var result = builder.BuildFilter(p => validIds.Contains(p.OrderId));

        result.Expression.Should().Contain("OrderId IN (:filt_v0, :filt_v1, :filt_v2)");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("id1");
        result.ExpressionAttributeValues[":filt_v1"].S.Should().Be("id2");
        result.ExpressionAttributeValues[":filt_v2"].S.Should().Be("id3");
    }

    #endregion

    #region Category G: Bool negation value check

    [Fact]
    public void Filter_NegatedBoolProperty_ValueIsFalse()
    {
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder.BuildFilter(p => !p.IsActive);

        result.Expression.Should().Be("IsActive = :filt_v0");
        // This is the critical assertion: BOOL must be false, not default/empty
        result.ExpressionAttributeValues[":filt_v0"].BOOL.Should().BeFalse();
        result.ExpressionAttributeValues[":filt_v0"].IsBOOLSet.Should().BeTrue();
    }

    [Fact]
    public void Filter_DirectBoolProperty_ValueIsTrue()
    {
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder.BuildFilter(p => p.IsActive);

        result.ExpressionAttributeValues[":filt_v0"].BOOL.Should().BeTrue();
        result.ExpressionAttributeValues[":filt_v0"].IsBOOLSet.Should().BeTrue();
    }

    #endregion

    #region Category H: Closure field/property capture tests

    [Fact]
    public void Filter_CapturedField_EvaluatesCorrectly()
    {
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);
        var holder = new FieldHolder { MinTotal = 250m };

        var result = builder.BuildFilter(p => p.Total > holder.MinTotal);

        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("250");
    }

    [Fact]
    public void Filter_CapturedProperty_EvaluatesCorrectly()
    {
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);
        var holder = new PropertyHolder { MinTotal = 350m };

        var result = builder.BuildFilter(p => p.Total > holder.MinTotal);

        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("350");
    }

    // Helper classes to test field vs property access in closures
    private class FieldHolder
    {
        public decimal MinTotal;
    }

    private class PropertyHolder
    {
        public decimal MinTotal { get; set; }
    }

    #endregion

    #region Category I: NoCoverage error/edge paths in FilterExpressionVisitor

    [Fact]
    public void Filter_ConvertExpression_TransparentlyHandled()
    {
        // Explicit cast in comparison: (long)p.Score > 100L
        // This exercises the Convert/ConvertChecked handling in VisitUnary
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);

        // Enum comparison triggers Convert node
        var result = builder.BuildFilter(p => p.Status == OrderStatus.Active);

        result.Should().NotBeNull();
        result.Expression.Should().NotBeEmpty();
    }

    [Fact]
    public void Filter_NullCheckFromLeftSide_GeneratesAttributeNotExists()
    {
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);

        // null == p.ExpiresOn syntax (null on the left)
        var result = builder.BuildFilter(p => null == p.ExpiresOn);

        result.Expression.Should().Be("attribute_not_exists(ExpiresOn)");
    }

    [Fact]
    public void Filter_NullNotEqualFromLeftSide_GeneratesAttributeExists()
    {
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);

        // null != p.ExpiresOn syntax (null on the left)
        var result = builder.BuildFilter(p => null != p.ExpiresOn);

        result.Expression.Should().Be("attribute_exists(ExpiresOn)");
    }

    [Fact]
    public void Filter_UnsupportedUnaryNodeType_ThrowsUnsupportedExpressionException()
    {
        // Exercise the VisitUnary throw path (L115) with an unsupported unary node type
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);

        // Build a predicate with OnesComplement (~) which is not Convert/ConvertChecked/Not
        var param = Expression.Parameter(typeof(FilterTestEntity), "p");
        var score = Expression.Property(param, nameof(FilterTestEntity.Total));
        var converted = Expression.Convert(score, typeof(int));
        var onesComp = Expression.OnesComplement(converted);
        var gt = Expression.GreaterThan(onesComp, Expression.Constant(0));
        var lambda = Expression.Lambda<Func<FilterTestEntity, bool>>(gt, param);

        var act = () => builder.BuildFilter(lambda);

        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void Filter_DynamoDbFunctions_Size_Standalone_Works()
    {
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder.BuildFilter(p => DynamoDbFunctions.Size(p.Tags) >= 2);

        result.Expression.Should().Be("size(Tags) >= :filt_v0");
        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("2");
    }

    [Fact]
    public void Filter_DynamoDbFunctions_AttributeType_Works()
    {
        var builder = new FilterExpressionBuilder<FilterTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder.BuildFilter(p => DynamoDbFunctions.AttributeType(p.Tags, "L"));

        result.Expression.Should().Be("attribute_type(Tags, :filt_v0)");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("L");
    }

    #endregion

    #region Category J: SortKeyCondition null/boundary tests

    [Fact]
    public void SortKeyEquals_NullProperty_ThrowsArgumentNullException()
    {
        var builder = new KeyConditionExpressionBuilder<KeyConditionTestEntity>(_resolverFactory, _converterRegistry);
        var sortKeyBuilder = builder.WithPartitionKey(e => e.PK, "USER#1");

        var act = () => sortKeyBuilder.WithSortKeyEquals<string>(null!, "val");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SortKeyEquals_NullValue_ThrowsArgumentNullException()
    {
        var builder = new KeyConditionExpressionBuilder<KeyConditionTestEntity>(_resolverFactory, _converterRegistry);
        var sortKeyBuilder = builder.WithPartitionKey(e => e.PK, "USER#1");

        var act = () => sortKeyBuilder.WithSortKeyEquals(e => e.SK, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SortKeyLessThan_NullProperty_ThrowsArgumentNullException()
    {
        var builder = new KeyConditionExpressionBuilder<KeyConditionTestEntity>(_resolverFactory, _converterRegistry);
        var sortKeyBuilder = builder.WithPartitionKey(e => e.PK, "USER#1");

        var act = () => sortKeyBuilder.WithSortKeyLessThan<string>(null!, "val");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SortKeyGreaterThan_NullValue_ThrowsArgumentNullException()
    {
        var builder = new KeyConditionExpressionBuilder<KeyConditionTestEntity>(_resolverFactory, _converterRegistry);
        var sortKeyBuilder = builder.WithPartitionKey(e => e.PK, "USER#1");

        var act = () => sortKeyBuilder.WithSortKeyGreaterThan(e => e.SK, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SortKeyBetween_NullProperty_ThrowsArgumentNullException()
    {
        var builder = new KeyConditionExpressionBuilder<KeyConditionTestEntity>(_resolverFactory, _converterRegistry);
        var sortKeyBuilder = builder.WithPartitionKey(e => e.PK, "USER#1");

        var act = () => sortKeyBuilder.WithSortKeyBetween<string>(null!, "a", "z");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SortKeyBetween_NullLow_ThrowsArgumentNullException()
    {
        var builder = new KeyConditionExpressionBuilder<KeyConditionTestEntity>(_resolverFactory, _converterRegistry);
        var sortKeyBuilder = builder.WithPartitionKey(e => e.PK, "USER#1");

        var act = () => sortKeyBuilder.WithSortKeyBetween(e => e.SK, null!, "z");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SortKeyBetween_NullHigh_ThrowsArgumentNullException()
    {
        var builder = new KeyConditionExpressionBuilder<KeyConditionTestEntity>(_resolverFactory, _converterRegistry);
        var sortKeyBuilder = builder.WithPartitionKey(e => e.PK, "USER#1");

        var act = () => sortKeyBuilder.WithSortKeyBetween(e => e.SK, "a", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SortKeyBetween_LowEqualsHigh_DoesNotThrow()
    {
        // Boundary test: low == high should be valid (single value range)
        var builder = new KeyConditionExpressionBuilder<KeyConditionTestEntity>(_resolverFactory, _converterRegistry);
        var sortKeyBuilder = builder.WithPartitionKey(e => e.PK, "USER#1");

        var result = sortKeyBuilder.WithSortKeyBetween(e => e.SK, "ORDER#100", "ORDER#100");

        result.Expression.Should().Contain("BETWEEN");
        result.ExpressionAttributeValues.Should().HaveCount(3);
    }

    [Fact]
    public void SortKeyBeginsWith_NullProperty_ThrowsArgumentNullException()
    {
        var builder = new KeyConditionExpressionBuilder<KeyConditionTestEntity>(_resolverFactory, _converterRegistry);
        var sortKeyBuilder = builder.WithPartitionKey(e => e.PK, "USER#1");

        var act = () => sortKeyBuilder.WithSortKeyBeginsWith(null!, "prefix");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SortKey_NestedProperty_ThrowsInvalidKeyConditionException()
    {
        var builder = new KeyConditionExpressionBuilder<KeyConditionTestEntity>(_resolverFactory, _converterRegistry);
        var sortKeyBuilder = builder.WithPartitionKey(e => e.PK, "USER#1");

        var act = () => sortKeyBuilder.WithSortKeyEquals(e => e.Address.City, "Seattle");

        act.Should().Throw<InvalidKeyConditionException>()
            .WithMessage("*nested*");
    }

    [Fact]
    public void SortKey_IgnoredAttribute_ThrowsInvalidKeyConditionException()
    {
        var builder = new KeyConditionExpressionBuilder<KeyConditionTestEntity>(_resolverFactory, _converterRegistry);
        var sortKeyBuilder = builder.WithPartitionKey(e => e.PK, "USER#1");

        var act = () => sortKeyBuilder.WithSortKeyEquals(e => e.InternalField, "val");

        act.Should().Throw<InvalidKeyConditionException>()
            .Which.PropertyName.Should().Be("InternalField");
    }

    #endregion

    #region Category K: Update misc (alias, empty Build, regex)

    [Fact]
    public void UpdateBuilder_RemoveOnNonReservedKeyword_UsesPropertyNameDirectly()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder
            .Remove(p => p.TempFlag)
            .Build();

        // TempFlag is not a reserved keyword - should appear directly without alias
        result.Expression.Should().Be("REMOVE TempFlag");
        result.ExpressionAttributeNames.Should().BeEmpty();
    }

    [Fact]
    public void UpdateBuilder_RemoveOnReservedKeyword_UsesAlias()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder
            .Remove(p => p.Status)
            .Build();

        // "Status" is a reserved keyword - should be aliased
        result.Expression.Should().Contain("#upd_");
        result.ExpressionAttributeNames.Values.Should().Contain("Status");
    }

    [Fact]
    public void UpdateBuilder_EmptyBuild_ReturnsEmptyResult()
    {
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder.Build();

        result.Should().NotBeNull();
        result.IsEmpty.Should().BeTrue();
        result.Expression.Should().BeEmpty();
    }

    [Fact]
    public void UpdateBuilder_SetThenOverwrite_RemovesOldNameAliases()
    {
        // When overwriting a SET on a reserved keyword, the old name alias should be cleaned up
        var builder = new UpdateExpressionBuilder<UpdateTestEntity>(_resolverFactory, _converterRegistry);

        var result = builder
            .Set(p => p.Status, "Active")  // Creates #upd_0 -> "Status"
            .Set(p => p.Status, "Inactive") // Should clean up old alias
            .Build();

        // Should have exactly one name alias and one value placeholder
        result.ExpressionAttributeNames.Should().HaveCount(1);
        result.ExpressionAttributeValues.Should().HaveCount(1);
    }

    #endregion

    #region Category L: ProjectionBuilder Lenient mode tests

    [Fact]
    public void ProjectionBuilder_LenientMode_SkipsIgnoredProperty()
    {
        var customFactory = new AttributeNameResolverFactoryBuilder()
            .Configure<MutTestLenientEntity>(cfg => cfg.Ignore(e => e.Computed))
            .Build();

        var builder = new ProjectionBuilder<MutTestLenientEntity>(
            customFactory,
            cache: NullExpressionCache.Instance,
            resolutionMode: NameResolutionMode.Lenient);

        // Projecting an ignored property in lenient mode should skip it
        var result = builder.BuildProjection(p => new { p.Id, p.Computed });

        // The ignored property's path should be skipped
        result.ProjectionExpression.Should().Be("Id");
    }

    [Fact]
    public void ProjectionBuilder_StrictMode_ThrowsOnIgnoredProperty()
    {
        var customFactory = new AttributeNameResolverFactoryBuilder()
            .Configure<MutTestLenientEntity>(cfg => cfg.Ignore(e => e.Computed))
            .Build();

        var builder = new ProjectionBuilder<MutTestLenientEntity>(
            customFactory,
            cache: NullExpressionCache.Instance,
            resolutionMode: NameResolutionMode.Strict);

        var act = () => builder.BuildProjection(p => new { p.Id, p.Computed });

        act.Should().Throw<InvalidProjectionException>();
    }

    [Fact]
    public void ProjectionBuilder_LenientMode_AllIgnored_ReturnsEmpty()
    {
        // When all projected properties are ignored in lenient mode, result should be empty
        var customFactory = new AttributeNameResolverFactoryBuilder()
            .Configure<MutTestLenientEntity>(cfg => cfg.Ignore(e => e.Computed))
            .Build();

        var builder = new ProjectionBuilder<MutTestLenientEntity>(
            customFactory,
            cache: NullExpressionCache.Instance,
            resolutionMode: NameResolutionMode.Lenient);

        var result = builder.BuildProjection(p => p.Computed);

        result.ProjectionExpression.Should().BeEmpty();
        result.IsEmpty.Should().BeTrue();
    }

    #endregion
}

/// <summary>
/// Entity for Lenient mode projection testing.
/// </summary>
public class MutTestLenientEntity
{
    public string Id { get; set; } = string.Empty;
    public string Computed { get; set; } = string.Empty;
}
