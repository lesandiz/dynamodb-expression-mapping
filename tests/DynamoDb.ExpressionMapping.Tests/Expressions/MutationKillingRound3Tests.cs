using System.Linq.Expressions;
using System.Text;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

/// <summary>
/// Round 3 mutation-killing tests targeting the remaining NoCoverage and Survived
/// mutants in the Expressions subsystem to push it past the 90% threshold.
/// </summary>
public class MutationKillingRound3Tests
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;

    public MutationKillingRound3Tests()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
    }

    #region FilterExpressionResult.Or -- NoCoverage for Or composition

    [Fact]
    public void FilterExpressionResult_Or_CombinesTwoFilters()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result1 = builder.BuildFilter(p => p.Name == "test1");
        var result2 = builder.BuildFilter(p => p.Name == "test2");
        var combined = FilterExpressionResult.Or(result1, result2);
        combined.Expression.Should().Contain("OR");
    }

    [Fact]
    public void FilterExpressionResult_Or_MergesValuePlaceholders()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result1 = builder.BuildFilter(p => p.Price > 10);
        var result2 = builder.BuildFilter(p => p.Price < 100);
        var combined = FilterExpressionResult.Or(result1, result2);
        combined.Expression.Should().Contain("OR");
        combined.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void FilterExpressionResult_Or_ReAliasesRightSide()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        // Use "Name" and "Status" which are DynamoDB reserved keywords, ensuring name aliases are produced
        var result1 = builder.BuildFilter(p => p.Name == "a");
        var result2 = builder.BuildFilter(p => p.Status == "b");
        var combined = FilterExpressionResult.Or(result1, result2);
        // Right side should be re-aliased, so values should have different keys
        combined.ExpressionAttributeValues.Should().HaveCount(2);
        // Both left and right name aliases should be present after composition
        combined.ExpressionAttributeNames.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    #endregion

    #region FilterExpressionResult And/Or -- empty operand short-circuits

    [Fact]
    public void FilterExpressionResult_And_EmptyLeft_ReturnsRight()
    {
        var empty = new FilterExpressionResult(
            string.Empty,
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var right = builder.BuildFilter(p => p.Price > 10);

        var result = FilterExpressionResult.And(empty, right);
        result.Should().BeSameAs(right);
    }

    [Fact]
    public void FilterExpressionResult_And_EmptyRight_ReturnsLeft()
    {
        var empty = new FilterExpressionResult(
            string.Empty,
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var left = builder.BuildFilter(p => p.Price > 10);

        var result = FilterExpressionResult.And(left, empty);
        result.Should().BeSameAs(left);
    }

    [Fact]
    public void FilterExpressionResult_Or_EmptyLeft_ReturnsRight()
    {
        var empty = new FilterExpressionResult(
            string.Empty,
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var right = builder.BuildFilter(p => p.Price > 10);

        var result = FilterExpressionResult.Or(empty, right);
        result.Should().BeSameAs(right);
    }

    [Fact]
    public void FilterExpressionResult_Or_EmptyRight_ReturnsLeft()
    {
        var empty = new FilterExpressionResult(
            string.Empty,
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var left = builder.BuildFilter(p => p.Price > 10);

        var result = FilterExpressionResult.Or(left, empty);
        result.Should().BeSameAs(left);
    }

    [Fact]
    public void FilterExpressionResult_And_NullLeft_Throws()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var right = builder.BuildFilter(p => p.Price > 10);

        var act = () => FilterExpressionResult.And(null!, right);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FilterExpressionResult_Or_NullLeft_Throws()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var right = builder.BuildFilter(p => p.Price > 10);

        var act = () => FilterExpressionResult.Or(null!, right);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ConditionExpressionResult -- And/Or composition (NoCoverage)

    [Fact]
    public void ConditionExpressionResult_And_CombinesTwoConditions()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result1 = builder.BuildCondition(p => p.Name == "test1");
        var result2 = builder.BuildCondition(p => p.Name == "test2");
        var combined = ConditionExpressionResult.And(result1, result2);
        combined.Expression.Should().Contain("AND");
    }

    [Fact]
    public void ConditionExpressionResult_And_MergesValues()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result1 = builder.BuildCondition(p => p.Price > 10);
        var result2 = builder.BuildCondition(p => p.Price < 100);
        var combined = ConditionExpressionResult.And(result1, result2);
        combined.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void ConditionExpressionResult_Or_CombinesTwoConditions()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result1 = builder.BuildCondition(p => p.Name == "test1");
        var result2 = builder.BuildCondition(p => p.Name == "test2");
        var combined = ConditionExpressionResult.Or(result1, result2);
        combined.Expression.Should().Contain("OR");
    }

    [Fact]
    public void ConditionExpressionResult_Or_MergesValues()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result1 = builder.BuildCondition(p => p.Price > 10);
        var result2 = builder.BuildCondition(p => p.Price < 100);
        var combined = ConditionExpressionResult.Or(result1, result2);
        combined.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void ConditionExpressionResult_And_EmptyLeft_ReturnsRight()
    {
        var empty = new ConditionExpressionResult(
            string.Empty,
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var right = builder.BuildCondition(p => p.Price > 10);

        var result = ConditionExpressionResult.And(empty, right);
        result.Should().BeSameAs(right);
    }

    [Fact]
    public void ConditionExpressionResult_And_EmptyRight_ReturnsLeft()
    {
        var empty = new ConditionExpressionResult(
            string.Empty,
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var left = builder.BuildCondition(p => p.Price > 10);

        var result = ConditionExpressionResult.And(left, empty);
        result.Should().BeSameAs(left);
    }

    [Fact]
    public void ConditionExpressionResult_Or_EmptyLeft_ReturnsRight()
    {
        var empty = new ConditionExpressionResult(
            string.Empty,
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var right = builder.BuildCondition(p => p.Price > 10);

        var result = ConditionExpressionResult.Or(empty, right);
        result.Should().BeSameAs(right);
    }

    [Fact]
    public void ConditionExpressionResult_Or_EmptyRight_ReturnsLeft()
    {
        var empty = new ConditionExpressionResult(
            string.Empty,
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var left = builder.BuildCondition(p => p.Price > 10);

        var result = ConditionExpressionResult.Or(left, empty);
        result.Should().BeSameAs(left);
    }

    #endregion

    #region UpdateExpressionBuilder -- RemoveOldValuePlaceholders for Increment/Decrement/SetIfNotExists (NoCoverage L177/210/243)

    [Fact]
    public void Update_DecrementSamePropertyTwice_RemovesOldPlaceholders()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder
            .Decrement(p => p.Score, 2)
            .Decrement(p => p.Score, 7)
            .Build();

        result.ExpressionAttributeValues.Should().HaveCount(1);
        result.ExpressionAttributeValues.Values.Should().ContainSingle()
            .Which.N.Should().Be("7");
    }

    [Fact]
    public void Update_SetIfNotExistsSamePropertyTwice_RemovesOldPlaceholders()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder
            .SetIfNotExists(p => p.Title, "first")
            .SetIfNotExists(p => p.Title, "second")
            .Build();

        result.ExpressionAttributeValues.Should().HaveCount(1);
        result.ExpressionAttributeValues.Values.Should().ContainSingle()
            .Which.S.Should().Be("second");
    }

    [Fact]
    public void Update_AppendToListSamePropertyTwice_RemovesOldPlaceholders()
    {
        var builder = new UpdateExpressionBuilder<MutR2EntityWithList>(_resolverFactory, _converterRegistry);
        var result = builder
            .AppendToList(p => p.Items, new List<string> { "a" })
            .AppendToList(p => p.Items, new List<string> { "b" })
            .Build();

        result.ExpressionAttributeValues.Should().HaveCount(1);
    }

    [Fact]
    public void Update_Set_ThenIncrement_SameProperty_RemovesOldPlaceholders()
    {
        // Set creates a setOperation, then Increment overwrites it -- triggers RemoveOldValuePlaceholders
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder
            .Set(p => p.Score, 10)
            .Increment(p => p.Score, 5)
            .Build();

        result.ExpressionAttributeValues.Should().HaveCount(1);
        result.ExpressionAttributeValues.Values.Should().ContainSingle()
            .Which.N.Should().Be("5");
    }

    [Fact]
    public void Update_Set_ThenDecrement_SameProperty_RemovesOldPlaceholders()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder
            .Set(p => p.Score, 10)
            .Decrement(p => p.Score, 3)
            .Build();

        result.ExpressionAttributeValues.Should().HaveCount(1);
        result.ExpressionAttributeValues.Values.Should().ContainSingle()
            .Which.N.Should().Be("3");
    }

    [Fact]
    public void Update_Set_ThenSetIfNotExists_SameProperty_RemovesOldPlaceholders()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder
            .Set(p => p.Title, "initial")
            .SetIfNotExists(p => p.Title, "fallback")
            .Build();

        result.ExpressionAttributeValues.Should().HaveCount(1);
        result.ExpressionAttributeValues.Values.Should().ContainSingle()
            .Which.S.Should().Be("fallback");
    }

    #endregion

    #region FilterExpressionVisitor -- NOT sub-expression path (Survived/NoCoverage L102-106)

    [Fact]
    public void Filter_NotSubExpression_ProducesNotKeyword()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        // NOT (compound expression) — exercises the "NOT (" path in VisitUnary
        var result = builder.BuildFilter(p => !(p.Price > 10 && p.Price < 100));
        result.Expression.Should().Contain("NOT");
    }

    #endregion

    #region ConditionExpressionBuilder -- basic coverage to exercise filter visitor paths

    [Fact]
    public void ConditionBuilder_NullPredicate_Throws()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.BuildCondition(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConditionBuilder_NullResolverFactory_Throws()
    {
        var act = () => new ConditionExpressionBuilder<TestEntity>(null!, _converterRegistry);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConditionBuilder_NullConverterRegistry_Throws()
    {
        var act = () => new ConditionExpressionBuilder<TestEntity>(_resolverFactory, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConditionBuilder_BoolProperty_ProducesEqualsTrue()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.BuildCondition(p => p.IsActive);
        result.Expression.Should().Contain("=");
        result.ExpressionAttributeValues.Should().ContainSingle()
            .Which.Value.BOOL.Should().BeTrue();
    }

    [Fact]
    public void ConditionBuilder_NegatedBool_ProducesEqualsFalse()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.BuildCondition(p => !p.IsActive);
        result.ExpressionAttributeValues.Should().ContainSingle()
            .Which.Value.BOOL.Should().BeFalse();
    }

    [Fact]
    public void ConditionBuilder_AndExpression_ProducesAnd()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.BuildCondition(p => p.IsActive && p.Price > 10);
        result.Expression.Should().Contain("AND");
    }

    [Fact]
    public void ConditionBuilder_OrExpression_ProducesOr()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.BuildCondition(p => p.IsActive || p.Price > 10);
        result.Expression.Should().Contain("OR");
    }

    #endregion

    #region FilterExpressionResult/ConditionExpressionResult -- MergeAttributeNames conflict (NoCoverage L197)

    [Fact]
    public void FilterExpressionResult_And_ConflictingNames_ResolvedByReAliasing()
    {
        // Create two results that after re-aliasing would produce conflicting names
        // We manually construct results with same key but different values to trigger MergeAttributeNames conflict
        var left = new FilterExpressionResult(
            "#filt_0 = :filt_v0",
            new Dictionary<string, string> { ["#filt_0"] = "Attr1" },
            new Dictionary<string, AttributeValue> { [":filt_v0"] = new() { S = "val1" } });

        // Create right with no aliases to re-alias (empty prefix matching)
        // This is hard to trigger via normal API because ReAlias always offsets.
        // But we can test MergeAttributeValues conflict by creating overlapping results manually.
        // The MergeAttributeNames path at L197 requires same key with different value.
        // Since ReAlias offsets, this path is guarded. It's effectively an equivalent mutant.
        // Instead, test the normal And path thoroughly.
        var right = new FilterExpressionResult(
            "#filt_0 = :filt_v0",
            new Dictionary<string, string> { ["#filt_0"] = "Attr2" },
            new Dictionary<string, AttributeValue> { [":filt_v0"] = new() { S = "val2" } });

        // Since right will be re-aliased to #filt_1/:filt_v1, no conflict
        var combined = FilterExpressionResult.And(left, right);
        combined.ExpressionAttributeNames.Should().HaveCount(2);
        combined.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void ConditionExpressionResult_And_ManuallyConflictingNames_NoConflictAfterReAlias()
    {
        var left = new ConditionExpressionResult(
            "#cond_0 = :cond_v0",
            new Dictionary<string, string> { ["#cond_0"] = "Attr1" },
            new Dictionary<string, AttributeValue> { [":cond_v0"] = new() { S = "val1" } });

        var right = new ConditionExpressionResult(
            "#cond_0 = :cond_v0",
            new Dictionary<string, string> { ["#cond_0"] = "Attr2" },
            new Dictionary<string, AttributeValue> { [":cond_v0"] = new() { S = "val2" } });

        var combined = ConditionExpressionResult.And(left, right);
        combined.ExpressionAttributeNames.Should().HaveCount(2);
        combined.ExpressionAttributeValues.Should().HaveCount(2);
    }

    #endregion

    #region UpdateExpressionBuilder -- RemoveOldValuePlaceholders empty expression path (L540-541)

    [Fact]
    public void Update_SetSamePropertyMultipleTimes_KeepsOnlyLatestPlaceholders()
    {
        // This exercises the early return in RemoveOldValuePlaceholders when expression is empty.
        // We can trigger this indirectly by having a remove operation (no expression) followed by a set.
        // Remove doesn't create a setOperation with an expression, so when a set on the same prop
        // tries to clean up, there shouldn't be an existing setOperation.
        // Instead, test that overwriting works correctly even with multiple operations.
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder
            .Set(p => p.Title, "a")
            .Set(p => p.Title, "b")
            .Set(p => p.Title, "c")
            .Build();

        result.ExpressionAttributeValues.Should().HaveCount(1);
        result.ExpressionAttributeValues.Values.Should().ContainSingle()
            .Which.S.Should().Be("c");
    }

    #endregion

    #region FilterExpressionResult/ConditionExpressionResult -- Or with name aliases (NoCoverage for Or ReAlias path)

    [Fact]
    public void FilterExpressionResult_Or_WithNameAliases_ReAliasesCorrectly()
    {
        // Use reserved keywords to force name aliases
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result1 = builder.BuildFilter(p => p.Name == "test1");
        var result2 = builder.BuildFilter(p => p.Status == "Active");
        var combined = FilterExpressionResult.Or(result1, result2);

        combined.Expression.Should().Contain("OR");
        combined.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void ConditionExpressionResult_Or_WithNameAliases_ReAliasesCorrectly()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result1 = builder.BuildCondition(p => p.Name == "test1");
        var result2 = builder.BuildCondition(p => p.Status == "Active");
        var combined = ConditionExpressionResult.Or(result1, result2);

        combined.Expression.Should().Contain("OR");
        combined.ExpressionAttributeValues.Should().HaveCount(2);
    }

    #endregion

    #region ConditionExpressionResult -- null guard tests (NoCoverage)

    [Fact]
    public void ConditionExpressionResult_And_NullLeft_Throws()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var right = builder.BuildCondition(p => p.Price > 10);

        var act = () => ConditionExpressionResult.And(null!, right);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConditionExpressionResult_And_NullRight_Throws()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var left = builder.BuildCondition(p => p.Price > 10);

        var act = () => ConditionExpressionResult.And(left, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConditionExpressionResult_Or_NullLeft_Throws()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var right = builder.BuildCondition(p => p.Price > 10);

        var act = () => ConditionExpressionResult.Or(null!, right);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConditionExpressionResult_Or_NullRight_Throws()
    {
        var builder = new ConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var left = builder.BuildCondition(p => p.Price > 10);

        var act = () => ConditionExpressionResult.Or(left, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region FilterExpressionResult -- And/Or with null guards on right

    [Fact]
    public void FilterExpressionResult_And_NullRight_Throws()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var left = builder.BuildFilter(p => p.Price > 10);

        var act = () => FilterExpressionResult.And(left, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FilterExpressionResult_Or_NullRight_Throws()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var left = builder.BuildFilter(p => p.Price > 10);

        var act = () => FilterExpressionResult.Or(left, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UpdateExpressionBuilder -- Reverse direction conflict checks (kills L156/189/222/255 CheckForConflicts removal)

    [Fact]
    public void Update_Remove_ThenIncrement_SameProperty_ThrowsConflict()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Remove(p => p.Score).Increment(p => p.Score, 1);
        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void Update_Remove_ThenDecrement_SameProperty_ThrowsConflict()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Remove(p => p.Score).Decrement(p => p.Score, 1);
        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void Update_Remove_ThenSetIfNotExists_SameProperty_ThrowsConflict()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Remove(p => p.Title).SetIfNotExists(p => p.Title, "default");
        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void Update_Remove_ThenAppendToList_SameProperty_ThrowsConflict()
    {
        var builder = new UpdateExpressionBuilder<MutR2EntityWithList>(_resolverFactory, _converterRegistry);
        var act = () => builder.Remove(p => p.Items).AppendToList(p => p.Items, new List<string> { "a" });
        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void Update_Add_ThenIncrement_SameProperty_ThrowsConflict()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Add(p => p.Score, 10).Increment(p => p.Score, 1);
        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void Update_Delete_ThenDecrement_DifferentProperties_NoConflict()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Delete(p => p.EnabledFeatures, new HashSet<string> { "a" }).Decrement(p => p.Score, 1);
        // These are on different properties, should NOT conflict
        act.Should().NotThrow();
    }

    #endregion

    #region FilterExpressionVisitor -- VisitUnary Convert direct path (L109)

    [Fact]
    public void Filter_VisitUnary_ConvertNode_ViaAndDispatch_HandledTransparently()
    {
        // To exercise VisitUnary's Convert path (L109), we need the ExpressionVisitor
        // to dispatch a Convert node through Visit(). This happens when AND/OR calls
        // Visit(left)/Visit(right) and one side is a Convert node.
        //
        // Construct: AND(p.IsActive, Convert(p.Score > 5, bool))
        var sb = new StringBuilder();
        var names = new Dictionary<string, string>();
        var vals = new Dictionary<string, AttributeValue>();

        var visitor = new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            sb, names, vals);

        var param = Expression.Parameter(typeof(TestEntity), "p");
        var isActive = Expression.MakeMemberAccess(param, typeof(TestEntity).GetProperty("IsActive")!);
        var score = Expression.MakeMemberAccess(param, typeof(TestEntity).GetProperty("Score")!);
        var scoreGt5 = Expression.GreaterThan(score, Expression.Constant(5));
        // Wrap the comparison in Convert(bool -> bool) — forces a Convert unary node
        var converted = Expression.Convert(scoreGt5, typeof(bool));
        // AND(p.IsActive, Convert(p.Score > 5))
        var andExpr = Expression.AndAlso(isActive, converted);

        visitor.Visit(andExpr);

        sb.ToString().Should().Contain("AND");
    }

    [Fact]
    public void Filter_VisitUnary_ConvertCheckedNode_ViaAndDispatch_HandledTransparently()
    {
        // Same as above but with ConvertChecked to exercise the second branch of L109
        var sb = new StringBuilder();
        var names = new Dictionary<string, string>();
        var vals = new Dictionary<string, AttributeValue>();

        var visitor = new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            sb, names, vals);

        var param = Expression.Parameter(typeof(TestEntity), "p");
        var isActive = Expression.MakeMemberAccess(param, typeof(TestEntity).GetProperty("IsActive")!);
        var score = Expression.MakeMemberAccess(param, typeof(TestEntity).GetProperty("Score")!);
        var scoreGt5 = Expression.GreaterThan(score, Expression.Constant(5));
        // Wrap in ConvertChecked — forces a ConvertChecked unary node
        var convertChecked = Expression.ConvertChecked(scoreGt5, typeof(bool));
        // AND(p.IsActive, ConvertChecked(p.Score > 5))
        var andExpr = Expression.AndAlso(isActive, convertChecked);

        visitor.Visit(andExpr);

        sb.ToString().Should().Contain("AND");
    }

    #endregion

    #region UpdateExpressionBuilder -- Multiple operation types combined

    [Fact]
    public void Update_Set_And_Add_DifferentProperties_BuildsCombinedExpression()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder
            .Set(p => p.Title, "newTitle")
            .Add(p => p.Score, 5)
            .Build();

        result.Expression.Should().Contain("SET");
        result.Expression.Should().Contain("ADD");
        result.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void Update_Set_And_Delete_DifferentProperties_BuildsCombinedExpression()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder
            .Set(p => p.Title, "newTitle")
            .Delete(p => p.EnabledFeatures, new HashSet<string> { "a" })
            .Build();

        result.Expression.Should().Contain("SET");
        result.Expression.Should().Contain("DELETE");
    }

    [Fact]
    public void Update_Remove_And_Add_DifferentProperties_BuildsCombinedExpression()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder
            .Remove(p => p.Title)
            .Add(p => p.Score, 5)
            .Build();

        result.Expression.Should().Contain("REMOVE");
        result.Expression.Should().Contain("ADD");
    }

    #endregion
}
