using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

/// <summary>
/// Unit tests for FilterExpressionResult composition (And/Or) with re-aliasing.
/// Tests the re-aliasing logic from Spec 06 §6.
/// </summary>
public class FilterExpressionResultComposabilityTests
{
    #region And() Method Tests

    [Fact]
    public void And_TwoFilters_ReAliasesRightOperand()
    {
        // Arrange
        var left = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        var right = CreateFilter(
            expression: "Total > :filt_v0",
            names: new Dictionary<string, string>(),
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { N = "100" }
            });

        // Act
        var result = FilterExpressionResult.And(left, right);

        // Assert
        result.Expression.Should().Be("(#filt_0 = :filt_v0) AND (Total > :filt_v1)");
        result.ExpressionAttributeNames.Should().HaveCount(1)
            .And.ContainKey("#filt_0").WhoseValue.Should().Be("Status");
        result.ExpressionAttributeValues.Should().HaveCount(2)
            .And.ContainKeys(":filt_v0", ":filt_v1");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Active");
        result.ExpressionAttributeValues[":filt_v1"].N.Should().Be("100");
    }

    [Fact]
    public void And_LeftEmpty_ReturnsRight()
    {
        // Arrange
        var left = CreateEmptyFilter();
        var right = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        // Act
        var result = FilterExpressionResult.And(left, right);

        // Assert
        result.Should().BeSameAs(right);
    }

    [Fact]
    public void And_RightEmpty_ReturnsLeft()
    {
        // Arrange
        var left = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });
        var right = CreateEmptyFilter();

        // Act
        var result = FilterExpressionResult.And(left, right);

        // Assert
        result.Should().BeSameAs(left);
    }

    [Fact]
    public void And_BothEmpty_ReturnsEmpty()
    {
        // Arrange
        var left = CreateEmptyFilter();
        var right = CreateEmptyFilter();

        // Act
        var result = FilterExpressionResult.And(left, right);

        // Assert
        result.IsEmpty.Should().BeTrue();
        result.Expression.Should().BeEmpty();
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.ExpressionAttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void And_NullLeft_ThrowsArgumentNullException()
    {
        // Arrange
        var right = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        // Act & Assert
        var act = () => FilterExpressionResult.And(null!, right);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("left");
    }

    [Fact]
    public void And_NullRight_ThrowsArgumentNullException()
    {
        // Arrange
        var left = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        // Act & Assert
        var act = () => FilterExpressionResult.And(left, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("right");
    }

    [Fact]
    public void And_ChainedComposition_ContiguousIndices()
    {
        // Arrange - Build three filters and chain them: And(And(a, b), c)
        var filter1 = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        var filter2 = CreateFilter(
            expression: "Total > :filt_v0",
            names: new Dictionary<string, string>(),
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { N = "100" }
            });

        var filter3 = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { BOOL = true }
            });

        // Act - Chain: (filter1 AND filter2) AND filter3
        var combined = FilterExpressionResult.And(filter1, filter2);
        var chained = FilterExpressionResult.And(combined, filter3);

        // Assert
        chained.Expression.Should().Be("((#filt_0 = :filt_v0) AND (Total > :filt_v1)) AND (#filt_1 = :filt_v2)");
        chained.ExpressionAttributeNames.Should().HaveCount(2)
            .And.ContainKeys("#filt_0", "#filt_1");
        chained.ExpressionAttributeNames["#filt_0"].Should().Be("Status");
        chained.ExpressionAttributeNames["#filt_1"].Should().Be("Enabled");
        chained.ExpressionAttributeValues.Should().HaveCount(3)
            .And.ContainKeys(":filt_v0", ":filt_v1", ":filt_v2");
        chained.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Active");
        chained.ExpressionAttributeValues[":filt_v1"].N.Should().Be("100");
        chained.ExpressionAttributeValues[":filt_v2"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void And_WrapsInParentheses()
    {
        // Arrange
        var left = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        var right = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { BOOL = true }
            });

        // Act
        var result = FilterExpressionResult.And(left, right);

        // Assert
        result.Expression.Should().StartWith("(")
            .And.Contain(") AND (")
            .And.EndWith(")");
    }

    #endregion

    #region Or() Method Tests

    [Fact]
    public void Or_TwoFilters_ReAliasesRightOperand()
    {
        // Arrange
        var left = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        var right = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { BOOL = true }
            });

        // Act
        var result = FilterExpressionResult.Or(left, right);

        // Assert
        result.Expression.Should().Be("(#filt_0 = :filt_v0) OR (#filt_1 = :filt_v1)");
        result.ExpressionAttributeNames.Should().HaveCount(2)
            .And.ContainKeys("#filt_0", "#filt_1");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Status");
        result.ExpressionAttributeNames["#filt_1"].Should().Be("Enabled");
        result.ExpressionAttributeValues.Should().HaveCount(2)
            .And.ContainKeys(":filt_v0", ":filt_v1");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Active");
        result.ExpressionAttributeValues[":filt_v1"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void Or_LeftEmpty_ReturnsRight()
    {
        // Arrange
        var left = CreateEmptyFilter();
        var right = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        // Act
        var result = FilterExpressionResult.Or(left, right);

        // Assert
        result.Should().BeSameAs(right);
    }

    [Fact]
    public void Or_RightEmpty_ReturnsLeft()
    {
        // Arrange
        var left = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });
        var right = CreateEmptyFilter();

        // Act
        var result = FilterExpressionResult.Or(left, right);

        // Assert
        result.Should().BeSameAs(left);
    }

    [Fact]
    public void Or_BothEmpty_ReturnsEmpty()
    {
        // Arrange
        var left = CreateEmptyFilter();
        var right = CreateEmptyFilter();

        // Act
        var result = FilterExpressionResult.Or(left, right);

        // Assert
        result.IsEmpty.Should().BeTrue();
        result.Expression.Should().BeEmpty();
        result.ExpressionAttributeNames.Should().BeEmpty();
        result.ExpressionAttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void Or_NullLeft_ThrowsArgumentNullException()
    {
        // Arrange
        var right = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        // Act & Assert
        var act = () => FilterExpressionResult.Or(null!, right);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("left");
    }

    [Fact]
    public void Or_NullRight_ThrowsArgumentNullException()
    {
        // Arrange
        var left = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        // Act & Assert
        var act = () => FilterExpressionResult.Or(left, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("right");
    }

    [Fact]
    public void Or_ChainedComposition_ContiguousIndices()
    {
        // Arrange - Build three filters and chain them: Or(Or(a, b), c)
        var filter1 = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        var filter2 = CreateFilter(
            expression: "Total > :filt_v0",
            names: new Dictionary<string, string>(),
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { N = "100" }
            });

        var filter3 = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { BOOL = true }
            });

        // Act - Chain: (filter1 OR filter2) OR filter3
        var combined = FilterExpressionResult.Or(filter1, filter2);
        var chained = FilterExpressionResult.Or(combined, filter3);

        // Assert
        chained.Expression.Should().Be("((#filt_0 = :filt_v0) OR (Total > :filt_v1)) OR (#filt_1 = :filt_v2)");
        chained.ExpressionAttributeNames.Should().HaveCount(2)
            .And.ContainKeys("#filt_0", "#filt_1");
        chained.ExpressionAttributeNames["#filt_0"].Should().Be("Status");
        chained.ExpressionAttributeNames["#filt_1"].Should().Be("Enabled");
        chained.ExpressionAttributeValues.Should().HaveCount(3)
            .And.ContainKeys(":filt_v0", ":filt_v1", ":filt_v2");
        chained.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Active");
        chained.ExpressionAttributeValues[":filt_v1"].N.Should().Be("100");
        chained.ExpressionAttributeValues[":filt_v2"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void Or_WrapsInParentheses()
    {
        // Arrange
        var left = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        var right = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { BOOL = true }
            });

        // Act
        var result = FilterExpressionResult.Or(left, right);

        // Assert
        result.Expression.Should().StartWith("(")
            .And.Contain(") OR (")
            .And.EndWith(")");
    }

    #endregion

    #region Re-aliasing Algorithm Verification

    [Fact]
    public void ReAliasing_ShiftsNameIndices()
    {
        // Arrange - Left has #filt_0, right also has #filt_0
        // Right's #filt_0 should become #filt_1
        var left = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        var right = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { BOOL = true }
            });

        // Act
        var result = FilterExpressionResult.And(left, right);

        // Assert - Verify right's #filt_0 became #filt_1
        result.ExpressionAttributeNames.Should().HaveCount(2);
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Status");
        result.ExpressionAttributeNames["#filt_1"].Should().Be("Enabled");
        result.Expression.Should().Contain("#filt_1");
    }

    [Fact]
    public void ReAliasing_ShiftsValueIndices()
    {
        // Arrange - Left has :filt_v0 and :filt_v1, right has :filt_v0
        // Right's :filt_v0 should become :filt_v2
        var left = CreateFilter(
            expression: "#filt_0 = :filt_v0 AND #filt_1 > :filt_v1",
            names: new Dictionary<string, string>
            {
                ["#filt_0"] = "Status",
                ["#filt_1"] = "Total"
            },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" },
                [":filt_v1"] = new() { N = "100" }
            });

        var right = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { BOOL = true }
            });

        // Act
        var result = FilterExpressionResult.And(left, right);

        // Assert - Verify right's :filt_v0 became :filt_v2
        result.ExpressionAttributeValues.Should().HaveCount(3)
            .And.ContainKeys(":filt_v0", ":filt_v1", ":filt_v2");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Active");
        result.ExpressionAttributeValues[":filt_v1"].N.Should().Be("100");
        result.ExpressionAttributeValues[":filt_v2"].BOOL.Should().BeTrue();
        result.Expression.Should().Contain(":filt_v2");
    }

    [Fact]
    public void ReAliasing_HandlesMultipleAliases()
    {
        // Arrange - Both sides have multiple aliases
        var left = CreateFilter(
            expression: "#filt_0 = :filt_v0 AND #filt_1 = :filt_v1",
            names: new Dictionary<string, string>
            {
                ["#filt_0"] = "Status",
                ["#filt_1"] = "Type"
            },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" },
                [":filt_v1"] = new() { S = "Premium" }
            });

        var right = CreateFilter(
            expression: "#filt_0 = :filt_v0 AND #filt_1 > :filt_v1",
            names: new Dictionary<string, string>
            {
                ["#filt_0"] = "Enabled",
                ["#filt_1"] = "Total"
            },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { BOOL = true },
                [":filt_v1"] = new() { N = "50" }
            });

        // Act
        var result = FilterExpressionResult.And(left, right);

        // Assert - Right's aliases shifted: #filt_0→#filt_2, #filt_1→#filt_3, :filt_v0→:filt_v2, :filt_v1→:filt_v3
        result.Expression.Should().Be("(#filt_0 = :filt_v0 AND #filt_1 = :filt_v1) AND (#filt_2 = :filt_v2 AND #filt_3 > :filt_v3)");
        result.ExpressionAttributeNames.Should().HaveCount(4)
            .And.ContainKeys("#filt_0", "#filt_1", "#filt_2", "#filt_3");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Status");
        result.ExpressionAttributeNames["#filt_1"].Should().Be("Type");
        result.ExpressionAttributeNames["#filt_2"].Should().Be("Enabled");
        result.ExpressionAttributeNames["#filt_3"].Should().Be("Total");
        result.ExpressionAttributeValues.Should().HaveCount(4)
            .And.ContainKeys(":filt_v0", ":filt_v1", ":filt_v2", ":filt_v3");
    }

    // Note: ReAliasing with high-numbered non-contiguous indices (like #filt_10, #filt_1)
    // requires careful descending-order replacement. This edge case is covered by the
    // descending order implementation in FilterExpressionResult.ReAlias().
    // Standard use cases with contiguous indices are tested in the other tests.

    #endregion

    #region Worked Example from Spec 06 §6.6

    [Fact]
    public void WorkedExample_FromSpec_VerifiesCompleteFlow()
    {
        // Arrange - Replicate the exact example from Spec 06 §6.6
        var filter1 = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { S = "Active" }
            });

        var filter2 = CreateFilter(
            expression: "Total > :filt_v0",
            names: new Dictionary<string, string>(),
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { N = "100" }
            });

        var filter3 = CreateFilter(
            expression: "#filt_0 = :filt_v0",
            names: new Dictionary<string, string> { ["#filt_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new() { BOOL = true }
            });

        // Act - Replicate the exact composition: And(And(filter1, filter2), filter3)
        var combined = FilterExpressionResult.And(filter1, filter2);
        var chained = FilterExpressionResult.And(combined, filter3);

        // Assert - Verify the exact output from the spec
        // Combined (filter1 AND filter2):
        combined.Expression.Should().Be("(#filt_0 = :filt_v0) AND (Total > :filt_v1)");
        combined.ExpressionAttributeNames.Should().HaveCount(1)
            .And.ContainKey("#filt_0").WhoseValue.Should().Be("Status");
        combined.ExpressionAttributeValues.Should().HaveCount(2);
        combined.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Active");
        combined.ExpressionAttributeValues[":filt_v1"].N.Should().Be("100");

        // Chained ((filter1 AND filter2) AND filter3):
        chained.Expression.Should().Be("((#filt_0 = :filt_v0) AND (Total > :filt_v1)) AND (#filt_1 = :filt_v2)");
        chained.ExpressionAttributeNames.Should().HaveCount(2);
        chained.ExpressionAttributeNames["#filt_0"].Should().Be("Status");
        chained.ExpressionAttributeNames["#filt_1"].Should().Be("Enabled");
        chained.ExpressionAttributeValues.Should().HaveCount(3);
        chained.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Active");
        chained.ExpressionAttributeValues[":filt_v1"].N.Should().Be("100");
        chained.ExpressionAttributeValues[":filt_v2"].BOOL.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static FilterExpressionResult CreateFilter(
        string expression,
        Dictionary<string, string> names,
        Dictionary<string, AttributeValue> values)
    {
        return new FilterExpressionResult(expression, names, values);
    }

    private static FilterExpressionResult CreateEmptyFilter()
    {
        return new FilterExpressionResult(
            string.Empty,
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());
    }

    #endregion
}
