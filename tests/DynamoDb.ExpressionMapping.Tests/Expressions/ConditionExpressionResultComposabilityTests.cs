using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

/// <summary>
/// Unit tests for ConditionExpressionResult composition (And/Or) with re-aliasing.
/// Tests the re-aliasing logic from Spec 06 §6 using #cond_ prefix.
/// </summary>
public class ConditionExpressionResultComposabilityTests
    : ExpressionResultComposabilityTestBase<ConditionExpressionResult>
{
    protected override string NamePrefix => "#cond_";
    protected override string ValuePrefix => ":cond_v";

    protected override ConditionExpressionResult CreateResult(
        string expression,
        Dictionary<string, string> names,
        Dictionary<string, AttributeValue> values)
        => new(expression, names, values);

    protected override ConditionExpressionResult CreateEmptyResult()
        => new(string.Empty, new Dictionary<string, string>(), new Dictionary<string, AttributeValue>());

    protected override ConditionExpressionResult ComposeAnd(ConditionExpressionResult left, ConditionExpressionResult right)
        => ConditionExpressionResult.And(left, right);

    protected override ConditionExpressionResult ComposeOr(ConditionExpressionResult left, ConditionExpressionResult right)
        => ConditionExpressionResult.Or(left, right);

    protected override Action ComposeAndNullLeft(ConditionExpressionResult right)
        => () => ConditionExpressionResult.And(null!, right);

    protected override Action ComposeAndNullRight(ConditionExpressionResult left)
        => () => ConditionExpressionResult.And(left, null!);

    protected override Action ComposeOrNullLeft(ConditionExpressionResult right)
        => () => ConditionExpressionResult.Or(null!, right);

    protected override Action ComposeOrNullRight(ConditionExpressionResult left)
        => () => ConditionExpressionResult.Or(left, null!);

    protected override string GetExpression(ConditionExpressionResult result) => result.Expression;
    protected override IReadOnlyDictionary<string, string> GetNames(ConditionExpressionResult result) => result.ExpressionAttributeNames;
    protected override IReadOnlyDictionary<string, AttributeValue> GetValues(ConditionExpressionResult result) => result.ExpressionAttributeValues;
    protected override bool IsEmpty(ConditionExpressionResult result) => result.IsEmpty;

    #region Alias Scope Verification (unique to ConditionExpressionResult)

    [Fact]
    public void ComposedConditions_UseCondScopeNotFiltScope()
    {
        // Verify composed conditions use #cond_ and :cond_v, not #filt_ and :filt_v
        var left = CreateResult(
            expression: "#cond_0 = :cond_v0",
            names: new() { ["#cond_0"] = "Status" },
            values: new() { [":cond_v0"] = new() { S = "Active" } });

        var right = CreateResult(
            expression: "#cond_0 = :cond_v0",
            names: new() { ["#cond_0"] = "Enabled" },
            values: new() { [":cond_v0"] = new() { BOOL = true } });

        var result = ComposeAnd(left, right);

        GetExpression(result).Should().Contain("#cond_");
        GetExpression(result).Should().Contain(":cond_v");
        GetExpression(result).Should().NotContain("#filt_");
        GetExpression(result).Should().NotContain(":filt_v");
        GetNames(result).Keys.Should().AllSatisfy(k => k.Should().StartWith("#cond_"));
        GetValues(result).Keys.Should().AllSatisfy(k => k.Should().StartWith(":cond_v"));
    }

    #endregion
}
