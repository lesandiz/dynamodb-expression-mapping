namespace ConsoleQuickStart;

public class Order
{
    public string PK { get; set; } = default!;          // CUSTOMER#<id>
    public string SK { get; set; } = default!;          // ORDER#<id>
    public string Name { get; set; } = default!;        // Reserved keyword in DynamoDB
    public string Status { get; set; } = default!;      // Reserved keyword in DynamoDB
    public Address ShippingAddress { get; set; } = default!;
    public Money Total { get; set; } = default!;
    public List<string> Tags { get; set; } = new();
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Address
{
    public string Street { get; set; } = default!;
    public string City { get; set; } = default!;
    public string PostCode { get; set; } = default!;
}

public record Money(decimal Amount, string Currency);

public class OrderSummary
{
    public string OrderId { get; set; } = default!;
    public string CustomerName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string City { get; set; } = default!;
}
