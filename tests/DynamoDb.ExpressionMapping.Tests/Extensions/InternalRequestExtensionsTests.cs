using System.Reflection;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Extensions;

/// <summary>
/// Tests for InternalRequestExtensions ??= → = mutations.
/// Verifies that applying expressions to requests with pre-populated
/// ExpressionAttributeNames/Values dictionaries preserves the existing entries.
/// </summary>
public class InternalRequestExtensionsTests
{
    [Fact]
    public void ApplyProjection_GetItemRequest_PreservesExistingAttributeNames()
    {
        var request = new GetItemRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            }
        };

        var result = new ProjectionResult(
            "#proj_0",
            new Dictionary<string, string> { ["#proj_0"] = "NewAttr" },
            Array.Empty<PropertyPath>(),
            ProjectionShape.SingleProperty,
            new[] { "NewAttr" });

        InvokeApplyProjection(request, result);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        request.ExpressionAttributeNames["#proj_0"].Should().Be("NewAttr");
    }

    [Fact]
    public void ApplyProjection_QueryRequest_PreservesExistingAttributeNames()
    {
        var request = new QueryRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            }
        };

        var result = new ProjectionResult(
            "#proj_0",
            new Dictionary<string, string> { ["#proj_0"] = "NewAttr" },
            Array.Empty<PropertyPath>(),
            ProjectionShape.SingleProperty,
            new[] { "NewAttr" });

        InvokeApplyProjection(request, result);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
    }

    [Fact]
    public void ApplyProjection_ScanRequest_PreservesExistingAttributeNames()
    {
        var request = new ScanRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            }
        };

        var result = new ProjectionResult(
            "#proj_0",
            new Dictionary<string, string> { ["#proj_0"] = "NewAttr" },
            Array.Empty<PropertyPath>(),
            ProjectionShape.SingleProperty,
            new[] { "NewAttr" });

        InvokeApplyProjection(request, result);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
    }

    [Fact]
    public void ApplyProjection_KeysAndAttributes_PreservesExistingAttributeNames()
    {
        var keysAndAttributes = new KeysAndAttributes
        {
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            }
        };

        var result = new ProjectionResult(
            "#proj_0",
            new Dictionary<string, string> { ["#proj_0"] = "NewAttr" },
            Array.Empty<PropertyPath>(),
            ProjectionShape.SingleProperty,
            new[] { "NewAttr" });

        InvokeApplyProjection(keysAndAttributes, result);

        keysAndAttributes.ExpressionAttributeNames.Should().HaveCount(2);
        keysAndAttributes.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
    }

    [Fact]
    public void ApplyFilter_QueryRequest_PreservesExistingDictionaries()
    {
        var request = new QueryRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var filterResult = new FilterExpressionResult(
            "#filt_0 = :filt_v0",
            new Dictionary<string, string> { ["#filt_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":filt_v0"] = new AttributeValue { S = "Active" } });

        InvokeApplyFilter(request, filterResult);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        request.ExpressionAttributeValues.Should().HaveCount(2);
        request.ExpressionAttributeValues[":existing"].S.Should().Be("ExistingValue");
    }

    [Fact]
    public void ApplyFilter_ScanRequest_PreservesExistingDictionaries()
    {
        var request = new ScanRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var filterResult = new FilterExpressionResult(
            "#filt_0 = :filt_v0",
            new Dictionary<string, string> { ["#filt_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":filt_v0"] = new AttributeValue { S = "Active" } });

        InvokeApplyFilter(request, filterResult);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyCondition_PutItemRequest_PreservesExistingDictionaries()
    {
        var request = new PutItemRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var conditionResult = new ConditionExpressionResult(
            "#cond_0 = :cond_v0",
            new Dictionary<string, string> { ["#cond_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":cond_v0"] = new AttributeValue { S = "Active" } });

        InvokeApplyCondition(request, conditionResult);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        request.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyCondition_DeleteItemRequest_PreservesExistingDictionaries()
    {
        var request = new DeleteItemRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var conditionResult = new ConditionExpressionResult(
            "#cond_0 = :cond_v0",
            new Dictionary<string, string> { ["#cond_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":cond_v0"] = new AttributeValue { S = "Active" } });

        InvokeApplyCondition(request, conditionResult);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyCondition_UpdateItemRequest_PreservesExistingDictionaries()
    {
        var request = new UpdateItemRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var conditionResult = new ConditionExpressionResult(
            "#cond_0 = :cond_v0",
            new Dictionary<string, string> { ["#cond_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":cond_v0"] = new AttributeValue { S = "Active" } });

        InvokeApplyCondition(request, conditionResult);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void MergeAttributeNames_QueryRequest_PreservesExistingNames()
    {
        var request = new QueryRequest
        {
            TableName = "Test",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            }
        };

        var names = new Dictionary<string, string> { ["#new"] = "NewAttr" };

        InvokeMergeAttributeNames(request, names);

        request.ExpressionAttributeNames.Should().HaveCount(2);
        request.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        request.ExpressionAttributeNames["#new"].Should().Be("NewAttr");
    }

    [Fact]
    public void MergeAttributeValues_QueryRequest_PreservesExistingValues()
    {
        var request = new QueryRequest
        {
            TableName = "Test",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };

        var values = new Dictionary<string, AttributeValue>
        {
            [":new"] = new AttributeValue { S = "NewValue" }
        };

        InvokeMergeAttributeValues(request, values);

        request.ExpressionAttributeValues.Should().HaveCount(2);
        request.ExpressionAttributeValues[":existing"].S.Should().Be("ExistingValue");
        request.ExpressionAttributeValues[":new"].S.Should().Be("NewValue");
    }

    #region Reflection helpers for internal methods

    private static void InvokeApplyProjection(GetItemRequest request, ProjectionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyProjection",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(GetItemRequest), typeof(ProjectionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyProjection(QueryRequest request, ProjectionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyProjection",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(QueryRequest), typeof(ProjectionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyProjection(ScanRequest request, ProjectionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyProjection",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(ScanRequest), typeof(ProjectionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyProjection(KeysAndAttributes keysAndAttributes, ProjectionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyProjection",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(KeysAndAttributes), typeof(ProjectionResult) },
            null);
        method!.Invoke(null, new object[] { keysAndAttributes, result });
    }

    private static void InvokeApplyFilter(QueryRequest request, FilterExpressionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyFilter",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(QueryRequest), typeof(FilterExpressionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyFilter(ScanRequest request, FilterExpressionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyFilter",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(ScanRequest), typeof(FilterExpressionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyCondition(PutItemRequest request, ConditionExpressionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyCondition",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(PutItemRequest), typeof(ConditionExpressionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyCondition(DeleteItemRequest request, ConditionExpressionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyCondition",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(DeleteItemRequest), typeof(ConditionExpressionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeApplyCondition(UpdateItemRequest request, ConditionExpressionResult result)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "ApplyCondition",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(UpdateItemRequest), typeof(ConditionExpressionResult) },
            null);
        method!.Invoke(null, new object[] { request, result });
    }

    private static void InvokeMergeAttributeNames(QueryRequest request, IReadOnlyDictionary<string, string> names)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "MergeAttributeNames",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(QueryRequest), typeof(IReadOnlyDictionary<string, string>) },
            null);
        method!.Invoke(null, new object[] { request, names });
    }

    private static void InvokeMergeAttributeValues(QueryRequest request, IReadOnlyDictionary<string, AttributeValue> values)
    {
        var method = typeof(InternalRequestExtensions).GetMethod(
            "MergeAttributeValues",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(QueryRequest), typeof(IReadOnlyDictionary<string, AttributeValue>) },
            null);
        method!.Invoke(null, new object[] { request, values });
    }

    #endregion
}
