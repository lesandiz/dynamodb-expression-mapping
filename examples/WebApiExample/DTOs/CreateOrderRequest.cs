namespace WebApiExample.DTOs;

/// <summary>
/// Request to create a new order with full address and flattened Money components.
/// </summary>
public class CreateOrderRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Processing";
    public int Quantity { get; set; } = 1;
    public decimal TotalAmount { get; set; }
    public string TotalCurrency { get; set; } = "USD";
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostCode { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}
