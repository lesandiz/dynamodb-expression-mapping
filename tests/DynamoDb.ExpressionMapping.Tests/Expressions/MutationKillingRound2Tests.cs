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

    #region FilterExpressionVisitor -- Closure capture (Survived L464-481)

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
