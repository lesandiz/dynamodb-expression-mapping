namespace DynamoDb.ExpressionMapping.Tests.Fixtures;

/// <summary>
/// Test entity for unit tests.
/// </summary>
public class TestEntity
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Address? Address { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();

    // Reserved keyword for testing
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public HashSet<string> EnabledFeatures { get; set; } = new();
    public int Score { get; set; }
}

/// <summary>
/// Nested address type for testing nested paths.
/// </summary>
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public Country? Country { get; set; }
}

/// <summary>
/// Deeply nested type for three-level path testing.
/// </summary>
public class Country
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Target DTO for object initializer tests.
/// </summary>
public class OrderSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

