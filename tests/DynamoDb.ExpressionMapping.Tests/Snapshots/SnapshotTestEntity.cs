using DynamoDb.ExpressionMapping.Attributes;

namespace DynamoDb.ExpressionMapping.Tests.Snapshots;

/// <summary>
/// Entity designed for snapshot tests. Contains properties covering all
/// snapshot test scenarios: simple, nested, deeply nested, reserved keywords,
/// remapped attributes, enum comparison, and mixed combinations.
/// </summary>
public class SnapshotTestEntity
{
    public string Id { get; set; } = string.Empty;
    public string PK { get; set; } = string.Empty;            // Partition key (not reserved)
    public string SK { get; set; } = string.Empty;            // Sort key (not reserved)
    public string Name { get; set; } = string.Empty;          // Reserved keyword
    public string Status { get; set; } = string.Empty;        // Reserved keyword
    public int Count { get; set; }
    public bool Enabled { get; set; }
    public int? OptionalScore { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public List<string> TagList { get; set; } = new();
    public HashSet<string> Categories { get; set; } = new();
    public SnapshotStatus StatusEnum { get; set; }             // Enum for EnumComparison snapshot

    [DynamoDbAttribute("customer_id")]
    public string CustomerId { get; set; } = string.Empty;    // Remapped attribute

    public SnapshotAddress Address { get; set; } = new();
    public SnapshotContact Contact { get; set; } = new();
}

public class SnapshotAddress
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}

public class SnapshotContact
{
    public string Email { get; set; } = string.Empty;
    public SnapshotMailingAddress MailingAddress { get; set; } = new();
}

public class SnapshotMailingAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string PostCode { get; set; } = string.Empty;
}

public enum SnapshotStatus
{
    Active,
    Inactive,
    Suspended
}
