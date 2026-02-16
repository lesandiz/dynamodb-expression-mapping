namespace DynamoDb.ExpressionMapping.SoakTests.Workloads;

/// <summary>
/// Entity used across soak test workloads.
/// Includes diverse property types to exercise all library subsystems.
/// </summary>
public class SoakOrder
{
    public string PK { get; set; } = default!;          // Partition key: CUSTOMER#<id>
    public string SK { get; set; } = default!;          // Sort key: ORDER#<id>
    public string OrderId { get; set; } = default!;
    public string CustomerId { get; set; } = default!;
    public string Name { get; set; } = default!;        // Reserved keyword
    public string Status { get; set; } = default!;      // Reserved keyword
    public decimal TotalAmount { get; set; }
    public string TotalCurrency { get; set; } = default!;
    public int Quantity { get; set; }
    public string ShippingStreet { get; set; } = default!;
    public string ShippingCity { get; set; } = default!;
    public string ShippingPostCode { get; set; } = default!;
    public List<string> Tags { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public bool IsGift { get; set; }
    public OrderPriority Priority { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public enum OrderPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

/// <summary>
/// Simplified projection type for result mapping tests.
/// </summary>
public class OrderSummary
{
    public string OrderId { get; set; } = default!;
    public string CustomerName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public decimal TotalAmount { get; set; }
    public string ShippingCity { get; set; } = default!;
}

/// <summary>
/// Another projection type for variety.
/// </summary>
public class OrderListItem
{
    public string OrderId { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}
