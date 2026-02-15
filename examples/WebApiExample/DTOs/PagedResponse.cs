namespace WebApiExample.DTOs;

/// <summary>
/// Generic wrapper for paginated list responses.
/// NextToken is Base64-encoded LastEvaluatedKey.
/// </summary>
/// <typeparam name="T">The type of items in the response.</typeparam>
public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public string? NextToken { get; set; }
}
