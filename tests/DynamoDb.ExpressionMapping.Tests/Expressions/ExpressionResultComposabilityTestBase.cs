using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

/// <summary>
/// Abstract base class for testing expression result composition (And/Or) with re-aliasing.
/// Derived classes provide factory methods for their specific result type.
/// </summary>
public abstract class ExpressionResultComposabilityTestBase<TResult> where TResult : class
{
    /// <summary>The name alias prefix (e.g., "#filt_" or "#cond_").</summary>
    protected abstract string NamePrefix { get; }

    /// <summary>The value alias prefix (e.g., ":filt_v" or ":cond_v").</summary>
    protected abstract string ValuePrefix { get; }

    // Factory methods
    protected abstract TResult CreateResult(
        string expression,
        Dictionary<string, string> names,
        Dictionary<string, AttributeValue> values);

    protected abstract TResult CreateEmptyResult();

    // Composition methods
    protected abstract TResult ComposeAnd(TResult left, TResult right);
    protected abstract TResult ComposeOr(TResult left, TResult right);

    // Null composition (throws)
    protected abstract Action ComposeAndNullLeft(TResult right);
    protected abstract Action ComposeAndNullRight(TResult left);
    protected abstract Action ComposeOrNullLeft(TResult right);
    protected abstract Action ComposeOrNullRight(TResult left);

    // Accessors
    protected abstract string GetExpression(TResult result);
    protected abstract IReadOnlyDictionary<string, string> GetNames(TResult result);
    protected abstract IReadOnlyDictionary<string, AttributeValue> GetValues(TResult result);
    protected abstract bool IsEmpty(TResult result);

    #region Helper — build aliases from prefix + index

    private string Name(int i) => $"{NamePrefix}{i}";
    private string Value(int i) => $"{ValuePrefix}{i}";

    #endregion

    #region And() Method Tests

    [Fact]
    public void And_TwoResults_ReAliasesRightOperand()
    {
        var left = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Status" },
            values: new() { [Value(0)] = new() { S = "Active" } });

        var right = CreateResult(
            expression: $"Total > {Value(0)}",
            names: new(),
            values: new() { [Value(0)] = new() { N = "100" } });

        var result = ComposeAnd(left, right);

