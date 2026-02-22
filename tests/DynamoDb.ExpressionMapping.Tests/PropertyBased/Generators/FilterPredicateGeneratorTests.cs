using System.Linq.Expressions;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;
using Gen = FsCheck.Fluent.Gen;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;

/// <summary>
/// Smoke tests for FilterPredicateGenerator to verify it produces valid output.
/// These are basic sanity checks before writing full property-based tests.
/// </summary>
[Trait("Category", "Property")]
public class FilterPredicateGeneratorTests
{
    [Fact]
    public void SimplePredicates_ShouldGenerateValidSingleComparisonPredicates()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.FilterPredicate(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 50);

        // Act & Assert
        foreach (var predicate in samples)
        {
            // Verify it's a valid expression
            predicate.Should().NotBeNull();
            predicate.Parameters.Should().HaveCount(1);
            predicate.Parameters[0].Type.Should().Be<TestEntity>();
            predicate.ReturnType.Should().Be<bool>();

            // Verify it can be compiled and executed
            var compiled = predicate.Compile();
            var testEntity = CreateTestEntity();
            var executeAction = () => compiled(testEntity);

            // Should not throw
            executeAction.Should().NotThrow();

            // Result should be a boolean
            var result = compiled(testEntity);
            (result is true || result is false).Should().BeTrue();
        }
    }

    [Fact]
    public void CompositePredicates_ShouldGenerateValidCombinedPredicates()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.FilterPredicate(Complexity.Composite);
        var samples = GenerateSamples(arbitrary, count: 50);

        // Act & Assert
        foreach (var predicate in samples)
        {
            // Verify it's a valid expression
            predicate.Should().NotBeNull();
            predicate.Parameters.Should().HaveCount(1);
            predicate.Parameters[0].Type.Should().Be<TestEntity>();
            predicate.ReturnType.Should().Be<bool>();

            // Verify the body contains logical operators (AndAlso or OrElse)
            var body = predicate.Body;
            body.Should().NotBeNull();

            // Should contain at least one logical operator
            ContainsLogicalOperator(body).Should().BeTrue(
                "composite predicates should use && or ||");

            // Verify it can be compiled and executed
            var compiled = predicate.Compile();
            var testEntity = CreateTestEntity();
            var executeAction = () => compiled(testEntity);

            // Should not throw
            executeAction.Should().NotThrow();

            // Result should be a boolean
            var result = compiled(testEntity);
            (result is true || result is false).Should().BeTrue();
        }
    }

    [Fact]
    public void ComplexPredicates_ShouldGenerateValidNestedAndFunctionPredicates()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.FilterPredicate(Complexity.Complex);
        var samples = GenerateSamples(arbitrary, count: 50);

        // Act & Assert
        foreach (var predicate in samples)
        {
            // Verify it's a valid expression
            predicate.Should().NotBeNull();
            predicate.Parameters.Should().HaveCount(1);
            predicate.Parameters[0].Type.Should().Be<TestEntity>();
            predicate.ReturnType.Should().Be<bool>();

            // Verify it can be compiled and executed
            var compiled = predicate.Compile();
            var testEntity = CreateTestEntityWithNested();
            var executeAction = () => compiled(testEntity);

            // Should not throw
            executeAction.Should().NotThrow();

            // Result should be a boolean
            var result = compiled(testEntity);
            (result is true || result is false).Should().BeTrue();
        }
    }

    [Fact]
    public void SimplePredicates_ShouldIncludeStringComparisons()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.FilterPredicate(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Find string comparisons
        var stringComparisons = samples.Where(pred =>
        {
            var body = pred.Body;
            return ContainsStringComparison(body);
        }).ToList();

        // Assert
        stringComparisons.Should().NotBeEmpty("should generate string comparisons");
    }

    [Fact]
    public void SimplePredicates_ShouldIncludeNumericComparisons()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.FilterPredicate(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Find numeric comparisons
        var numericComparisons = samples.Where(pred =>
        {
            var body = pred.Body;
            return ContainsNumericComparison(body);
        }).ToList();

        // Assert
        numericComparisons.Should().NotBeEmpty("should generate numeric comparisons");
    }

    [Fact]
    public void SimplePredicates_ShouldIncludeNullableComparisons()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.FilterPredicate(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Find nullable comparisons
        var nullableComparisons = samples.Where(pred =>
        {
            var body = pred.Body;
            return ContainsNullableComparison(body);
        }).ToList();

        // Assert
        nullableComparisons.Should().NotBeEmpty("should generate nullable comparisons");
    }

    [Fact]
    public void ComplexPredicates_ShouldIncludeStringFunctions()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.FilterPredicate(Complexity.Complex);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Find method calls (StartsWith, Contains)
        var stringFunctions = samples.Where(pred =>
        {
            var body = pred.Body;
            return ContainsMethodCall(body, "StartsWith") || ContainsMethodCall(body, "Contains");
        }).ToList();

        // Assert
        stringFunctions.Should().NotBeEmpty("should generate string function predicates");
    }

    [Fact]
    public void ComplexPredicates_ShouldIncludeNotExpressions()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.FilterPredicate(Complexity.Complex);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Find NOT expressions
        var notExpressions = samples.Where(pred =>
        {
            var body = pred.Body;
            return ContainsNotExpression(body);
        }).ToList();

        // Assert
        notExpressions.Should().NotBeEmpty("should generate NOT predicates");
    }

    [Fact]
    public void ComplexPredicates_ShouldIncludeNestedPropertyAccess()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.FilterPredicate(Complexity.Complex);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Find nested property access (Address.City, etc.)
        var nestedAccess = samples.Where(pred =>
        {
            var body = pred.Body;
            return ContainsNestedPropertyAccess(body);
        }).ToList();

        // Assert
        nestedAccess.Should().NotBeEmpty("should generate nested property predicates");
    }

    #region Helper Methods

    private static List<Expression<Func<TestEntity, bool>>> GenerateSamples(
        FsCheck.Arbitrary<Expression<Func<TestEntity, bool>>> arbitrary,
        int count)
    {
        var gen = arbitrary.Generator;
        var samples = new List<Expression<Func<TestEntity, bool>>>();

        for (int i = 0; i < count; i++)
        {
            var sample = Gen.Sample(gen, 1, 10).Single();
            samples.Add(sample);
        }

        return samples;
    }

    private static TestEntity CreateTestEntity()
    {
        return new TestEntity
        {
            OrderId = "ORD001",
            CustomerId = "CUST001",
            Title = "Test Order",
            Name = "Test Name",
            Status = "Active",
            Price = 99.99m,
            IsActive = true,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Tags = new[] { "test", "sample" }
        };
    }

    private static TestEntity CreateTestEntityWithNested()
    {
        return new TestEntity
        {
            OrderId = "ORD001",
            CustomerId = "CUST001",
            Title = "Test Order",
            Name = "Test Name",
            Status = "Active",
            Price = 99.99m,
            IsActive = true,
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2024, 12, 31),
            Tags = new[] { "test", "sample" },
            Address = new Address
            {
                Street = "123 Main St",
                City = "Portland",
                ZipCode = "12345",
                Country = new Country
                {
                    Code = "US",
                    Name = "United States"
                }
            }
        };
    }

    private static bool ContainsLogicalOperator(Expression expression)
    {
        return expression.NodeType == ExpressionType.AndAlso
               || expression.NodeType == ExpressionType.OrElse
               || (expression is BinaryExpression binary &&
                   (ContainsLogicalOperator(binary.Left) || ContainsLogicalOperator(binary.Right)));
    }

    private static bool ContainsStringComparison(Expression expression)
    {
        if (expression is BinaryExpression binary)
        {
            if (binary.Left.Type == typeof(string) || binary.Right.Type == typeof(string))
            {
                return binary.NodeType is ExpressionType.Equal or ExpressionType.NotEqual;
            }

            return ContainsStringComparison(binary.Left) || ContainsStringComparison(binary.Right);
        }

        return false;
    }

    private static bool ContainsNumericComparison(Expression expression)
    {
        if (expression is BinaryExpression binary)
        {
            if (binary.Left.Type == typeof(decimal) || binary.Right.Type == typeof(decimal))
            {
                return binary.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
                    or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
                    or ExpressionType.LessThan or ExpressionType.LessThanOrEqual;
            }

            return ContainsNumericComparison(binary.Left) || ContainsNumericComparison(binary.Right);
        }

        return false;
    }

    private static bool ContainsNullableComparison(Expression expression)
    {
        if (expression is BinaryExpression binary)
        {
            if (binary.Left.Type == typeof(DateTime?) || binary.Right.Type == typeof(DateTime?))
            {
                return true;
            }

            return ContainsNullableComparison(binary.Left) || ContainsNullableComparison(binary.Right);
        }

        if (expression is MemberExpression member && member.Type == typeof(DateTime?))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsMethodCall(Expression expression, string methodName)
    {
        if (expression is MethodCallExpression method && method.Method.Name == methodName)
        {
            return true;
        }

        if (expression is BinaryExpression binary)
        {
            return ContainsMethodCall(binary.Left, methodName) || ContainsMethodCall(binary.Right, methodName);
        }

        if (expression is UnaryExpression unary)
        {
            return ContainsMethodCall(unary.Operand, methodName);
        }

        return false;
    }

    private static bool ContainsNotExpression(Expression expression)
    {
        if (expression.NodeType == ExpressionType.Not)
        {
            return true;
        }

        if (expression is BinaryExpression binary)
        {
            return ContainsNotExpression(binary.Left) || ContainsNotExpression(binary.Right);
        }

        if (expression is UnaryExpression unary)
        {
            return ContainsNotExpression(unary.Operand);
        }

        return false;
    }

    private static bool ContainsNestedPropertyAccess(Expression expression)
    {
        if (expression is MemberExpression member)
        {
            // Check if the member access is on another member (nested)
            if (member.Expression is MemberExpression)
            {
                return true;
            }

            // Recursively check
            if (member.Expression != null)
            {
                return ContainsNestedPropertyAccess(member.Expression);
            }
        }

        if (expression is BinaryExpression binary)
        {
            return ContainsNestedPropertyAccess(binary.Left) || ContainsNestedPropertyAccess(binary.Right);
        }

        if (expression is UnaryExpression unary)
        {
            return ContainsNestedPropertyAccess(unary.Operand);
        }

        if (expression is MethodCallExpression method)
        {
            if (method.Object != null)
            {
                return ContainsNestedPropertyAccess(method.Object);
            }

            return method.Arguments.Any(ContainsNestedPropertyAccess);
        }

        return false;
    }

    #endregion
}
