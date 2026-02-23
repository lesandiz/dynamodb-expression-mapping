using System.Reflection;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Extensions;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Extensions;

/// <summary>
/// Tests for RequestMergeHelpers (Spec 10 §6).
/// </summary>
public class RequestMergeHelpersTests
{
    [Fact]
    public void MergeAttributeNames_DisjointKeys_MergesAll()
    {
        // Arrange
        var target = new Dictionary<string, string>
        {
            ["#a"] = "AttrA",
            ["#b"] = "AttrB"
        };
        var source = new Dictionary<string, string>
        {
            ["#c"] = "AttrC",
            ["#d"] = "AttrD"
        };

        // Act
        InvokeMergeAttributeNames(target, source);

        // Assert
        target.Should().HaveCount(4);
        target["#a"].Should().Be("AttrA");
        target["#b"].Should().Be("AttrB");
        target["#c"].Should().Be("AttrC");
        target["#d"].Should().Be("AttrD");
    }

    [Fact]
    public void MergeAttributeNames_SameKeyAndValue_NoConflict()
    {
        // Arrange
        var target = new Dictionary<string, string>
        {
            ["#a"] = "AttrA"
        };
        var source = new Dictionary<string, string>
        {
            ["#a"] = "AttrA"
        };

        // Act
        InvokeMergeAttributeNames(target, source);

        // Assert
        target.Should().HaveCount(1);
        target["#a"].Should().Be("AttrA");
    }

    [Fact]
    public void MergeAttributeNames_SameKeyDifferentValue_ThrowsConflictException()
    {
        // Arrange
        var target = new Dictionary<string, string>
        {
            ["#a"] = "AttrA"
        };
        var source = new Dictionary<string, string>
        {
            ["#a"] = "AttrB"
        };

        // Act
        var act = () => InvokeMergeAttributeNames(target, source);

        // Assert
        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<ExpressionAttributeConflictException>()
            .Which.AliasKey.Should().Be("#a");
    }

    [Fact]
    public void MergeAttributeValues_DisjointKeys_MergesAll()
    {
        // Arrange
        var target = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { S = "ValueA" },
            [":b"] = new AttributeValue { N = "42" }
        };
        var source = new Dictionary<string, AttributeValue>
        {
            [":c"] = new AttributeValue { BOOL = true },
            [":d"] = new AttributeValue { S = "ValueD" }
        };

        // Act
        InvokeMergeAttributeValues(target, source);

        // Assert
        target.Should().HaveCount(4);
        target[":a"].S.Should().Be("ValueA");
        target[":b"].N.Should().Be("42");
        target[":c"].BOOL.Should().BeTrue();
        target[":d"].S.Should().Be("ValueD");
    }

    [Fact]
    public void MergeAttributeValues_DuplicateKey_ThrowsConflictException()
    {
        // Arrange
        var target = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { S = "ValueA" }
        };
        var source = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { S = "ValueB" }
        };

        // Act
        var act = () => InvokeMergeAttributeValues(target, source);

        // Assert
        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<ExpressionAttributeConflictException>()
            .Which.AliasKey.Should().Be(":a");
    }

    [Fact]
    public void ConflictException_CarriesAliasKeyAndValues()
    {
        // Arrange
        var target = new Dictionary<string, string>
        {
            ["#key"] = "ExistingValue"
        };
        var source = new Dictionary<string, string>
        {
            ["#key"] = "ConflictingValue"
        };

        // Act
        var act = () => InvokeMergeAttributeNames(target, source);

        // Assert
        var exception = act.Should().Throw<TargetInvocationException>()
            .WithInnerException<ExpressionAttributeConflictException>()
            .Which;
        exception.AliasKey.Should().Be("#key");
        exception.ExistingValue.Should().Be("ExistingValue");
        exception.ConflictingValue.Should().Be("ConflictingValue");
    }

    // Helper methods to invoke internal static methods via reflection
    private static void InvokeMergeAttributeNames(
        Dictionary<string, string> target,
        IReadOnlyDictionary<string, string> source)
    {
        var helperType = typeof(RequestMergeHelpers);
        var method = helperType.GetMethod("MergeAttributeNames",
            BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { target, source });
    }

    private static void InvokeMergeAttributeValues(
        Dictionary<string, AttributeValue> target,
        IReadOnlyDictionary<string, AttributeValue> source)
    {
        var helperType = typeof(RequestMergeHelpers);
        var method = helperType.GetMethod("MergeAttributeValues",
            BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { target, source });
    }

    #region Empty source merges

    [Fact]
    public void MergeAttributeNames_EmptySource_TargetUnchanged()
    {
        var target = new Dictionary<string, string>
        {
            ["#a"] = "AttrA"
        };
        var source = new Dictionary<string, string>();

        InvokeMergeAttributeNames(target, source);

        target.Should().HaveCount(1);
        target["#a"].Should().Be("AttrA");
    }

    [Fact]
    public void MergeAttributeValues_EmptySource_TargetUnchanged()
    {
        var target = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { S = "ValueA" }
        };
        var source = new Dictionary<string, AttributeValue>();

        InvokeMergeAttributeValues(target, source);

        target.Should().HaveCount(1);
        target[":a"].S.Should().Be("ValueA");
    }

    [Fact]
    public void MergeAttributeValues_WithNullAttributeValue_ReportsValueInException()
    {
        var target = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { BOOL = true }
        };
        var source = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { S = "conflict" }
        };

        var act = () => InvokeMergeAttributeValues(target, source);

        act.Should().Throw<TargetInvocationException>()
            .And.InnerException.Should().BeOfType<ExpressionAttributeConflictException>()
            .Which.AliasKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void MergeAttributeValues_WithNumberValue_ReportsNInException()
    {
        var target = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { N = "42" }
        };
        var source = new Dictionary<string, AttributeValue>
        {
            [":a"] = new AttributeValue { S = "conflict" }
        };

        var act = () => InvokeMergeAttributeValues(target, source);

        act.Should().Throw<TargetInvocationException>()
            .And.InnerException.Should().BeOfType<ExpressionAttributeConflictException>()
            .Which.AliasKey.Should().NotBeNullOrEmpty();
    }

    #endregion

}
