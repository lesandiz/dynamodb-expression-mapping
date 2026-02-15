using Amazon.DynamoDBv2;
using DynamoDb.ExpressionMapping;
using WebApiExample.Converters;
using WebApiExample.Infrastructure;
using WebApiExample.Models;

var builder = WebApplication.CreateBuilder(args);

// DynamoDB Client — read ServiceUrl from configuration
var dynamoDbServiceUrl = builder.Configuration["DynamoDb:ServiceUrl"] ?? "http://localhost:8000";
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var config = new AmazonDynamoDBConfig { ServiceURL = dynamoDbServiceUrl };
    return new AmazonDynamoDBClient(config);
});

// Expression Mapping — register custom converters
builder.Services.AddDynamoDbExpressionMapping(config =>
{
    config.WithConverter(new MoneyConverter());
});

// Entity Configuration — register entity-specific resolvers
builder.Services.AddDynamoDbEntity<Order>();
builder.Services.AddDynamoDbEntity<Product>();
builder.Services.AddDynamoDbEntity<Customer>();

// Repository Registration
// Note: IOrderRepository will be registered in task 2.9
// builder.Services.AddSingleton<IOrderRepository, OrderRepository>();

// Database Seeder — runs on startup
builder.Services.AddHostedService<DynamoDbSeeder>();

// ASP.NET Core Essentials
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
