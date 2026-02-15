namespace WebApiExample.DTOs;

/// <summary>
/// Simplified order list view for query responses.
/// </summary>
public class OrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
