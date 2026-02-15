namespace WebApiExample.DTOs;

/// <summary>
/// Full order detail with shipping city and creation timestamp.
/// </summary>
public class OrderDetailDto
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string City { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
