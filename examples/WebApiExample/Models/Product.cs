using DynamoDb.ExpressionMapping.Attributes;

namespace WebApiExample.Models;

public class Product
{
    public string PK { get; set; } = default!;
    public string SK { get; set; } = default!;

    [DynamoDbAttribute("product_id")]
    public string ProductId { get; set; } = default!;

    public string Name { get; set; } = default!;            // Reserved keyword
    public string Category { get; set; } = default!;
    public Money Price { get; set; } = default!;
    public int StockCount { get; set; }
    public bool IsActive { get; set; }
}
