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
{
    #region And() Method Tests

    [Fact]
    public void And_TwoConditions_ReAliasesRightOperand()
    {
        // Arrange
        var left = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        var right = CreateCondition(
            expression: "Total > :cond_v0",
            names: new Dictionary<string, string>(),
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { N = "100" }
            });

        // Act
        var result = ConditionExpressionResult.And(left, right);

        // Assert
        result.Expression.Should().Be("(#cond_0 = :cond_v0) AND (Total > :cond_v1)");
        result.ExpressionAttributeNames.Should().HaveCount(1)
            .And.ContainKey("#cond_0").WhoseValue.Should().Be("Status");
        result.ExpressionAttributeValues.Should().HaveCount(2)
            .And.ContainKeys(":cond_v0", ":cond_v1");
        result.ExpressionAttributeValues[":cond_v0"].S.Should().Be("Active");
        result.ExpressionAttributeValues[":cond_v1"].N.Should().Be("100");
    }

    [Fact]
    public void And_LeftEmpty_ReturnsRight()
    {
        // Arrange
        var left = CreateEmptyCondition();
        var right = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        // Act
        var result = ConditionExpressionResult.And(left, right);

        // Assert
        result.Should().BeSameAs(right);
    }

    [Fact]
    public void And_RightEmpty_ReturnsLeft()
    {
        // Arrange
        var left = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });
        var right = CreateEmptyCondition();

        // Act
        var result = ConditionExpressionResult.And(left, right);

        // Assert
        result.Should().BeSameAs(left);
    }

    [Fact]
    public void And_BothEmpty_ReturnsEmpty()
    {
        // Arrange
        var left = CreateEmptyCondition();
        var right = CreateEmptyCondition();

        // Act
        var result = ConditionExpressionResult.And(left, right);

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
        var right = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        // Act & Assert
        var act = () => ConditionExpressionResult.And(null!, right);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("left");
    }

    [Fact]
    public void And_NullRight_ThrowsArgumentNullException()
    {
        // Arrange
        var left = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        // Act & Assert
        var act = () => ConditionExpressionResult.And(left, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("right");
    }

    [Fact]
    public void And_ChainedComposition_ContiguousIndices()
    {
        // Arrange - Build three conditions and chain them: And(And(a, b), c)
        var condition1 = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        var condition2 = CreateCondition(
            expression: "Total > :cond_v0",
            names: new Dictionary<string, string>(),
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { N = "100" }
            });

        var condition3 = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { BOOL = true }
            });

        // Act - Chain: (condition1 AND condition2) AND condition3
        var combined = ConditionExpressionResult.And(condition1, condition2);
        var chained = ConditionExpressionResult.And(combined, condition3);

        // Assert
        chained.Expression.Should().Be("((#cond_0 = :cond_v0) AND (Total > :cond_v1)) AND (#cond_1 = :cond_v2)");
        chained.ExpressionAttributeNames.Should().HaveCount(2)
            .And.ContainKeys("#cond_0", "#cond_1");
        chained.ExpressionAttributeNames["#cond_0"].Should().Be("Status");
        chained.ExpressionAttributeNames["#cond_1"].Should().Be("Enabled");
        chained.ExpressionAttributeValues.Should().HaveCount(3)
            .And.ContainKeys(":cond_v0", ":cond_v1", ":cond_v2");
        chained.ExpressionAttributeValues[":cond_v0"].S.Should().Be("Active");
        chained.ExpressionAttributeValues[":cond_v1"].N.Should().Be("100");
        chained.ExpressionAttributeValues[":cond_v2"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void And_WrapsInParentheses()
    {
        // Arrange
        var left = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        var right = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { BOOL = true }
            });

        // Act
        var result = ConditionExpressionResult.And(left, right);

        // Assert
        result.Expression.Should().StartWith("(")
            .And.Contain(") AND (")
            .And.EndWith(")");
    }

    #endregion

    #region Or() Method Tests

    [Fact]
    public void Or_TwoConditions_ReAliasesRightOperand()
    {
        // Arrange
        var left = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        var right = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { BOOL = true }
            });

        // Act
        var result = ConditionExpressionResult.Or(left, right);

        // Assert
        result.Expression.Should().Be("(#cond_0 = :cond_v0) OR (#cond_1 = :cond_v1)");
        result.ExpressionAttributeNames.Should().HaveCount(2)
            .And.ContainKeys("#cond_0", "#cond_1");
        result.ExpressionAttributeNames["#cond_0"].Should().Be("Status");
        result.ExpressionAttributeNames["#cond_1"].Should().Be("Enabled");
        result.ExpressionAttributeValues.Should().HaveCount(2)
            .And.ContainKeys(":cond_v0", ":cond_v1");
        result.ExpressionAttributeValues[":cond_v0"].S.Should().Be("Active");
        result.ExpressionAttributeValues[":cond_v1"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void Or_LeftEmpty_ReturnsRight()
    {
        // Arrange
        var left = CreateEmptyCondition();
        var right = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        // Act
        var result = ConditionExpressionResult.Or(left, right);

        // Assert
        result.Should().BeSameAs(right);
    }

    [Fact]
    public void Or_RightEmpty_ReturnsLeft()
    {
        // Arrange
        var left = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });
        var right = CreateEmptyCondition();

        // Act
        var result = ConditionExpressionResult.Or(left, right);

        // Assert
        result.Should().BeSameAs(left);
    }

    [Fact]
    public void Or_BothEmpty_ReturnsEmpty()
    {
        // Arrange
        var left = CreateEmptyCondition();
        var right = CreateEmptyCondition();

        // Act
        var result = ConditionExpressionResult.Or(left, right);

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
        var right = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        // Act & Assert
        var act = () => ConditionExpressionResult.Or(null!, right);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("left");
    }

    [Fact]
    public void Or_NullRight_ThrowsArgumentNullException()
    {
        // Arrange
        var left = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        // Act & Assert
        var act = () => ConditionExpressionResult.Or(left, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("right");
    }

    [Fact]
    public void Or_ChainedComposition_ContiguousIndices()
    {
        // Arrange - Build three conditions and chain them: Or(Or(a, b), c)
        var condition1 = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        var condition2 = CreateCondition(
            expression: "Total > :cond_v0",
            names: new Dictionary<string, string>(),
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { N = "100" }
            });

        var condition3 = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { BOOL = true }
            });

        // Act - Chain: (condition1 OR condition2) OR condition3
        var combined = ConditionExpressionResult.Or(condition1, condition2);
        var chained = ConditionExpressionResult.Or(combined, condition3);

        // Assert
        chained.Expression.Should().Be("((#cond_0 = :cond_v0) OR (Total > :cond_v1)) OR (#cond_1 = :cond_v2)");
        chained.ExpressionAttributeNames.Should().HaveCount(2)
            .And.ContainKeys("#cond_0", "#cond_1");
        chained.ExpressionAttributeNames["#cond_0"].Should().Be("Status");
        chained.ExpressionAttributeNames["#cond_1"].Should().Be("Enabled");
        chained.ExpressionAttributeValues.Should().HaveCount(3)
            .And.ContainKeys(":cond_v0", ":cond_v1", ":cond_v2");
        chained.ExpressionAttributeValues[":cond_v0"].S.Should().Be("Active");
        chained.ExpressionAttributeValues[":cond_v1"].N.Should().Be("100");
        chained.ExpressionAttributeValues[":cond_v2"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void Or_WrapsInParentheses()
    {
        // Arrange
        var left = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        var right = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { BOOL = true }
            });

        // Act
        var result = ConditionExpressionResult.Or(left, right);

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
        // Arrange - Left has #cond_0, right also has #cond_0
        // Right's #cond_0 should become #cond_1
        var left = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        var right = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { BOOL = true }
            });

        // Act
        var result = ConditionExpressionResult.And(left, right);

        // Assert - Verify right's #cond_0 became #cond_1
        result.ExpressionAttributeNames.Should().HaveCount(2);
        result.ExpressionAttributeNames["#cond_0"].Should().Be("Status");
        result.ExpressionAttributeNames["#cond_1"].Should().Be("Enabled");
        result.Expression.Should().Contain("#cond_1");
    }

    [Fact]
    public void ReAliasing_ShiftsValueIndices()
    {
        // Arrange - Left has :cond_v0 and :cond_v1, right has :cond_v0
        // Right's :cond_v0 should become :cond_v2
        var left = CreateCondition(
            expression: "#cond_0 = :cond_v0 AND #cond_1 > :cond_v1",
            names: new Dictionary<string, string>
            {
                ["#cond_0"] = "Status",
                ["#cond_1"] = "Total"
            },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" },
                [":cond_v1"] = new() { N = "100" }
            });

        var right = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { BOOL = true }
            });

        // Act
        var result = ConditionExpressionResult.And(left, right);

        // Assert - Verify right's :cond_v0 became :cond_v2
        result.ExpressionAttributeValues.Should().HaveCount(3)
            .And.ContainKeys(":cond_v0", ":cond_v1", ":cond_v2");
        result.ExpressionAttributeValues[":cond_v0"].S.Should().Be("Active");
        result.ExpressionAttributeValues[":cond_v1"].N.Should().Be("100");
        result.ExpressionAttributeValues[":cond_v2"].BOOL.Should().BeTrue();
        result.Expression.Should().Contain(":cond_v2");
    }

    [Fact]
    public void ReAliasing_HandlesMultipleAliases()
    {
        // Arrange - Both sides have multiple aliases
        var left = CreateCondition(
            expression: "#cond_0 = :cond_v0 AND #cond_1 = :cond_v1",
            names: new Dictionary<string, string>
            {
                ["#cond_0"] = "Status",
                ["#cond_1"] = "Type"
            },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" },
                [":cond_v1"] = new() { S = "Premium" }
            });

        var right = CreateCondition(
            expression: "#cond_0 = :cond_v0 AND #cond_1 > :cond_v1",
            names: new Dictionary<string, string>
            {
                ["#cond_0"] = "Enabled",
                ["#cond_1"] = "Total"
            },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { BOOL = true },
                [":cond_v1"] = new() { N = "50" }
            });

        // Act
        var result = ConditionExpressionResult.And(left, right);

        // Assert - Right's aliases shifted: #cond_0→#cond_2, #cond_1→#cond_3, :cond_v0→:cond_v2, :cond_v1→:cond_v3
        result.Expression.Should().Be("(#cond_0 = :cond_v0 AND #cond_1 = :cond_v1) AND (#cond_2 = :cond_v2 AND #cond_3 > :cond_v3)");
        result.ExpressionAttributeNames.Should().HaveCount(4)
            .And.ContainKeys("#cond_0", "#cond_1", "#cond_2", "#cond_3");
        result.ExpressionAttributeNames["#cond_0"].Should().Be("Status");
        result.ExpressionAttributeNames["#cond_1"].Should().Be("Type");
        result.ExpressionAttributeNames["#cond_2"].Should().Be("Enabled");
        result.ExpressionAttributeNames["#cond_3"].Should().Be("Total");
        result.ExpressionAttributeValues.Should().HaveCount(4)
            .And.ContainKeys(":cond_v0", ":cond_v1", ":cond_v2", ":cond_v3");
    }

    #endregion

    #region Alias Scope Verification

    [Fact]
    public void ComposedConditions_UseCondScopeNotFiltScope()
    {
        // Arrange - Verify composed conditions use #cond_ and :cond_v, not #filt_ and :filt_v
        var left = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        var right = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { BOOL = true }
            });

        // Act
        var result = ConditionExpressionResult.And(left, right);

        // Assert - Verify no filter aliases present
        result.Expression.Should().Contain("#cond_");
        result.Expression.Should().Contain(":cond_v");
        result.Expression.Should().NotContain("#filt_");
        result.Expression.Should().NotContain(":filt_v");
        result.ExpressionAttributeNames.Keys.Should().AllSatisfy(k => k.Should().StartWith("#cond_"));
        result.ExpressionAttributeValues.Keys.Should().AllSatisfy(k => k.Should().StartWith(":cond_v"));
    }

    #endregion

    #region Worked Example from Spec 06 §6.6

    [Fact]
    public void WorkedExample_FromSpec_VerifiesCompleteFlow_WithCondPrefix()
    {
        // Arrange - Replicate the exact example from Spec 06 §6.6 but with #cond_ prefix
        var condition1 = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Status" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { S = "Active" }
            });

        var condition2 = CreateCondition(
            expression: "Total > :cond_v0",
            names: new Dictionary<string, string>(),
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { N = "100" }
            });

        var condition3 = CreateCondition(
            expression: "#cond_0 = :cond_v0",
            names: new Dictionary<string, string> { ["#cond_0"] = "Enabled" },
            values: new Dictionary<string, AttributeValue>
            {
                [":cond_v0"] = new() { BOOL = true }
            });

        // Act - Replicate the exact composition: And(And(condition1, condition2), condition3)
        var combined = ConditionExpressionResult.And(condition1, condition2);
        var chained = ConditionExpressionResult.And(combined, condition3);

        // Assert - Verify the exact output from the spec with #cond_ prefix
        // Combined (condition1 AND condition2):
        combined.Expression.Should().Be("(#cond_0 = :cond_v0) AND (Total > :cond_v1)");
        combined.ExpressionAttributeNames.Should().HaveCount(1)
            .And.ContainKey("#cond_0").WhoseValue.Should().Be("Status");
        combined.ExpressionAttributeValues.Should().HaveCount(2);
        combined.ExpressionAttributeValues[":cond_v0"].S.Should().Be("Active");
        combined.ExpressionAttributeValues[":cond_v1"].N.Should().Be("100");

        // Chained ((condition1 AND condition2) AND condition3):
        chained.Expression.Should().Be("((#cond_0 = :cond_v0) AND (Total > :cond_v1)) AND (#cond_1 = :cond_v2)");
        chained.ExpressionAttributeNames.Should().HaveCount(2);
        chained.ExpressionAttributeNames["#cond_0"].Should().Be("Status");
        chained.ExpressionAttributeNames["#cond_1"].Should().Be("Enabled");
        chained.ExpressionAttributeValues.Should().HaveCount(3);
        chained.ExpressionAttributeValues[":cond_v0"].S.Should().Be("Active");
        chained.ExpressionAttributeValues[":cond_v1"].N.Should().Be("100");
        chained.ExpressionAttributeValues[":cond_v2"].BOOL.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static ConditionExpressionResult CreateCondition(
        string expression,
        Dictionary<string, string> names,
        Dictionary<string, AttributeValue> values)
    {
        return new ConditionExpressionResult(expression, names, values);
    }

    private static ConditionExpressionResult CreateEmptyCondition()
    {
        return new ConditionExpressionResult(
            string.Empty,
            new Dictionary<string, string>(),
            new Dictionary<string, AttributeValue>());
    }

    #endregion
}
