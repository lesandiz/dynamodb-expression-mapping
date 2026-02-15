using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Tests.Fixtures;

/// <summary>
/// Helper methods for building Dictionary&lt;string, AttributeValue&gt; in tests.
/// </summary>
public static class AttributeValueFixtures
{
    public static Dictionary<string, AttributeValue> CreateTestEntityItem(
        Guid? id = null,
        string name = "Test",
        int count = 0,
        long largeCount = 0,
        bool enabled = true,
        decimal total = 0m,
        double ratio = 0.0,
        TestStatus status = TestStatus.Active,
        int? optionalScore = null,
        DateTime? expiresOn = null,
        byte[]? payload = null,
        List<string>? tags = null,
        List<int>? scores = null,
        HashSet<string>? categories = null,
        Dictionary<string, string>? metadata = null)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["Id"] = new() { S = (id ?? Guid.NewGuid()).ToString() },
            ["Name"] = new() { S = name },
            ["Count"] = new() { N = count.ToString() },
            ["LargeCount"] = new() { N = largeCount.ToString() },
            ["Enabled"] = new() { BOOL = enabled },
            ["Total"] = new() { N = total.ToString("F2") },
            ["Ratio"] = new() { N = ratio.ToString("F10") },
            ["Status"] = new() { S = status.ToString() },
            ["CreatedAt"] = new() { S = DateTime.UtcNow.ToString("O") },
            ["ModifiedAt"] = new() { S = DateTimeOffset.UtcNow.ToString("O") },
            ["CustomerId"] = new() { S = Guid.NewGuid().ToString() }
        };

        if (optionalScore.HasValue)
            item["OptionalScore"] = new() { N = optionalScore.Value.ToString() };

        if (expiresOn.HasValue)
            item["ExpiresOn"] = new() { S = expiresOn.Value.ToString("O") };

        if (payload != null)
            item["Payload"] = new() { B = new MemoryStream(payload) };

        if (tags != null && tags.Count > 0)
            item["Tags"] = new() { L = tags.Select(t => new AttributeValue { S = t }).ToList() };

        if (scores != null && scores.Count > 0)
            item["Scores"] = new() { L = scores.Select(s => new AttributeValue { N = s.ToString() }).ToList() };

        if (categories != null && categories.Count > 0)
            item["Categories"] = new() { SS = categories.ToList() };

        if (metadata != null && metadata.Count > 0)
            item["Metadata"] = new()
            {
                M = metadata.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new AttributeValue { S = kvp.Value })
            };

        return item;
    }

    public static Dictionary<string, AttributeValue> CreateNestedEntityItem(
        Guid? id = null,
        string city = "London",
        string postCode = "SW1A 1AA",
        int floor = 0)
    {
        var item = CreateTestEntityItem(id: id);
        item["Address"] = new()
        {
            M = new Dictionary<string, AttributeValue>
            {
                ["City"] = new() { S = city },
                ["PostCode"] = new() { S = postCode },
                ["Floor"] = new() { N = floor.ToString() }
            }
        };
        return item;
    }

    public static Dictionary<string, AttributeValue> CreateKeyedEntityItem(
        string pk,
        string sk,
        string data = "test",
        TestStatus status = TestStatus.Active)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = pk },
            ["SK"] = new() { S = sk },
            ["Data"] = new() { S = data },
            ["Status"] = new() { S = status.ToString() }
        };
    }
}
