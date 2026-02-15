namespace WebApiExample.DTOs;

/// <summary>
/// Request for selective order updates. All properties are optional.
/// </summary>
public class UpdateOrderRequest
{
    public string? Status { get; set; }
    public int? Quantity { get; set; }
    public string? Notes { get; set; }
}
