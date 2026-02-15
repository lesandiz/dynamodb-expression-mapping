using DynamoDb.ExpressionMapping.Attributes;

namespace WebApiExample.Models;

public class Customer
{
    public string PK { get; set; } = default!;
    public string SK { get; set; } = default!;

    [DynamoDbAttribute("customer_id")]
    public string CustomerId { get; set; } = default!;

    public string Name { get; set; } = default!;            // Reserved keyword
    public string Email { get; set; } = default!;
    public Address Address { get; set; } = default!;
    public DateTime JoinedAt { get; set; }
}
