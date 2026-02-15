namespace WebApiExample.DTOs;

/// <summary>
/// Product list/filter view for catalog endpoints.
/// </summary>
public class ProductDto
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int StockCount { get; set; }
    public bool IsActive { get; set; }
}