        GetExpression(result).Should().Be($"({Name(0)} = {Value(0)}) AND (Total > {Value(1)})");
        GetNames(result).Should().HaveCount(1)
            .And.ContainKey(Name(0)).WhoseValue.Should().Be("Status");
        GetValues(result).Should().HaveCount(2)
            .And.ContainKeys(Value(0), Value(1));
        GetValues(result)[Value(0)].S.Should().Be("Active");
        GetValues(result)[Value(1)].N.Should().Be("100");
    }

    [Fact]
    public void And_LeftEmpty_ReturnsRight()
    {
        var left = CreateEmptyResult();
        var right = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Status" },
            values: new() { [Value(0)] = new() { S = "Active" } });

        var result = ComposeAnd(left, right);

        result.Should().BeSameAs(right);
    }

    [Fact]
    public void And_RightEmpty_ReturnsLeft()
    {
        var left = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Status" },
            values: new() { [Value(0)] = new() { S = "Active" } });
        var right = CreateEmptyResult();

        var result = ComposeAnd(left, right);

        result.Should().BeSameAs(left);
    }

    [Fact]
    public void And_BothEmpty_ReturnsEmpty()
    {
        var left = CreateEmptyResult();
        var right = CreateEmptyResult();

        var result = ComposeAnd(left, right);

        IsEmpty(result).Should().BeTrue();
        GetExpression(result).Should().BeEmpty();
        GetNames(result).Should().BeEmpty();
        GetValues(result).Should().BeEmpty();
    }

    [Fact]
    public void And_NullLeft_ThrowsArgumentNullException()
    {
        var right = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Status" },
            values: new() { [Value(0)] = new() { S = "Active" } });

        ComposeAndNullLeft(right).Should().Throw<ArgumentNullException>()
            .WithParameterName("left");
    }

    [Fact]
    public void And_NullRight_ThrowsArgumentNullException()
    {
        var left = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Status" },
            values: new() { [Value(0)] = new() { S = "Active" } });

        ComposeAndNullRight(left).Should().Throw<ArgumentNullException>()
            .WithParameterName("right");
    }

    [Fact]
    public void And_ChainedComposition_ContiguousIndices()
    {
        var r1 = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Status" },
            values: new() { [Value(0)] = new() { S = "Active" } });

        var r2 = CreateResult(
            expression: $"Total > {Value(0)}",
            names: new(),
            values: new() { [Value(0)] = new() { N = "100" } });

        var r3 = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Enabled" },
            values: new() { [Value(0)] = new() { BOOL = true } });

        var combined = ComposeAnd(r1, r2);
        var chained = ComposeAnd(combined, r3);

        GetExpression(chained).Should().Be(
            $"(({Name(0)} = {Value(0)}) AND (Total > {Value(1)})) AND ({Name(1)} = {Value(2)})");
        GetNames(chained).Should().HaveCount(2)
            .And.ContainKeys(Name(0), Name(1));
        GetNames(chained)[Name(0)].Should().Be("Status");
        GetNames(chained)[Name(1)].Should().Be("Enabled");
        GetValues(chained).Should().HaveCount(3)
            .And.ContainKeys(Value(0), Value(1), Value(2));
        GetValues(chained)[Value(0)].S.Should().Be("Active");
        GetValues(chained)[Value(1)].N.Should().Be("100");
        GetValues(chained)[Value(2)].BOOL.Should().BeTrue();
    }

    #endregion

    #region Or() Method Tests

    [Fact]
    public void Or_TwoResults_ReAliasesRightOperand()
    {
        var left = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Status" },
            values: new() { [Value(0)] = new() { S = "Active" } });

        var right = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Enabled" },
            values: new() { [Value(0)] = new() { BOOL = true } });

        var result = ComposeOr(left, right);

        GetExpression(result).Should().Be($"({Name(0)} = {Value(0)}) OR ({Name(1)} = {Value(1)})");
        GetNames(result).Should().HaveCount(2)
            .And.ContainKeys(Name(0), Name(1));
        GetNames(result)[Name(0)].Should().Be("Status");
        GetNames(result)[Name(1)].Should().Be("Enabled");
        GetValues(result).Should().HaveCount(2)
            .And.ContainKeys(Value(0), Value(1));
        GetValues(result)[Value(0)].S.Should().Be("Active");
        GetValues(result)[Value(1)].BOOL.Should().BeTrue();
    }

    [Fact]
    public void Or_LeftEmpty_ReturnsRight()
    {
        var left = CreateEmptyResult();
        var right = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Status" },
            values: new() { [Value(0)] = new() { S = "Active" } });

        var result = ComposeOr(left, right);

        result.Should().BeSameAs(right);
    }

    [Fact]
    public void Or_RightEmpty_ReturnsLeft()
    {
        var left = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Status" },
            values: new() { [Value(0)] = new() { S = "Active" } });
        var right = CreateEmptyResult();

        var result = ComposeOr(left, right);

        result.Should().BeSameAs(left);
    }

    [Fact]
    public void Or_BothEmpty_ReturnsEmpty()
    {
        var left = CreateEmptyResult();
        var right = CreateEmptyResult();

        var result = ComposeOr(left, right);

        IsEmpty(result).Should().BeTrue();
        GetExpression(result).Should().BeEmpty();
        GetNames(result).Should().BeEmpty();
        GetValues(result).Should().BeEmpty();
    }

    [Fact]
    public void Or_NullLeft_ThrowsArgumentNullException()
    {
        var right = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Status" },
            values: new() { [Value(0)] = new() { S = "Active" } });

        ComposeOrNullLeft(right).Should().Throw<ArgumentNullException>()
            .WithParameterName("left");
    }

    [Fact]
    public void Or_NullRight_ThrowsArgumentNullException()
    {
        var left = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Status" },
            values: new() { [Value(0)] = new() { S = "Active" } });

        ComposeOrNullRight(left).Should().Throw<ArgumentNullException>()
            .WithParameterName("right");
    }

    [Fact]
    public void Or_ChainedComposition_ContiguousIndices()
    {
        var r1 = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Status" },
            values: new() { [Value(0)] = new() { S = "Active" } });

        var r2 = CreateResult(
            expression: $"Total > {Value(0)}",
            names: new(),
            values: new() { [Value(0)] = new() { N = "100" } });

        var r3 = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Enabled" },
            values: new() { [Value(0)] = new() { BOOL = true } });

        var combined = ComposeOr(r1, r2);
        var chained = ComposeOr(combined, r3);

        GetExpression(chained).Should().Be(
            $"(({Name(0)} = {Value(0)}) OR (Total > {Value(1)})) OR ({Name(1)} = {Value(2)})");
        GetNames(chained).Should().HaveCount(2)
            .And.ContainKeys(Name(0), Name(1));
        GetNames(chained)[Name(0)].Should().Be("Status");
        GetNames(chained)[Name(1)].Should().Be("Enabled");
        GetValues(chained).Should().HaveCount(3)
            .And.ContainKeys(Value(0), Value(1), Value(2));
        GetValues(chained)[Value(0)].S.Should().Be("Active");
        GetValues(chained)[Value(1)].N.Should().Be("100");
        GetValues(chained)[Value(2)].BOOL.Should().BeTrue();
    }

    #endregion

    #region Re-aliasing Algorithm Verification

    [Fact]
    public void ReAliasing_ShiftsValueIndices()
    {
        var left = CreateResult(
            expression: $"{Name(0)} = {Value(0)} AND {Name(1)} > {Value(1)}",
            names: new()
            {
                [Name(0)] = "Status",
                [Name(1)] = "Total"
            },
            values: new()
            {
                [Value(0)] = new() { S = "Active" },
                [Value(1)] = new() { N = "100" }
            });

        var right = CreateResult(
            expression: $"{Name(0)} = {Value(0)}",
            names: new() { [Name(0)] = "Enabled" },
            values: new() { [Value(0)] = new() { BOOL = true } });

        var result = ComposeAnd(left, right);

        GetValues(result).Should().HaveCount(3)
            .And.ContainKeys(Value(0), Value(1), Value(2));
        GetValues(result)[Value(0)].S.Should().Be("Active");
        GetValues(result)[Value(1)].N.Should().Be("100");
        GetValues(result)[Value(2)].BOOL.Should().BeTrue();
        GetExpression(result).Should().Contain(Value(2));
    }

    [Fact]
    public void ReAliasing_HandlesMultipleAliases()
    {
        var left = CreateResult(
            expression: $"{Name(0)} = {Value(0)} AND {Name(1)} = {Value(1)}",
            names: new()
            {
                [Name(0)] = "Status",
                [Name(1)] = "Type"
            },
            values: new()
            {
                [Value(0)] = new() { S = "Active" },
                [Value(1)] = new() { S = "Premium" }
            });

        var right = CreateResult(
            expression: $"{Name(0)} = {Value(0)} AND {Name(1)} > {Value(1)}",
            names: new()
            {
                [Name(0)] = "Enabled",
                [Name(1)] = "Total"
            },
            values: new()
            {
                [Value(0)] = new() { BOOL = true },
                [Value(1)] = new() { N = "50" }
            });

        var result = ComposeAnd(left, right);

        GetExpression(result).Should().Be(
            $"({Name(0)} = {Value(0)} AND {Name(1)} = {Value(1)}) AND ({Name(2)} = {Value(2)} AND {Name(3)} > {Value(3)})");
        GetNames(result).Should().HaveCount(4)
            .And.ContainKeys(Name(0), Name(1), Name(2), Name(3));
        GetNames(result)[Name(0)].Should().Be("Status");
        GetNames(result)[Name(1)].Should().Be("Type");
        GetNames(result)[Name(2)].Should().Be("Enabled");
        GetNames(result)[Name(3)].Should().Be("Total");
        GetValues(result).Should().HaveCount(4)
            .And.ContainKeys(Value(0), Value(1), Value(2), Value(3));
    }

    #endregion
}
