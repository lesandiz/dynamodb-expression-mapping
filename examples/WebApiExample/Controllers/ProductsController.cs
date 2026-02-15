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
/// Products catalog endpoint demonstrating dynamic filter composition.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProjectionBuilder<Product> _projectionBuilder;
    private readonly IFilterExpressionBuilder<Product> _filterBuilder;
    private readonly IDirectResultMapper<Product> _resultMapper;
    private readonly IAmazonDynamoDB _client;

    public ProductsController(
        IProjectionBuilder<Product> projectionBuilder,
        IFilterExpressionBuilder<Product> filterBuilder,
        IDirectResultMapper<Product> resultMapper,
        IAmazonDynamoDB client)
    {
        _projectionBuilder = projectionBuilder;
        _filterBuilder = filterBuilder;
        _resultMapper = resultMapper;
        _client = client;
    }

    /// <summary>
    /// Get products with optional category and active status filters.
    /// Demonstrates dynamic filter composition with And() re-aliasing.
    /// </summary>
    /// <param name="category">Optional category filter (e.g., "Electronics")</param>
    /// <param name="activeOnly">When true, returns only active products (default: false)</param>
    /// <returns>List of products matching the filter criteria</returns>
    [HttpGet]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? category = null,
        [FromQuery] bool activeOnly = false)
    {
        // 1. Create mapper for ProductDto
        var mapper = _resultMapper.CreateMapper(p => new ProductDto
        {
            ProductId = p.ProductId,
            Name = p.Name,
            Category = p.Category,
            StockCount = p.StockCount,
            IsActive = p.IsActive
        });

        // 2. Build base scan request with projection
        var request = new ScanRequest { TableName = TableDefinitions.TableName }
            .WithProjection(_projectionBuilder,
                p => new { p.ProductId, p.Name, p.Category, p.StockCount, p.IsActive });

        // 3. Build and compose filters dynamically
        FilterExpressionResult? composedFilter = null;

        if (!string.IsNullOrEmpty(category))
        {
            var catFilter = _filterBuilder.BuildFilter(p => p.Category == category);
            composedFilter = catFilter;
        }

        if (activeOnly)
        {
            var activeFilter = _filterBuilder.BuildFilter(p => p.IsActive == true);
            composedFilter = composedFilter is not null
                ? FilterExpressionResult.And(composedFilter, activeFilter)
                : activeFilter;
        }

        // 4. Apply composed filter to request (if any filters were specified)
        if (composedFilter is not null)
        {
            request.FilterExpression = composedFilter.Expression;

            // Initialize dictionaries if null (projection may have already populated them)
            request.ExpressionAttributeNames ??= new Dictionary<string, string>();
            request.ExpressionAttributeValues ??= new Dictionary<string, AttributeValue>();

            // Merge filter attributes with existing attributes from projection
            foreach (var kvp in composedFilter.ExpressionAttributeNames)
            {
                request.ExpressionAttributeNames[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in composedFilter.ExpressionAttributeValues)
            {
                request.ExpressionAttributeValues[kvp.Key] = kvp.Value;
            }
        }

        // 5. Execute scan and map results
        var response = await _client.ScanAsync(request);
        var products = response.Items.Select(item => mapper(item)).ToList();

        return Ok(products);
    }
}
