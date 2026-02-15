namespace WebApiExample.DTOs;

/// <summary>
/// Customer profile with flattened address city.
/// </summary>
public class CustomerDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}
