# Examples

## ConsoleQuickStart

Example project demonstrating library usage. DynamoDB Local runs on **port 8002** (port 8000 was in use).

```bash
cd examples/ConsoleQuickStart && docker compose up -d && dotnet run
```

## WebApiExample

ASP.NET Core Web API demonstrating real-world usage with RESTful endpoints. DynamoDB Local runs on **port 8003** (host) mapping to 8000 (container).

```bash
cd examples/WebApiExample && docker compose up -d
# API runs on port 5000
# Swagger UI: http://localhost:5000/swagger/index.html
```

Test endpoints:

```bash
# Query orders with pagination
curl "http://localhost:5000/api/orders?customerId=alice&limit=2"

# Get single order
curl "http://localhost:5000/api/orders/alice/001"

# Create order
curl -X POST http://localhost:5000/api/orders -H "Content-Type: application/json" \
  -d '{"orderId":"ORD001","customerId":"CUST001","name":"Test","status":"Processing","quantity":1,"totalAmount":99.99,"totalCurrency":"USD","street":"123 Main","city":"Portland","postCode":"12345","tags":["test"]}'

# Update order
curl -X PUT http://localhost:5000/api/orders/CUST001/ORD001 -H "Content-Type: application/json" \
  -d '{"status":"Shipped","notes":"Shipped via FedEx"}'

# Delete order
curl -X DELETE http://localhost:5000/api/orders/CUST001/ORD001

# Filter products
curl "http://localhost:5000/api/products?category=Electronics&activeOnly=true"

# Get customer
curl "http://localhost:5000/api/customers/alice"
```