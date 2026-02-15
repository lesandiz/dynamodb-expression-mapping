using DynamoDb.ExpressionMapping.Attributes;

namespace WebApiExample.Models;

public class Order
{
    public string PK { get; set; } = default!;
    public string SK { get; set; } = default!;

    [DynamoDbAttribute("order_id")]
    public string OrderId { get; set; } = default!;

    [DynamoDbAttribute("customer_id")]
    public string CustomerId { get; set; } = default!;

    public string Name { get; set; } = default!;            // Reserved keyword
    public string Status { get; set; } = default!;          // Reserved keyword
    public Money Total { get; set; } = default!;
    public int Quantity { get; set; }
    public Address ShippingAddress { get; set; } = default!;
    public List<string> Tags { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
