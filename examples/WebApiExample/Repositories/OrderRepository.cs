using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Mapping.Converters;
using DynamoDb.ExpressionMapping.ResultMapping;
using WebApiExample.DTOs;
using WebApiExample.Infrastructure;
using WebApiExample.Models;

namespace WebApiExample.Repositories;

/// <summary>
/// Repository for Order entity operations using DynamoDB.
/// Demonstrates typed builder pattern with projection, filtering, key conditions, and direct result mapping.
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly IAmazonDynamoDB _client;
    private readonly IProjectionBuilder<Order> _projectionBuilder;
    private readonly IFilterExpressionBuilder<Order> _filterBuilder;
    private readonly IConditionExpressionBuilder<Order> _conditionBuilder;
    private readonly IKeyConditionExpressionBuilder<Order> _keyConditionBuilder;
    private readonly IDirectResultMapper<Order> _resultMapper;
    private readonly IAttributeValueConverterRegistry _converterRegistry;
    private readonly IAttributeNameResolverFactory _resolverFactory;

    public OrderRepository(
        IAmazonDynamoDB client,
        IProjectionBuilder<Order> projectionBuilder,
        IFilterExpressionBuilder<Order> filterBuilder,
        IConditionExpressionBuilder<Order> conditionBuilder,
        IKeyConditionExpressionBuilder<Order> keyConditionBuilder,
        IDirectResultMapper<Order> resultMapper,
        IAttributeValueConverterRegistry converterRegistry,
        IAttributeNameResolverFactory resolverFactory)
    {
        _client = client;
        _projectionBuilder = projectionBuilder;
        _filterBuilder = filterBuilder;
        _conditionBuilder = conditionBuilder;
        _keyConditionBuilder = keyConditionBuilder;
        _resultMapper = resultMapper;
        _converterRegistry = converterRegistry;
        _resolverFactory = resolverFactory;
    }

    /// <inheritdoc />
    public async Task<PagedResponse<OrderDto>> QueryOrdersAsync(
        string customerId,
        string? statusFilter = null,
        int limit = 20,
        string? paginationToken = null)
    {
        // Compile mapper once (idempotent, cached internally)
        var mapper = _resultMapper.CreateMapper(o => new OrderDto
        {
            OrderId = o.OrderId,
            Name = o.Name,
            Status = o.Status,
            Quantity = o.Quantity
        });

        var request = new QueryRequest { TableName = TableDefinitions.TableName, Limit = limit }
            .WithKeyCondition(_keyConditionBuilder,
                b => b.WithPartitionKey(o => o.PK, TableDefinitions.KeyPatterns.Order.PK(customerId))
                      .WithSortKeyBeginsWith(o => o.SK, TableDefinitions.KeyPatterns.Order.SKPrefix))
            .WithProjection(_projectionBuilder,
                o => new { o.OrderId, o.Name, o.Status, o.Quantity });

        // Apply optional status filter dynamically
        if (!string.IsNullOrEmpty(statusFilter))
        {
            // Build filter independently and apply
            var filterResult = _filterBuilder.BuildFilter(o => o.Status == statusFilter);
            request.FilterExpression = filterResult.Expression;
            // Merge names/values (RequestMergeHelpers handles collision detection)
            foreach (var name in filterResult.ExpressionAttributeNames)
                request.ExpressionAttributeNames[name.Key] = name.Value;
            foreach (var val in filterResult.ExpressionAttributeValues)
                request.ExpressionAttributeValues[val.Key] = val.Value;
        }

        // Apply pagination token
        if (!string.IsNullOrEmpty(paginationToken))
        {
            request.ExclusiveStartKey = DecodePaginationToken(paginationToken);
        }

        var response = await _client.QueryAsync(request);

        return new PagedResponse<OrderDto>
        {
            Items = response.Items.Select(item => mapper(item)).ToList(),
            NextToken = response.LastEvaluatedKey?.Count > 0
                ? EncodePaginationToken(response.LastEvaluatedKey)
                : null
        };
    }

    /// <inheritdoc />
    public async Task<OrderDetailDto?> GetOrderAsync(string customerId, string orderId)
    {
        var mapper = _resultMapper.CreateMapper(o => new OrderDetailDto
        {
            OrderId = o.OrderId,
            CustomerId = o.CustomerId,
            Name = o.Name,
            Status = o.Status,
            Quantity = o.Quantity,
            City = o.ShippingAddress.City,
            Notes = o.Notes,
            CreatedAt = o.CreatedAt
        });

        var request = new GetItemRequest
        {
            TableName = TableDefinitions.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [TableDefinitions.PartitionKey] = new AttributeValue { S = TableDefinitions.KeyPatterns.Order.PK(customerId) },
                [TableDefinitions.SortKey] = new AttributeValue { S = TableDefinitions.KeyPatterns.Order.SK(orderId) }
            }
        }
        .WithProjection(_projectionBuilder,
            o => new { o.OrderId, o.CustomerId, o.Name, o.Status,
                       o.Quantity, o.ShippingAddress.City, o.Notes, o.CreatedAt });

        var response = await _client.GetItemAsync(request);

        if (!response.IsItemSet)
            return null;

        return mapper(response.Item);
    }

    /// <inheritdoc />
    public async Task CreateOrderAsync(CreateOrderRequest request)
    {
        var item = MapToItem(request);

        var putRequest = new PutItemRequest
        {
            TableName = TableDefinitions.TableName,
            Item = item
        }
        .WithCondition(_conditionBuilder,
            o => o.PK == null); // attribute_not_exists(PK) — prevents overwrite

        try
        {
            await _client.PutItemAsync(putRequest);
        }
        catch (ConditionalCheckFailedException)
        {
            throw new InvalidOperationException(
                $"Order {request.OrderId} already exists for customer {request.CustomerId}.");
        }
    }

    /// <inheritdoc />
    public async Task UpdateOrderAsync(string customerId, string orderId, UpdateOrderRequest request)
    {
        // Build update dynamically based on which fields are present
        var updateBuilder = new UpdateExpressionBuilder<Order>(
            _resolverFactory, _converterRegistry);

        if (request.Status is not null)
            updateBuilder.Set(o => o.Status, request.Status);

        if (request.Quantity.HasValue)
            updateBuilder.Set(o => o.Quantity, request.Quantity.Value);

        if (request.Notes is not null)
            updateBuilder.Set(o => o.Notes, request.Notes);

        var updateResult = updateBuilder.Build();

        if (updateResult.IsEmpty)
            return; // Nothing to update

        var updateRequest = new UpdateItemRequest
        {
            TableName = TableDefinitions.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [TableDefinitions.PartitionKey] = new AttributeValue { S = TableDefinitions.KeyPatterns.Order.PK(customerId) },
                [TableDefinitions.SortKey] = new AttributeValue { S = TableDefinitions.KeyPatterns.Order.SK(orderId) }
            }
        }
        .WithUpdate(updateResult);

        await _client.UpdateItemAsync(updateRequest);
    }

    /// <inheritdoc />
    public async Task DeleteOrderAsync(string customerId, string orderId)
    {
        var deleteRequest = new DeleteItemRequest
        {
            TableName = TableDefinitions.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [TableDefinitions.PartitionKey] = new AttributeValue { S = TableDefinitions.KeyPatterns.Order.PK(customerId) },
                [TableDefinitions.SortKey] = new AttributeValue { S = TableDefinitions.KeyPatterns.Order.SK(orderId) }
            }
        }
        .WithCondition(_conditionBuilder,
            o => o.PK != null); // attribute_exists(PK) — only delete if exists

        try
        {
            await _client.DeleteItemAsync(deleteRequest);
        }
        catch (ConditionalCheckFailedException)
        {
            throw new KeyNotFoundException(
                $"Order {orderId} not found for customer {customerId}.");
        }
    }

    // ==================== PAGINATION HELPERS ====================

    /// <summary>
    /// Encode LastEvaluatedKey to Base64 token for pagination.
    /// </summary>
    private static string EncodePaginationToken(Dictionary<string, AttributeValue> lastEvaluatedKey)
    {
        // Serialize to JSON, then Base64 encode
        var json = System.Text.Json.JsonSerializer.Serialize(
            lastEvaluatedKey.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.S ?? kvp.Value.N ?? string.Empty));
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Decode Base64 pagination token back to ExclusiveStartKey.
    /// </summary>
    private static Dictionary<string, AttributeValue> DecodePaginationToken(string token)
    {
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        return dict.ToDictionary(
            kvp => kvp.Key,
            kvp => new AttributeValue { S = kvp.Value });
    }

    // ==================== MAPPING HELPERS ====================

    /// <summary>
    /// Maps CreateOrderRequest DTO to DynamoDB item dictionary.
    /// </summary>
    private Dictionary<string, AttributeValue> MapToItem(CreateOrderRequest request)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [TableDefinitions.PartitionKey] = new AttributeValue { S = TableDefinitions.KeyPatterns.Order.PK(request.CustomerId) },
            [TableDefinitions.SortKey] = new AttributeValue { S = TableDefinitions.KeyPatterns.Order.SK(request.OrderId) },
            ["order_id"] = new AttributeValue { S = request.OrderId },
            ["customer_id"] = new AttributeValue { S = request.CustomerId },
            ["Name"] = new AttributeValue { S = request.Name },
            ["Status"] = new AttributeValue { S = request.Status },
            ["Quantity"] = new AttributeValue { N = request.Quantity.ToString() },
            ["CreatedAt"] = new AttributeValue { S = DateTime.UtcNow.ToString("o") }
        };

        // Use MoneyConverter via registry for Total
        var moneyConverter = _converterRegistry.GetConverter<Money>();
        item["Total"] = moneyConverter.ToAttributeValue(new Money(request.TotalAmount, request.TotalCurrency));

        // Map Address
        item["ShippingAddress"] = new AttributeValue
        {
            M = new Dictionary<string, AttributeValue>
            {
                ["Street"] = new AttributeValue { S = request.Street },
                ["City"] = new AttributeValue { S = request.City },
                ["PostCode"] = new AttributeValue { S = request.PostCode }
            }
        };

        // Map Tags
        if (request.Tags.Count > 0)
        {
            item["Tags"] = new AttributeValue { L = request.Tags.Select(t => new AttributeValue { S = t }).ToList() };
        }

        return item;
    }
}
