using WebApiExample.DTOs;

namespace WebApiExample.Repositories;

/// <summary>
/// Repository interface for Order entity operations using DynamoDB.
/// Demonstrates typed builder pattern with projection, filtering, key conditions, and direct result mapping.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Query orders for a specific customer with optional status filter and pagination.
    /// </summary>
    /// <param name="customerId">Customer identifier (part of partition key)</param>
    /// <param name="statusFilter">Optional filter for order status</param>
    /// <param name="limit">Maximum number of items to return (default 20)</param>
    /// <param name="paginationToken">Base64-encoded LastEvaluatedKey from previous page</param>
    /// <returns>Paged response containing OrderDto items and optional next token</returns>
    Task<PagedResponse<OrderDto>> QueryOrdersAsync(
        string customerId,
        string? statusFilter = null,
        int limit = 20,
        string? paginationToken = null);

    /// <summary>
    /// Fetch a single order by customer and order identifiers.
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="orderId">Order identifier</param>
    /// <returns>Full order details including nested address, or null if not found</returns>
    Task<OrderDetailDto?> GetOrderAsync(string customerId, string orderId);

    /// <summary>
    /// Create a new order with idempotency guard (fails if order already exists).
    /// </summary>
    /// <param name="request">Order creation request with all required fields</param>
    /// <exception cref="InvalidOperationException">Thrown if order with same keys already exists</exception>
    Task CreateOrderAsync(CreateOrderRequest request);

    /// <summary>
    /// Partially update an existing order with only the fields provided in the request.
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="orderId">Order identifier</param>
    /// <param name="request">Update request containing optional fields to change</param>
    Task UpdateOrderAsync(string customerId, string orderId, UpdateOrderRequest request);

    /// <summary>
    /// Delete an order with existence guard (fails if order doesn't exist).
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="orderId">Order identifier</param>
    /// <exception cref="KeyNotFoundException">Thrown if order doesn't exist</exception>
    Task DeleteOrderAsync(string customerId, string orderId);
}
