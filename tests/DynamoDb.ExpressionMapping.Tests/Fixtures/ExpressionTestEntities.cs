using DynamoDb.ExpressionMapping.Attributes;

namespace DynamoDb.ExpressionMapping.Tests.Fixtures;

/// <summary>
/// Enhanced test entity with all property types needed for comprehensive filter testing.
/// </summary>
public class FilterTestEntity
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty; // Reserved keyword
    public decimal Total { get; set; }
    public bool IsActive { get; set; }
    public bool IsPremium { get; set; }
    public bool IsHidden { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public Guid CorrelationId { get; set; }
    public OrderStatus Status { get; set; } // Reserved keyword (enum)

    [DynamoDbAttribute("Status")]
    public string StatusString { get; set; } = string.Empty; // Reserved keyword (string version for testing)

    public string? FallbackId { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public FilterAddress Address { get; set; } = new();

    [DynamoDbIgnore]
    public string IgnoredProperty { get; set; } = string.Empty;
}

/// <summary>
/// Nested address type for filter testing.
/// </summary>
public class FilterAddress
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}

/// <summary>
/// Enum for testing enum conversion in filters.
/// </summary>
public enum OrderStatus
{
    Pending,
    Active,
    Completed,
    Expired
}

/// <summary>
/// Test entity for update expression tests.
/// </summary>
public class UpdateTestEntity
{
    public string Title { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty; // Reserved keyword
    public string Status { get; set; } = string.Empty; // Reserved keyword
    public decimal Price { get; set; }
    public int ViewCount { get; set; }
    public int Score { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
    public HashSet<string> EnabledFeatures { get; set; } = new();
    public string? TempFlag { get; set; }
    public Address? Address { get; set; }
    public TestPriority Priority { get; set; }

    [DynamoDbIgnore]
    public string InternalField { get; set; } = string.Empty;
}

/// <summary>
/// Test enum for conversion testing.
/// </summary>
public enum TestPriority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Test entity for key condition expression tests.
/// </summary>
public class KeyConditionTestEntity
{
    public string PK { get; set; } = string.Empty;
    public string SK { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty; // Reserved keyword
    public NestedAddress Address { get; set; } = new();

    [DynamoDbIgnore]
    public string InternalField { get; set; } = string.Empty;
}

/// <summary>
/// Entity with remapped attributes for testing attribute name resolution.
/// </summary>
public class RemappedEntity
{
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;

    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
}

/// <summary>
/// Nested address type for testing nested paths (should fail validation).
/// </summary>
public class NestedAddress
{
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// Entity with a List property for testing list-related update operations.
/// </summary>
public class MutR2EntityWithList
{
    public string Id { get; set; } = string.Empty;
    public List<string> Items { get; set; } = new();
}
