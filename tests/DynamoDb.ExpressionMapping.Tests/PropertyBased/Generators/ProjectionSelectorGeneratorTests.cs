using System.Linq.Expressions;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;
using Gen = FsCheck.Fluent.Gen;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;

/// <summary>
/// Smoke tests for ProjectionSelectorGenerator to verify it produces valid output.
/// These are basic sanity checks before writing full property-based tests.
/// </summary>
public class ProjectionSelectorGeneratorTests
{
    [Fact]
    public void SimpleProjections_ShouldGenerateValidSinglePropertySelectors()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.ProjectionSelector(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 20);

        // Act & Assert
        foreach (var selector in samples)
        {
            // Verify it's a valid expression
            selector.Should().NotBeNull();
            selector.Parameters.Should().HaveCount(1);
            selector.Parameters[0].Type.Should().Be<TestEntity>();
            selector.ReturnType.Should().Be<object>();

            // Verify it can be compiled and executed
            var compiled = selector.Compile();
            var testEntity = CreateTestEntity();
            var executeAction = () => compiled(testEntity);

            // Should not throw
            executeAction.Should().NotThrow();

            // Result should not be null for our test entity (all properties populated)
            var result = compiled(testEntity);
            result.Should().NotBeNull();
        }
    }

    [Fact]
    public void CompositeProjections_ShouldGenerateValidMultiPropertySelectors()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.ProjectionSelector(Complexity.Composite);
        var samples = GenerateSamples(arbitrary, count: 20);

        // Act & Assert
        foreach (var selector in samples)
        {
            // Verify it's a valid expression
            selector.Should().NotBeNull();
            selector.Parameters.Should().HaveCount(1);
            selector.Parameters[0].Type.Should().Be<TestEntity>();
            selector.ReturnType.Should().Be<object>();

            // Verify the body is a tuple creation (ValueTuple)
            var body = selector.Body;
            body.Should().NotBeNull();

            // Should be a Convert to object wrapping a NewExpression (tuple constructor)
            if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            {
                unary.Operand.Should().BeOfType<NewExpression>();
                var newExpr = (NewExpression)unary.Operand;

                // Should have 2-3 properties (composite)
                newExpr.Arguments.Should().HaveCountGreaterOrEqualTo(2);
                newExpr.Arguments.Should().HaveCountLessOrEqualTo(3);
            }

            // Verify it can be compiled and executed
            var compiled = selector.Compile();
            var testEntity = CreateTestEntity();
            var executeAction = () => compiled(testEntity);

            executeAction.Should().NotThrow();
            var result = compiled(testEntity);
            result.Should().NotBeNull();
        }
    }

    [Fact]
    public void ComplexProjections_ShouldGenerateValidNestedPropertySelectors()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.ProjectionSelector(Complexity.Complex);
        var samples = GenerateSamples(arbitrary, count: 20);

        // Act & Assert
        foreach (var selector in samples)
        {
            // Verify it's a valid expression
            selector.Should().NotBeNull();
            selector.Parameters.Should().HaveCount(1);
            selector.Parameters[0].Type.Should().Be<TestEntity>();
            selector.ReturnType.Should().Be<object>();

            // Verify the body contains nested property access
            var body = selector.Body;
            body.Should().NotBeNull();

            // Should be a Convert to object wrapping a NewExpression (tuple constructor)
            if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            {
                unary.Operand.Should().BeOfType<NewExpression>();
                var newExpr = (NewExpression)unary.Operand;

                // Should have 2-4 properties (complex can have more)
                newExpr.Arguments.Should().HaveCountGreaterOrEqualTo(2);
                newExpr.Arguments.Should().HaveCountLessOrEqualTo(4);

                // At least one argument should involve nested property access
                bool hasNestedAccess = newExpr.Arguments.Any(arg => ContainsNestedPropertyAccess(arg));
                hasNestedAccess.Should().BeTrue("complex projections should include nested properties");
            }

            // Verify it can be compiled and executed
            var compiled = selector.Compile();
            var testEntity = CreateTestEntity();
            var executeAction = () => compiled(testEntity);

            executeAction.Should().NotThrow();
            var result = compiled(testEntity);
            result.Should().NotBeNull();
        }
    }

    [Fact]
    public void SimpleProjections_ShouldIncludeReservedKeywordProperties()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.ProjectionSelector(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 50);

        // Act
        var propertyNames = samples
            .Select(GetPropertyNameFromSimpleSelector)
            .Where(name => name != null)
            .ToList();

        // Assert - should include reserved keywords "Name" and "Status"
        propertyNames.Should().Contain("Name");
        propertyNames.Should().Contain("Status");
    }

    [Fact]
    public void AllComplexityLevels_ShouldProduceDifferentSamples()
    {
        // Arrange & Act
        var simpleSamples = GenerateSamples(ExpressionGenerators.ProjectionSelector(Complexity.Simple), 10);
        var compositeSamples = GenerateSamples(ExpressionGenerators.ProjectionSelector(Complexity.Composite), 10);
        var complexSamples = GenerateSamples(ExpressionGenerators.ProjectionSelector(Complexity.Complex), 10);

        // Assert - samples should be distinct (generators should produce variety)
        simpleSamples.Select(s => s.ToString()).Distinct().Should().HaveCountGreaterThan(1);
        compositeSamples.Select(s => s.ToString()).Distinct().Should().HaveCountGreaterThan(1);
        complexSamples.Select(s => s.ToString()).Distinct().Should().HaveCountGreaterThan(1);
    }

    #region Helper Methods

    /// <summary>
    /// Generate multiple samples from an arbitrary generator.
    /// </summary>
    private static List<T> GenerateSamples<T>(FsCheck.Arbitrary<T> arbitrary, int count)
    {
        var generator = arbitrary.Generator;
        return Gen.Sample(generator, count, 10).ToList();
    }

    /// <summary>
    /// Create a fully populated test entity to avoid null reference issues.
    /// </summary>
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
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            Tags = new[] { "tag1", "tag2" },
            Address = new Address
            {
                Street = "123 Main St",
                City = "Portland",
                ZipCode = "97201",
                Country = new Country
                {
                    Code = "US",
                    Name = "United States"
                }
            }
        };
    }

    /// <summary>
    /// Check if an expression contains nested property access (chained MemberExpression).
    /// </summary>
    private static bool ContainsNestedPropertyAccess(Expression expr)
    {
        // Unwrap Convert expressions
        while (expr is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            expr = unary.Operand;
        }

        // Check if it's a property access
        if (expr is MemberExpression memberExpr)
        {
            // If the property's owner is also a property access (not a parameter), it's nested
            return memberExpr.Expression is MemberExpression;
        }

        return false;
    }

    /// <summary>
    /// Extract property name from a simple selector like x => x.PropertyName.
    /// Returns null if not a simple selector.
    /// </summary>
    private static string? GetPropertyNameFromSimpleSelector(Expression<Func<TestEntity, object>> selector)
    {
        var body = selector.Body;

        // Unwrap Convert if present
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            body = unary.Operand;
        }

        // Check if it's a simple property access
        if (body is MemberExpression memberExpr && memberExpr.Expression is ParameterExpression)
        {
            return memberExpr.Member.Name;
        }

        return null;
    }

    #endregion
}
