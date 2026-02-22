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
/// Round 2 mutation-killing tests targeting surviving and NoCoverage mutants
/// from the 3b.10 Stryker re-run.
/// </summary>
public class MutationKillingRound2Tests
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;

    public MutationKillingRound2Tests()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
    }

    #region FilterExpressionVisitor -- VisitUnary Convert/ConvertChecked (NoCoverage L109-115)

    [Fact]
    public void Filter_ConvertExpression_HandledTransparently()
    {
        var builder = new FilterExpressionBuilder<MutR2EntityWithEnum>(_resolverFactory, _converterRegistry);
        var result = builder.BuildFilter(p => (int)p.Status == 1);
        result.Expression.Should().Contain("=");
    }

    [Fact]
    public void Filter_NullableComparison_HitsConvertPath()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var targetDate = new DateTime(2024, 1, 1);
        var result = builder.BuildFilter(p => p.EndDate == targetDate);
        result.Expression.Should().Contain("=");
    }

    #endregion

    #region FilterExpressionVisitor -- VisitBinary unsupported (NoCoverage L82)

    [Fact]
    public void Filter_UnsupportedBinaryExpression_Throws()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.BuildFilter(p => p.Score + 1 > 5);
        act.Should().Throw<UnsupportedExpressionException>();
    }

    #endregion

    #region FilterExpressionVisitor -- VisitMember non-bool (NoCoverage L132)

    [Fact]
    public void Filter_StandaloneMemberAccess_NonBool_Throws()
    {
        var visitor = new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            new StringBuilder(),
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());

        var param = Expression.Parameter(typeof(TestEntity), "p");
        var member = Expression.MakeMemberAccess(param, typeof(TestEntity).GetProperty("Title")!);
        var act = () => visitor.Visit(member);
        act.Should().Throw<UnsupportedExpressionException>();
    }

    #endregion

    #region FilterExpressionVisitor -- VisitUnary unsupported (NoCoverage L115)

    [Fact]
    public void Filter_UnsupportedUnaryExpression_Throws()
    {
        var visitor = new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            new StringBuilder(),
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());

        var param = Expression.Parameter(typeof(TestEntity), "p");
        var score = Expression.MakeMemberAccess(param, typeof(TestEntity).GetProperty("Score")!);
        var negate = Expression.Negate(score);
        var act = () => visitor.Visit(negate);
        act.Should().Throw<UnsupportedExpressionException>();
    }

    #endregion

    #region FilterExpressionVisitor -- Size function (NoCoverage L250)

    [Fact]
    public void Filter_SizeFunction_InComparison_ProducesCorrectExpression()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.BuildFilter(p => DynamoDbFunctions.Size(p.Tags) > 0);
        result.Expression.Should().Contain("size(");
        result.Expression.Should().Contain(">");
    }

    #endregion

    #region FilterExpressionVisitor -- Unsupported MethodCall (NoCoverage L268)

    [Fact]
    public void Filter_UnsupportedMethodCall_Throws()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.BuildFilter(p => p.Title.ToUpper() == "TEST");
        act.Should().Throw<UnsupportedExpressionException>();
    }

    #endregion

    #region FilterExpressionVisitor -- BuildPropertyPath error paths (NoCoverage L398, L405)

    [Fact]
    public void Filter_NonPropertyMemberAccess_Throws()
    {
        var visitor = new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            new StringBuilder(),
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());

        var param = Expression.Parameter(typeof(MutR2EntityWithField), "p");
        var fieldAccess = Expression.Field(param, typeof(MutR2EntityWithField).GetField("PublicField")!);
        var comparison = Expression.Equal(fieldAccess, Expression.Constant("test"));
        var act = () => visitor.Visit(comparison);
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void Filter_NonParameterRoot_Throws()
    {
        var visitor = new FilterExpressionVisitor(
            _resolverFactory,
            new ExpressionValueEmitter(_converterRegistry),
            new AliasGenerator("filt"),
            new StringBuilder(),
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());

        var entity = new TestEntity { Title = "test" };
        var constant = Expression.Constant(entity);
        var member = Expression.Property(constant, "Title");
        var comparison = Expression.Equal(member, Expression.Constant("abc"));
        var act = () => visitor.Visit(comparison);
        act.Should().Throw<UnsupportedExpressionException>();
    }

    #endregion

    #region FilterExpressionVisitor -- Logical mutations in Contains dispatch (Survived L140, L156, L173, L190)

    [Fact]
    public void Filter_StringStartsWith_ProducesBeginsWith()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.BuildFilter(p => p.Title.StartsWith("test"));
        result.Expression.Should().Contain("begins_with(");
        result.ExpressionAttributeValues.Should().HaveCount(1);
    }

    [Fact]
    public void Filter_StringContains_ProducesContainsFunction()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.BuildFilter(p => p.Title.Contains("test"));
        result.Expression.Should().Contain("contains(");
        result.ExpressionAttributeValues.Should().HaveCount(1);
    }

    [Fact]
    public void Filter_EnumerableContains_StaticMethod_ProducesInOperator()
    {
        var statuses = new[] { "Active", "Pending" };
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.BuildFilter(p => statuses.Contains(p.Status));
        result.Expression.Should().Contain("IN (");
        result.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void Filter_InstanceContains_ProducesInOperator()
    {
        var statuses = new List<string> { "Active", "Pending" };
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.BuildFilter(p => statuses.Contains(p.Status));
        result.Expression.Should().Contain("IN (");
        result.ExpressionAttributeValues.Should().HaveCount(2);
    }

    #endregion

    #region FilterExpressionVisitor -- Closure capture (Survived L464-481)

    [Fact]
    public void Filter_CapturedVariable_ValueExtracted()
    {
        var threshold = 100m;
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.BuildFilter(p => p.Price > threshold);
        result.Expression.Should().Contain(">");
        result.ExpressionAttributeValues.Should().ContainSingle()
            .Which.Value.N.Should().Be("100");
    }

    [Fact]
    public void Filter_CapturedProperty_ValueExtracted()
    {
        var holder = new ValueHolder { Value = "test" };
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.BuildFilter(p => p.Title == holder.Value);
        result.Expression.Should().Contain("=");
        result.ExpressionAttributeValues.Should().ContainSingle()
            .Which.Value.S.Should().Be("test");
    }

    [Fact]
    public void Filter_DirectConstant_ValueExtracted()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.BuildFilter(p => p.Price > 50m);
        result.Expression.Should().Contain(">");
        result.ExpressionAttributeValues.Should().ContainSingle()
            .Which.Value.N.Should().Be("50");
    }

    #endregion

    #region UpdateExpressionBuilder -- CheckForConflicts removal (Survived L156, L189, L222, L255)

    [Fact]
    public void Update_Increment_ThenRemove_SameProperty_ThrowsConflict()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Increment(p => p.Score, 1).Remove(p => p.Score);
        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void Update_Decrement_ThenRemove_SameProperty_ThrowsConflict()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Decrement(p => p.Score, 1).Remove(p => p.Score);
        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void Update_SetIfNotExists_ThenRemove_SameProperty_ThrowsConflict()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.SetIfNotExists(p => p.Title, "default").Remove(p => p.Title);
        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void Update_AppendToList_ThenRemove_SameProperty_ThrowsConflict()
    {
        var builder = new UpdateExpressionBuilder<MutR2EntityWithList>(_resolverFactory, _converterRegistry);
        var act = () => builder.AppendToList(p => p.Items, new List<string> { "a" }).Remove(p => p.Items);
        act.Should().Throw<InvalidUpdateException>();
    }

    #endregion

    #region UpdateExpressionBuilder -- Build empty returns Empty (Survived L349-351)

    [Fact]
    public void Update_Build_NoOperations_ReturnsEmptyResult()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.Build();
        result.IsEmpty.Should().BeTrue();
        result.Expression.Should().BeEmpty();
    }

    [Fact]
    public void Update_Build_NoOperations_ReturnsSameAsEmpty()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder.Build();
        result.Should().Be(UpdateExpressionResult.Empty);
    }

    #endregion

    #region ProjectionExpressionVisitor -- Identity shape (Survived L55-59)

    [Fact]
    public void Projection_IdentitySelector_SetsIdentityShape()
    {
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths<TestEntity, TestEntity>(
            p => p, out var shape);
        shape.Should().Be(ProjectionShape.Identity);
        paths.Should().BeEmpty();
    }

    #endregion

    #region ProjectionBuilder -- null coalescing (Survived L32)

    [Fact]
    public void ProjectionBuilder_NullReservedKeywords_UsesDefault()
    {
        var builder = new ProjectionBuilder<TestEntity>(_resolverFactory, reservedKeywords: null);
        var result = builder.BuildProjection(p => p.Name);
        result.ExpressionAttributeNames.Should().NotBeEmpty();
    }

    [Fact]
    public void ProjectionBuilder_ExplicitReservedKeywords_Works()
    {
        var keywords = new ReservedKeywordRegistry();
        var builder = new ProjectionBuilder<TestEntity>(_resolverFactory, reservedKeywords: keywords);
        var result = builder.BuildProjection(p => p.Name);
        result.ExpressionAttributeNames.Should().NotBeEmpty();
    }

    #endregion

    #region FilterExpressionResult -- And/Or composition (NoCoverage L197-222)

    [Fact]
    public void FilterExpressionResult_And_CombinesTwoFilters()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result1 = builder.BuildFilter(p => p.Name == "test1");
        var result2 = builder.BuildFilter(p => p.Name == "test2");
        var combined = FilterExpressionResult.And(result1, result2);
        combined.Expression.Should().Contain("AND");
    }

    [Fact]
    public void FilterExpressionResult_And_MergesValuePlaceholders()
    {
        var builder = new FilterExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result1 = builder.BuildFilter(p => p.Price > 10);
        var result2 = builder.BuildFilter(p => p.Price < 100);
        var combined = FilterExpressionResult.And(result1, result2);
        combined.Expression.Should().Contain("AND");
        combined.ExpressionAttributeValues.Should().HaveCount(2);
    }

    #endregion

    #region UpdateExpressionBuilder -- NoCoverage error paths (L434, L441, L446)

    [Fact]
    public void Update_NonPropertyMemberInExpression_Throws()
    {
        var builder = new UpdateExpressionBuilder<MutR2EntityWithField>(_resolverFactory, _converterRegistry);
        var param = Expression.Parameter(typeof(MutR2EntityWithField), "p");
        var fieldAccess = Expression.Field(param, typeof(MutR2EntityWithField).GetField("PublicField")!);
        var lambda = Expression.Lambda<Func<MutR2EntityWithField, string>>(fieldAccess, param);
        var act = () => builder.Set(lambda, "value");
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void Update_NonParameterRootExpression_Throws()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var entity = new TestEntity();
        var constant = Expression.Constant(entity);
        var member = Expression.Property(constant, "Title");
        var param = Expression.Parameter(typeof(TestEntity), "p");
        var lambda = Expression.Lambda<Func<TestEntity, string>>(member, param);
        var act = () => builder.Set(lambda, "value");
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void Update_EmptyMemberChain_Throws()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var param = Expression.Parameter(typeof(TestEntity), "p");
        var convert = Expression.Convert(param, typeof(object));
        var lambda = Expression.Lambda<Func<TestEntity, object>>(convert, param);
        var act = () => builder.Set(lambda, new object());
        act.Should().Throw<UnsupportedExpressionException>();
    }

    #endregion

    #region KeyConditionExpressionBuilder -- NoCoverage error paths (L139, L146, L151)

    [Fact]
    public void KeyCondition_NonPropertyMember_Throws()
    {
        var builder = new KeyConditionExpressionBuilder<MutR2EntityWithField>(_resolverFactory, _converterRegistry);
        var param = Expression.Parameter(typeof(MutR2EntityWithField), "p");
        var fieldAccess = Expression.Field(param, typeof(MutR2EntityWithField).GetField("PublicField")!);
        var lambda = Expression.Lambda<Func<MutR2EntityWithField, string>>(fieldAccess, param);
        var act = () => builder.WithPartitionKey(lambda, "value");
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void KeyCondition_NonParameterRoot_Throws()
    {
        var builder = new KeyConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var entity = new TestEntity();
        var constant = Expression.Constant(entity);
        var member = Expression.Property(constant, "OrderId");
        var param = Expression.Parameter(typeof(TestEntity), "p");
        var lambda = Expression.Lambda<Func<TestEntity, string>>(member, param);
        var act = () => builder.WithPartitionKey(lambda, "value");
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void KeyCondition_EmptyMemberChain_Throws()
    {
        var builder = new KeyConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var param = Expression.Parameter(typeof(TestEntity), "p");
        var convert = Expression.Convert(param, typeof(object));
        var lambda = Expression.Lambda<Func<TestEntity, object>>(convert, param);
        var act = () => builder.WithPartitionKey(lambda, new object());
        act.Should().Throw<UnsupportedExpressionException>();
    }

    #endregion

    #region SortKeyConditionBuilder -- NoCoverage error paths (L203, L210, L215)

    [Fact]
    public void SortKeyCondition_NonPropertyMember_Throws()
    {
        var keyBuilder = new KeyConditionExpressionBuilder<MutR2EntityWithField>(_resolverFactory, _converterRegistry);
        var withPk = keyBuilder.WithPartitionKey(p => p.PartitionKey, "pk");
        var param = Expression.Parameter(typeof(MutR2EntityWithField), "p");
        var fieldAccess = Expression.Field(param, typeof(MutR2EntityWithField).GetField("PublicField")!);
        var lambda = Expression.Lambda<Func<MutR2EntityWithField, string>>(fieldAccess, param);
        var act = () => withPk.WithSortKeyEquals(lambda, "value");
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void SortKeyCondition_NonParameterRoot_Throws()
    {
        var keyBuilder = new KeyConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var withPk = keyBuilder.WithPartitionKey(p => p.OrderId, "pk");
        var entity = new TestEntity();
        var constant = Expression.Constant(entity);
        var member = Expression.Property(constant, "CustomerId");
        var param = Expression.Parameter(typeof(TestEntity), "p");
        var lambda = Expression.Lambda<Func<TestEntity, string>>(member, param);
        var act = () => withPk.WithSortKeyEquals(lambda, "value");
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void SortKeyCondition_EmptyMemberChain_Throws()
    {
        var keyBuilder = new KeyConditionExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var withPk = keyBuilder.WithPartitionKey(p => p.OrderId, "pk");
        var param = Expression.Parameter(typeof(TestEntity), "p");
        var convert = Expression.Convert(param, typeof(object));
        var lambda = Expression.Lambda<Func<TestEntity, object>>(convert, param);
        var act = () => withPk.WithSortKeyEquals(lambda, new object());
        act.Should().Throw<UnsupportedExpressionException>();
    }

    #endregion

    #region UpdateExpressionBuilder -- conflict/orphan paths

    [Fact]
    public void Update_CheckForConflicts_SetAndRemove_ThrowsConflict()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Set(p => p.Title, "newTitle").Remove(p => p.Title);
        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void Update_CheckForConflicts_RemoveAndSet_ThrowsConflict()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var act = () => builder.Remove(p => p.Title).Set(p => p.Title, "newTitle");
        act.Should().Throw<InvalidUpdateException>();
    }

    [Fact]
    public void Update_SetSamePropertyTwice_OrphanedPlaceholdersRemoved()
    {
        var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
        var result = builder
            .Set(p => p.Title, "first")
            .Set(p => p.Title, "second")
            .Build();
        result.ExpressionAttributeValues.Should().HaveCount(1);
        result.ExpressionAttributeValues.Values.Should().ContainSingle()
            .Which.S.Should().Be("second");
    }

    #endregion
}

#region Test entities

public class MutR2EntityWithEnum
{
    public string Id { get; set; } = string.Empty;
    public MutR2Status Status { get; set; }
}

public enum MutR2Status
{
    Active = 0,
    Inactive = 1
}

public class MutR2EntityWithField
{
    public string PublicField = string.Empty;
    public string PartitionKey { get; set; } = string.Empty;
    public string SortKey { get; set; } = string.Empty;
}

public class MutR2EntityWithList
{
    public string Id { get; set; } = string.Empty;
    public List<string> Items { get; set; } = new();
}

public class ValueHolder
{
    public string Value { get; set; } = string.Empty;
}

#endregion
