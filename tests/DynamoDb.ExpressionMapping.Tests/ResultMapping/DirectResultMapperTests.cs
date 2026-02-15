using System.Diagnostics;
using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using Bogus;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ResultMapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using NSubstitute;

namespace DynamoDb.ExpressionMapping.Tests.ResultMapping;

/// <summary>
/// Comprehensive unit tests for DirectResultMapper&lt;TSource&gt; based on Spec 12 test plan.
/// Tests all mapping strategies, type conversions, null handling, nested attributes,
/// performance, custom converters, and validation.
/// </summary>
public class DirectResultMapperTests
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;
    private readonly DirectResultMapper<TestEntity> _mapper;
    private readonly Faker _faker = new();

    public DirectResultMapperTests()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
        _mapper = new DirectResultMapper<TestEntity>(
            _resolverFactory,
            _converterRegistry,
            NullExpressionCache.Instance);
    }

    #region Mapping Strategies

    [Fact]
    public void CreateMapper_SingleProperty_MapsDirectly()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-123" }
        };

        // Act
        var mapper = _mapper.CreateMapper(e => e.OrderId);
        var result = mapper(attributes);

        // Assert
        result.Should().Be("order-123");
    }

    [Fact]
    public void CreateMapper_AnonymousType_ConstructsViaConstructor()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-456" },
            ["Price"] = new() { N = "99.99" }
        };

        // Act
        var mapper = _mapper.CreateMapper(e => new { e.OrderId, e.Price });
        var result = mapper(attributes);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be("order-456");
        result.Price.Should().Be(99.99m);
    }

    [Fact]
    public void CreateMapper_NamedType_ConstructsViaPropertySetters()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-789" },
            ["Name"] = new() { S = "Test Order" },
            ["Price"] = new() { N = "149.50" }
        };

        // Act
        var mapper = _mapper.CreateMapper(e => new OrderSummary
        {
            Id = e.OrderId,
            Name = e.Name,
            Price = e.Price
        });
        var result = mapper(attributes);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("order-789");
        result.Name.Should().Be("Test Order");
        result.Price.Should().Be(149.50m);
    }

    [Fact]
    public void CreateMapper_Record_ConstructsViaConstructor()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-record-1" },
            ["Status"] = new() { S = "Active" }
        };

        // Act
        var mapper = _mapper.CreateMapper(e => new OrderRecord(e.OrderId, e.Status));
        var result = mapper(attributes);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("order-record-1");
        result.Status.Should().Be("Active");
    }

    [Fact]
    public void CreateMapper_ParameterisedConstructor_ConstructsViaConstructorArgs()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["CustomerId"] = new() { S = "cust-123" },
            ["Name"] = new() { S = "Customer Name" }
        };

        // Act
        var mapper = _mapper.CreateMapper(e => new CustomerDto(e.CustomerId, e.Name));
        var result = mapper(attributes);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().Be("cust-123");
        result.Name.Should().Be("Customer Name");
    }

    [Fact]
    public void CreateMapper_Identity_DelegatesToFallback()
    {
        // Arrange
        var fallbackCalled = false;
        var fallbackMapper = new Func<Dictionary<string, AttributeValue>, object>(attrs =>
        {
            fallbackCalled = true;
            return new TestEntity { OrderId = "fallback" };
        });

        var mapperWithFallback = new DirectResultMapper<TestEntity>(
            _resolverFactory,
            _converterRegistry,
            NullExpressionCache.Instance,
            fallbackMapper);

        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-identity" }
        };

        // Act
        var mapper = mapperWithFallback.CreateMapper(e => e);
        var result = mapper(attributes);

        // Assert
        fallbackCalled.Should().BeTrue();
        result.Should().BeOfType<TestEntity>();
        ((TestEntity)result).OrderId.Should().Be("fallback");
    }

    #endregion

    #region Type Conversion - All Built-in Types

    [Fact]
    public void Map_StringAttribute_MapsToString()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["Title"] = new() { S = "Test Title" }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.Title);

        // Assert
        result.Should().Be("Test Title");
    }

    [Fact]
    public void Map_BoolAttribute_MapsToBool()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["IsActive"] = new() { BOOL = true }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.IsActive);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Map_NumberAttribute_MapsToDecimal()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["Price"] = new() { N = "299.99" }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.Price);

        // Assert
        result.Should().Be(299.99m);
    }

    [Fact]
    public void Map_DateTimeAttribute_MapsToDateTime()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["StartDate"] = new() { S = dateTime.ToString("O") }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.StartDate);

        // Assert
        result.Should().Be(dateTime);
    }

    [Fact]
    public void Map_NullableAttribute_MapsToNullable()
    {
        // Arrange
        var endDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["EndDate"] = new() { S = endDate.ToString("O") }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.EndDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(endDate);
    }

    [Fact]
    public void Map_ListAttribute_MapsToArray()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["Tags"] = new()
            {
                L = new List<AttributeValue>
                {
                    new() { S = "tag1" },
                    new() { S = "tag2" },
                    new() { S = "tag3" }
                }
            }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.Tags);

        // Assert
        result.Should().NotBeNull();
        result.Should().Equal("tag1", "tag2", "tag3");
    }

    #endregion

    #region Missing / Null Attributes

    [Fact]
    public void Map_MissingAttribute_ReturnsDefault()
    {
        // Arrange - no "Title" attribute in dictionary
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-123" }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.Title);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Map_NullAttribute_ReturnsDefault()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["Title"] = new() { NULL = true }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.Title);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Map_MissingNullableAttribute_ReturnsNull()
    {
        // Arrange - no "EndDate" attribute
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-123" }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.EndDate);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Map_NullNullableAttribute_ReturnsNull()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["EndDate"] = new() { NULL = true }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.EndDate);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Map_WrongDynamoDbType_ReturnsDefault()
    {
        // Arrange - Price expects N (number) but gets S (string)
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["Price"] = new() { S = "not-a-number" }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.Price);

        // Assert
        result.Should().Be(0m);
    }

    #endregion

    #region Nested Attributes

    [Fact]
    public void Map_NestedMapAttribute_MapsCorrectly()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["Address"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["City"] = new() { S = "London" },
                    ["ZipCode"] = new() { S = "SW1A 1AA" }
                }
            }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.Address!.City);

        // Assert
        result.Should().Be("London");
    }

    [Fact]
    public void Map_NestedMapAttribute_MultipleProperties_MapsCorrectly()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["Address"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["Street"] = new() { S = "10 Downing Street" },
                    ["City"] = new() { S = "London" },
                    ["ZipCode"] = new() { S = "SW1A 1AA" }
                }
            }
        };

        // Act
        var result = _mapper.Map(attributes, e => new
        {
            Street = e.Address!.Street,
            City = e.Address!.City,
            PostalCode = e.Address!.ZipCode
        });

        // Assert
        result.Should().NotBeNull();
        result.Street.Should().Be("10 Downing Street");
        result.City.Should().Be("London");
        result.PostalCode.Should().Be("SW1A 1AA");
    }

    [Fact]
    public void Map_NavigateToLeaf_MissingIntermediate_ReturnsDefault()
    {
        // Arrange - no "Address" attribute
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-123" }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.Address!.City);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Map_NavigateToLeaf_IntermediateNotMap_ReturnsDefault()
    {
        // Arrange - "Address" exists but is not a Map
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["Address"] = new() { S = "not-a-map" }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.Address!.City);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Map_DeeplyNestedPath_ThreeLevels_MapsCorrectly()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["Address"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["Country"] = new()
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["Name"] = new() { S = "United Kingdom" },
                            ["Code"] = new() { S = "GB" }
                        }
                    }
                }
            }
        };

        // Act
        var result = _mapper.Map(attributes, e => e.Address!.Country!.Name);

        // Assert
        result.Should().Be("United Kingdom");
    }

    #endregion

    #region One-shot Map Method

    [Fact]
    public void Map_OneShot_ReturnsMappedResult()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-one-shot" },
            ["Price"] = new() { N = "199.99" }
        };

        // Act
        var result = _mapper.Map(attributes, e => new { e.OrderId, e.Price });

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be("order-one-shot");
        result.Price.Should().Be(199.99m);
    }

    [Fact]
    public void Map_OneShot_UsesCachedMapperInternally()
    {
        // Arrange
        var attributes1 = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-1" }
        };

        var attributes2 = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-2" }
        };

        // Act - call Map twice with same selector
        var result1 = _mapper.Map(attributes1, e => e.OrderId);
        var result2 = _mapper.Map(attributes2, e => e.OrderId);

        // Assert - both should work and use cached mapper
        result1.Should().Be("order-1");
        result2.Should().Be("order-2");
    }

    #endregion

    #region Performance - Caching

    [Fact]
    public void CreateMapper_ReturnsReusableDelegate()
    {
        // Arrange
        var attributes1 = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-a" },
            ["Price"] = new() { N = "10.00" }
        };

        var attributes2 = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "order-b" },
            ["Price"] = new() { N = "20.00" }
        };

        // Act
        var mapper = _mapper.CreateMapper(e => new { e.OrderId, e.Price });
        var result1 = mapper(attributes1);
        var result2 = mapper(attributes2);

        // Assert
        result1.OrderId.Should().Be("order-a");
        result1.Price.Should().Be(10.00m);
        result2.OrderId.Should().Be("order-b");
        result2.Price.Should().Be(20.00m);
    }

    [Fact]
    public void CachedMapper_ReturnsSameDelegate()
    {
        // Arrange
        Expression<Func<TestEntity, object>> selector = e => new { e.OrderId, e.Price };

        // Act
        var mapper1 = _mapper.CreateMapper(selector);
        var mapper2 = _mapper.CreateMapper(selector);

        // Assert
        mapper1.Should().BeSameAs(mapper2, "cached mappers should be the same instance");
    }

    [Fact]
    public void CreateMapper_Performance_CompiledDelegateIsFast()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "perf-test" },
            ["CustomerId"] = new() { S = "customer-123" },
            ["Title"] = new() { S = "Performance Test" },
            ["Price"] = new() { N = "299.99" },
            ["IsActive"] = new() { BOOL = true }
        };

        var mapper = _mapper.CreateMapper(e => new
        {
            e.OrderId,
            e.CustomerId,
            e.Title,
            e.Price,
            e.IsActive
        });

        // Act - run 10,000 iterations
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
        {
            _ = mapper(attributes);
        }
        sw.Stop();

        // Assert - should complete in reasonable time (< 100ms for 10k iterations)
        sw.ElapsedMilliseconds.Should().BeLessThan(100,
            "compiled delegate should execute efficiently");
    }

    #endregion

    #region Custom Converters

    [Fact]
    public void CreateMapper_RegisteredConverter_UsedForType()
    {
        // Arrange - create custom registry with Money converter
        var customRegistry = AttributeValueConverterRegistry.Default.Clone();
        customRegistry.Register(new MoneyConverter());

        var mapperWithCustomConverter = new DirectResultMapper<EntityWithMoney>(
            _resolverFactory,
            customRegistry);

        var attributes = new Dictionary<string, AttributeValue>
        {
            ["Amount"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["Amount"] = new() { N = "100.50" },
                    ["Currency"] = new() { S = "USD" }
                }
            }
        };

        // Act
        var mapper = mapperWithCustomConverter.CreateMapper(e => e.Amount);
        var result = mapper(attributes);

        // Assert
        result.Should().NotBeNull();
        result.Amount.Should().Be(100.50m);
        result.Currency.Should().Be("USD");
    }

    #endregion

    #region Validation

    [Fact]
    public void CreateMapper_NullSelector_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _mapper.CreateMapper<string>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("selector");
    }

    [Fact]
    public void Map_NullAttributes_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _mapper.Map(null!, e => e.OrderId);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("attributes");
    }

    [Fact]
    public void Map_NullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>();

        // Act
        Action act = () => _mapper.Map(attributes, (Expression<Func<TestEntity, string>>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("selector");
    }

    [Fact]
    public void CreateMapper_UnsupportedExpressionShape_ThrowsUnsupportedExpressionException()
    {
        // Act - method call is not supported
        Action act = () => _mapper.CreateMapper(e => e.OrderId.ToUpper());

        // Assert
        act.Should().Throw<UnsupportedExpressionException>();
    }

    [Fact]
    public void CreateMapper_NoConverterForType_ThrowsMissingConverterException_AtCreationTime()
    {
        // Arrange - create entity with unsupported type
        // Use the default registry which doesn't have a converter for CustomUnsupportedType
        var mapper = new DirectResultMapper<EntityWithCustomType>(
            _resolverFactory,
            AttributeValueConverterRegistry.Default);

        // Act - should throw at creation time, not at mapping time
        Action act = () => mapper.CreateMapper(e => e.CustomValue);

        // Assert
        act.Should().Throw<MissingConverterException>()
            .Which.TargetType.Should().Be(typeof(CustomType));
    }

    #endregion

    #region Concurrent Access

    [Fact]
    public void CreateMapper_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "concurrent-test" },
            ["Price"] = new() { N = "99.99" }
        };

        var tasks = new List<Task<string>>();

        // Act - create mappers concurrently from multiple threads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var mapper = _mapper.CreateMapper(e => e.OrderId);
                return mapper(attributes);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - all should succeed with same result
        foreach (var task in tasks)
        {
            task.Result.Should().Be("concurrent-test");
        }
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void Map_ComplexProjection_AllTypesAndNesting_MapsCorrectly()
    {
        // Arrange
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(7);

        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "complex-order" },
            ["CustomerId"] = new() { S = "complex-customer" },
            ["Title"] = new() { S = "Complex Order" },
            ["Price"] = new() { N = "499.99" },
            ["IsActive"] = new() { BOOL = true },
            ["StartDate"] = new() { S = startDate.ToString("O") },
            ["EndDate"] = new() { S = endDate.ToString("O") },
            ["Tags"] = new()
            {
                L = new List<AttributeValue>
                {
                    new() { S = "premium" },
                    new() { S = "urgent" }
                }
            },
            ["Address"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["City"] = new() { S = "New York" },
                    ["ZipCode"] = new() { S = "10001" }
                }
            }
        };

        // Act
        var result = _mapper.Map(attributes, e => new
        {
            e.OrderId,
            e.CustomerId,
            e.Title,
            e.Price,
            e.IsActive,
            e.StartDate,
            e.EndDate,
            e.Tags,
            City = e.Address!.City
        });

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be("complex-order");
        result.CustomerId.Should().Be("complex-customer");
        result.Title.Should().Be("Complex Order");
        result.Price.Should().Be(499.99m);
        result.IsActive.Should().BeTrue();
        result.StartDate.Should().Be(startDate);
        result.EndDate.Should().Be(endDate);
        result.Tags.Should().Equal("premium", "urgent");
        result.City.Should().Be("New York");
    }

    [Fact]
    public void Map_NamedType_PartialProjection_UnmappedPropertiesHaveDefaults()
    {
        // Arrange
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "partial-order" }
            // Name and Price are missing
        };

        // Act
        var result = _mapper.Map(attributes, e => new OrderSummary
        {
            Id = e.OrderId,
            Name = e.Name,
            Price = e.Price
        });

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("partial-order");
        result.Name.Should().BeNull();
        result.Price.Should().Be(0m);
    }

    #endregion

    #region Test Helper Types

    private record OrderRecord(string Id, string Status);

    private class CustomerDto
    {
        public string CustomerId { get; }
        public string Name { get; }

        public CustomerDto(string customerId, string name)
        {
            CustomerId = customerId;
            Name = name;
        }
    }

    private class EntityWithMoney
    {
        public Money Amount { get; set; } = default!;
    }

    private record Money(decimal Amount, string Currency);

    private class MoneyConverter : AttributeValueConverterBase<Money>
    {
        public override AttributeValue ToAttributeValue(Money value)
        {
            if (value == null)
                return new AttributeValue { NULL = true };

            return new AttributeValue
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["Amount"] = new() { N = value.Amount.ToString() },
                    ["Currency"] = new() { S = value.Currency }
                }
            };
        }

        public override Money FromAttributeValue(AttributeValue attributeValue)
        {
            if (attributeValue?.M == null || attributeValue.M.Count == 0)
                return new Money(0m, string.Empty);

            var amount = attributeValue.M.TryGetValue("Amount", out var amountAttr) && amountAttr.N != null
                ? decimal.Parse(amountAttr.N)
                : 0m;

            var currency = attributeValue.M.TryGetValue("Currency", out var currencyAttr)
                ? currencyAttr.S
                : string.Empty;

            return new Money(amount, currency);
        }
    }

    private class EntityWithCustomType
    {
        public CustomType CustomValue { get; set; } = default!;
    }

    private class CustomType
    {
        public string Value { get; set; } = string.Empty;
    }

    #endregion
}
