# Spec 00: Package Overview

## Package Name

`DynamoDb.ExpressionMapping`

## Purpose

A general-purpose .NET library that bridges C# LINQ expression trees and AWS DynamoDB expressions. It enables type-safe, efficient DynamoDB operations by converting C# lambda expressions into DynamoDB `ProjectionExpression`, `FilterExpression`, `ConditionExpression`, `UpdateExpression`, and `KeyConditionExpression` strings — with direct result mapping that avoids full entity hydration.

## Problem Statement

The AWS SDK for .NET (`AWSSDK.DynamoDBv2`) provides only string-based expression building. Teams using the low-level SDK must manually construct `ProjectionExpression`, `FilterExpression`, and `ExpressionAttributeNames`/`ExpressionAttributeValues` dictionaries. This is error-prone, hard to refactor, and lacks compile-time safety.

Existing solutions (PocoDynamo, Linq2DynamoDb) are full ORM replacements with heavy dependencies and commercial licences. No lightweight, pluggable library exists that:

1. Converts C# expressions to DynamoDB expression strings
2. Maps DynamoDB `AttributeValue` results directly to projected types without hydrating a full entity
3. Works alongside the low-level AWS SDK rather than replacing it
4. Is generic and reusable across different entity types

## Goals

1. **Type-safe expression building** — Compile-time checked DynamoDB expressions from C# lambdas
2. **Direct result mapping** — Map `Dictionary<string, AttributeValue>` directly to `TResult` without intermediate full-entity hydration
3. **Attribute name resolution** — Support configurable C# property name to DynamoDB attribute name mapping (convention-based, attribute-based, or explicit)
4. **Reserved keyword handling** — Automatic detection and aliasing of DynamoDB reserved keywords
5. **Expression caching** — Cache compiled expressions and projection results to avoid repeated analysis
6. **AWS SDK integration** — Extension methods on `GetItemRequest`, `QueryRequest`, `ScanRequest`, `BatchGetItemRequest`
7. **Pluggable type converters** — Extensible `AttributeValue` to .NET type conversion
8. **Minimal dependencies** — Only `AWSSDK.DynamoDBv2` and `System.Linq.Expressions`

## Non-Goals

- Full ORM / `IQueryable<T>` provider (not replacing DynamoDBContext)
- Table creation, schema management, or migration tooling
- Transaction orchestration (use AWS SDK directly)
- Connection/client management
- Caching layer (in-memory, Redis, etc.) — that is a consumer concern

## Design Principles

1. **Composable, not prescriptive** — Each component (visitor, builder, mapper, converters) is independently usable
2. **Generic-first** — All public APIs are generic over `TSource` (the entity type), not coupled to any specific model
3. **Convention over configuration** — Sensible defaults (property name = attribute name) with override points
4. **Fail fast** — Validate expressions at build time, throw clear exceptions for unsupported patterns
5. **Performance-aware** — Cache compiled delegates, avoid allocations in hot paths, support pre-compiled mappers

## Target Framework

- .NET 8.0+ (primary target)
- .NET Standard 2.1 compatibility as a stretch goal

## Dependencies

- `AWSSDK.DynamoDBv2` (>= 3.7.x)
- `Microsoft.Extensions.Logging.Abstractions` (>= 8.0.0) — optional logging support
- `Microsoft.Extensions.DependencyInjection.Abstractions` (>= 8.0.0) — optional DI registration extensions

## Package Structure

```
DynamoDb.ExpressionMapping/
├── Attributes/
│   ├── DynamoDbAttributeAttribute.cs      # [DynamoDbAttribute("actual_name")]
│   ├── DynamoDbIgnoreAttribute.cs         # [DynamoDbIgnore]
│   └── DynamoDbConverterAttribute.cs      # [DynamoDbConverter(typeof(...))]
├── Mapping/
│   ├── IAttributeNameResolver.cs          # Property name → DynamoDB attribute name
│   ├── AttributeNameResolver.cs           # Convention + attribute-based resolution
│   ├── IAttributeValueConverter.cs        # AttributeValue ↔ .NET type
│   ├── AttributeValueConverterRegistry.cs # Type converter registry
│   └── BuiltInConverters.cs               # String, Guid, bool, int, DateTime, etc.
├── Expressions/
│   ├── ProjectionExpressionVisitor.cs     # Extracts property paths from selectors
│   ├── ProjectionBuilder.cs               # ProjectionBuilder<TSource> — builds ProjectionExpression string
│   ├── FilterExpressionBuilder.cs         # FilterExpressionBuilder<TSource> — builds FilterExpression
│   ├── ConditionExpressionBuilder.cs      # ConditionExpressionBuilder<TSource> — builds ConditionExpression
│   ├── UpdateExpressionBuilder.cs         # UpdateExpressionBuilder<TSource> — builds UpdateExpression
│   ├── KeyConditionExpressionBuilder.cs   # KeyConditionExpressionBuilder<TSource> — builds KeyConditionExpression
│   └── ExpressionResult.cs                # Result container (expression + names + values)
├── ResultMapping/
│   ├── IDirectResultMapper.cs             # IDirectResultMapper<TSource> — AttributeValue dict → TResult
│   ├── DirectResultMapper.cs              # DirectResultMapper<TSource> — expression-based direct mapping
│   └── ResultMapperCache.cs               # Caches compiled mapper delegates
├── ReservedKeywords/
│   ├── ReservedKeywordRegistry.cs         # Reserved keyword detection
│   └── AliasGenerator.cs                  # Alias generation for reserved words
├── Extensions/
│   ├── GetItemRequestExtensions.cs        # .WithProjection(selector)
│   ├── QueryRequestExtensions.cs          # .WithProjection(selector), .WithFilter(predicate)
│   ├── ScanRequestExtensions.cs           # .WithProjection(selector), .WithFilter(predicate)
│   ├── BatchGetItemRequestExtensions.cs   # .WithProjection(tableName, selector)
│   └── PutItemRequestExtensions.cs        # .WithCondition(predicate)
├── Exceptions/
│   ├── ExpressionMappingException.cs      # Abstract base for all library exceptions
│   ├── UnsupportedExpressionException.cs  # Unsupported expression tree node
│   ├── MissingConverterException.cs       # No converter for .NET type
│   ├── ExpressionAttributeConflictException.cs # Alias collision during merge
│   ├── InvalidExpressionException.cs      # Abstract base for builder validation errors
│   ├── InvalidProjectionException.cs      # Invalid projection selector
│   ├── InvalidFilterException.cs          # Invalid filter/condition predicate
│   ├── InvalidUpdateException.cs          # Invalid update operation
│   └── InvalidKeyConditionException.cs    # Invalid key condition input
├── Caching/
│   ├── ExpressionCache.cs                 # Thread-safe expression result cache
│   └── ExpressionKeyGenerator.cs          # Structural expression hashing
└── DynamoDbExpressionConfig.cs            # Global configuration / builder
```

## Versioning

Semantic versioning. Initial release: `1.0.0`.
