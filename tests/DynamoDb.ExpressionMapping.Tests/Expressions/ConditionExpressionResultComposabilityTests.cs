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
}
