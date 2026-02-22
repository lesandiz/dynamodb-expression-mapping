using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

/// <summary>
/// Unit tests for FilterExpressionResult composition (And/Or) with re-aliasing.
/// Tests the re-aliasing logic from Spec 06 §6.
/// </summary>
public class FilterExpressionResultComposabilityTests
    : ExpressionResultComposabilityTestBase<FilterExpressionResult>
{
    protected override string NamePrefix => "#filt_";
    protected override string ValuePrefix => ":filt_v";

    protected override FilterExpressionResult CreateResult(
        string expression,
        Dictionary<string, string> names,
        Dictionary<string, AttributeValue> values)
        => new(expression, names, values);

    protected override FilterExpressionResult CreateEmptyResult()
        => new(string.Empty, new Dictionary<string, string>(), new Dictionary<string, AttributeValue>());

    protected override FilterExpressionResult ComposeAnd(FilterExpressionResult left, FilterExpressionResult right)
        => FilterExpressionResult.And(left, right);

    protected override FilterExpressionResult ComposeOr(FilterExpressionResult left, FilterExpressionResult right)
        => FilterExpressionResult.Or(left, right);

    protected override Action ComposeAndNullLeft(FilterExpressionResult right)
        => () => FilterExpressionResult.And(null!, right);

    protected override Action ComposeAndNullRight(FilterExpressionResult left)
        => () => FilterExpressionResult.And(left, null!);

    protected override Action ComposeOrNullLeft(FilterExpressionResult right)
        => () => FilterExpressionResult.Or(null!, right);

    protected override Action ComposeOrNullRight(FilterExpressionResult left)
        => () => FilterExpressionResult.Or(left, null!);

    protected override string GetExpression(FilterExpressionResult result) => result.Expression;
    protected override IReadOnlyDictionary<string, string> GetNames(FilterExpressionResult result) => result.ExpressionAttributeNames;
    protected override IReadOnlyDictionary<string, AttributeValue> GetValues(FilterExpressionResult result) => result.ExpressionAttributeValues;
    protected override bool IsEmpty(FilterExpressionResult result) => result.IsEmpty;
}
