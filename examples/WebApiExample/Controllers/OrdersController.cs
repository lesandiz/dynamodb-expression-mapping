using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Mvc;
using WebApiExample.DTOs;
using WebApiExample.Repositories;

namespace WebApiExample.Controllers;

/// <summary>
/// REST API endpoints for Order management.
/// Demonstrates DynamoDb.ExpressionMapping with repository pattern, dynamic filtering, and pagination.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository _repository;

    public OrdersController(IOrderRepository repository)
        => _repository = repository;

    /// <summary>
    /// Query orders for a customer with optional status filter and pagination.
    /// </summary>
    /// <param name="customerId">Required: Customer ID to query orders for</param>
    /// <param name="status">Optional: Filter by order status (Shipped, Delivered, Processing, Cancelled)</param>
    /// <param name="limit">Optional: Maximum number of items to return (default: 20)</param>
    /// <param name="token">Optional: Pagination token from previous response</param>
    /// <returns>Paged list of order summaries</returns>
    [HttpGet]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string customerId,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 20,
        [FromQuery] string? token = null)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return BadRequest(new { error = "customerId is required" });

        if (limit <= 0 || limit > 100)
            return BadRequest(new { error = "limit must be between 1 and 100" });

        var result = await _repository.QueryOrdersAsync(
            customerId, status, limit, token);

        return Ok(result);
    }

    /// <summary>
    /// Get a single order with full details.
    /// </summary>
    /// <param name="customerId">Customer ID</param>
    /// <param name="orderId">Order ID</param>
    /// <returns>Full order details with nested city field</returns>
    [HttpGet("{customerId}/{orderId}")]
    public async Task<IActionResult> GetOrder(string customerId, string orderId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return BadRequest(new { error = "customerId is required" });

        if (string.IsNullOrWhiteSpace(orderId))
            return BadRequest(new { error = "orderId is required" });

        var order = await _repository.GetOrderAsync(customerId, orderId);

        if (order is null)
            return NotFound(new { error = $"Order {orderId} not found for customer {customerId}" });

        return Ok(order);
    }

    /// <summary>
    /// Create a new order.
    /// </summary>
    /// <param name="request">Order creation details</param>
    /// <returns>201 Created on success, 409 Conflict if order already exists</returns>
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _repository.CreateOrderAsync(request);
            return CreatedAtAction(
                nameof(GetOrder),
                new { customerId = request.CustomerId, orderId = request.OrderId },
                request);
        }
        catch (ConditionalCheckFailedException)
        {
            return Conflict(new
            {
                error = $"Order {request.OrderId} already exists for customer {request.CustomerId}"
            });
        }
    }

    /// <summary>
    /// Update an existing order (partial update).
    /// </summary>
    /// <param name="customerId">Customer ID</param>
    /// <param name="orderId">Order ID</param>
    /// <param name="request">Fields to update (only non-null fields are updated)</param>
    /// <returns>204 No Content on success, 404 if order not found</returns>
    [HttpPut("{customerId}/{orderId}")]
    public async Task<IActionResult> UpdateOrder(
        string customerId,
        string orderId,
        [FromBody] UpdateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return BadRequest(new { error = "customerId is required" });

        if (string.IsNullOrWhiteSpace(orderId))
            return BadRequest(new { error = "orderId is required" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _repository.UpdateOrderAsync(customerId, orderId, request);
            return NoContent();
        }
        catch (ConditionalCheckFailedException)
        {
            return NotFound(new { error = $"Order {orderId} not found for customer {customerId}" });
        }
    }

    /// <summary>
    /// Delete an order.
    /// </summary>
    /// <param name="customerId">Customer ID</param>
    /// <param name="orderId">Order ID</param>
    /// <returns>204 No Content on success, 404 if order doesn't exist</returns>
    [HttpDelete("{customerId}/{orderId}")]
    public async Task<IActionResult> DeleteOrder(string customerId, string orderId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return BadRequest(new { error = "customerId is required" });

        if (string.IsNullOrWhiteSpace(orderId))
            return BadRequest(new { error = "orderId is required" });

        try
        {
            await _repository.DeleteOrderAsync(customerId, orderId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
