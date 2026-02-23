namespace DynamoDb.ExpressionMapping.Benchmarks.Fixtures;

/// <summary>
/// Primary benchmark entity with diverse property types.
/// Includes reserved keywords (Name, Status), nested types, collections,
/// nullable types, and enums to exercise all library subsystems.
/// </summary>
public class BenchmarkOrder
{
    public string PK { get; set; } = default!;
    public string SK { get; set; } = default!;
    public string OrderId { get; set; } = default!;
    public string CustomerId { get; set; } = default!;

    // Reserved keywords — forces alias generation
    public string Name { get; set; } = default!;
    public string Status { get; set; } = default!;

    public decimal TotalAmount { get; set; }
    public int Quantity { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }

    // Enum
    public OrderPriority Priority { get; set; }

    // Nested type
    public BenchmarkAddress? Address { get; set; }

    // Collections
    public List<string> Tags { get; set; } = new();
    public HashSet<string> Features { get; set; } = new();
    public Dictionary<string, string>? Metadata { get; set; }

    // Extra properties for "twenty properties" benchmarks
    public string Prop1 { get; set; } = default!;
    public string Prop2 { get; set; } = default!;
    public string Prop3 { get; set; } = default!;
    public string Prop4 { get; set; } = default!;
    public string Prop5 { get; set; } = default!;
    public string Prop6 { get; set; } = default!;
    public string Prop7 { get; set; } = default!;
    public string Prop8 { get; set; } = default!;
    public int Score { get; set; }
}

public class BenchmarkAddress
{
    public string Street { get; set; } = default!;
    public string City { get; set; } = default!;
    public string ZipCode { get; set; } = default!;
    public BenchmarkCountry? Country { get; set; }
}

public class BenchmarkCountry
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
}

public enum OrderPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

/// <summary>
/// Target DTO for projection/mapping benchmarks.
/// </summary>
public class OrderSummary
{
    public string OrderId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Status { get; set; } = default!;
    public decimal TotalAmount { get; set; }
    public string City { get; set; } = default!;
}

/// <summary>
/// Larger projection target for 10-property mapping benchmarks.
/// </summary>
public class OrderDetail
{
    public string OrderId { get; set; } = default!;
    public string CustomerId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Status { get; set; } = default!;
    public decimal TotalAmount { get; set; }
    public int Quantity { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Score { get; set; }
    public string Prop1 { get; set; } = default!;
}

/// <summary>
/// Record type for record mapping benchmarks.
/// </summary>
public record OrderRecord(string OrderId, string Name, decimal TotalAmount);
