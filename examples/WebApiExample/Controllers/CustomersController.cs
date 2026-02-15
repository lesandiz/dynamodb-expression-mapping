using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using DynamoDb.ExpressionMapping.ResultMapping;
using Microsoft.AspNetCore.Mvc;
using WebApiExample.DTOs;
using WebApiExample.Infrastructure;
using WebApiExample.Models;

namespace WebApiExample.Controllers;

/// <summary>
/// Customer profile endpoint demonstrating projection with nested path (Address.City).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly IProjectionBuilder<Customer> _projectionBuilder;
    private readonly IDirectResultMapper<Customer> _resultMapper;
    private readonly IAmazonDynamoDB _client;

    public CustomersController(
        IProjectionBuilder<Customer> projectionBuilder,
        IDirectResultMapper<Customer> resultMapper,
        IAmazonDynamoDB client)
    {
        _projectionBuilder = projectionBuilder;
        _resultMapper = resultMapper;
        _client = client;
    }

    /// <summary>
    /// Get customer profile by ID with nested path projection (Address.City).
    /// </summary>
    /// <param name="id">Customer ID</param>
    /// <returns>Customer profile with flattened city field</returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetCustomer(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { error = "Customer ID is required" });

        // 1. Create mapper for CustomerDto with nested path (Address.City -> City)
        var mapper = _resultMapper.CreateMapper(c => new CustomerDto
        {
            CustomerId = c.CustomerId,
            Name = c.Name,
            Email = c.Email,
            City = c.Address.City,  // Nested path: Address.City flattened to City
            JoinedAt = c.JoinedAt
        });

        // 2. Build GetItem request with projection
        var request = new GetItemRequest
        {
            TableName = TableDefinitions.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [TableDefinitions.PartitionKey] = new AttributeValue { S = TableDefinitions.KeyPatterns.Customer.PK(id) },
                [TableDefinitions.SortKey] = new AttributeValue { S = TableDefinitions.KeyPatterns.Customer.ProfileSK }
            }
        }.WithProjection(_projectionBuilder,
            c => new { c.CustomerId, c.Name, c.Email, c.Address.City, c.JoinedAt });

        // 3. Execute query
        var response = await _client.GetItemAsync(request);

        // 4. Handle 404
        if (!response.IsItemSet)
            return NotFound();

        // 5. Map and return
        return Ok(mapper(response.Item));
    }
}
