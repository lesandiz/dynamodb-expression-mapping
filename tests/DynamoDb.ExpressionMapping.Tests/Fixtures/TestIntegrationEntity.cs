using Amazon.DynamoDBv2.DataModel;
using DynamoDb.ExpressionMapping.Attributes;

namespace DynamoDb.ExpressionMapping.Tests.Fixtures;

/// <summary>
/// Comprehensive test entity for integration tests covering all built-in attribute types and edge cases.
/// Matches the specification in Spec 12.
/// </summary>
public class TestIntegrationEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;                    // Reserved keyword
    public int Count { get; set; }
    public long LargeCount { get; set; }
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public decimal Total { get; set; }
    public double Ratio { get; set; }
    public byte[]? Payload { get; set; }
    public int? OptionalScore { get; set; }             // Nullable value type
    public DateTime? ExpiresOn { get; set; }            // Nullable DateTime
    public List<string> Tags { get; set; } = new();
    public List<int> Scores { get; set; } = new();
    public HashSet<string> Categories { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public TestStatus Status { get; set; }              // Reserved keyword + enum

    [DynamoDbIgnore]
    public bool IsActive => Enabled && Status == TestStatus.Active;

    [DynamoDbAttribute("cust_id")]
    public Guid CustomerId { get; set; }

    [DynamoDbConverter(typeof(MoneyConverter))]
    public Money? Price { get; set; }                    // Per-property custom converter

    public TestAddress? Address { get; set; }            // Nested object
    public TestContact? Contact { get; set; }            // Nested object (3-level depth)
}

public class TestAddress
{
    public string City { get; set; } = string.Empty;
    public string PostCode { get; set; } = string.Empty;
    public int Floor { get; set; }                      // Nested non-string leaf
}

public class TestContact
{
    public string Phone { get; set; } = string.Empty;
    public TestAddress? MailingAddress { get; set; }      // 3-level nesting
}

public enum TestStatus { Active, Inactive, Suspended }

public record Money(decimal Amount, string Currency);
