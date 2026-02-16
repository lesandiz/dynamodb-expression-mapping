using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Gen = FsCheck.Fluent.Gen;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;

/// <summary>
/// Smoke tests for UpdateOperationGenerator to verify it produces valid output.
/// These are basic sanity checks before writing full property-based tests.
/// </summary>
public class UpdateOperationGeneratorTests
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;

    public UpdateOperationGeneratorTests()
    {
        var services = new ServiceCollection();
        services.AddDynamoDbExpressionMapping();
        var provider = services.BuildServiceProvider();

        _resolverFactory = provider.GetRequiredService<IAttributeNameResolverFactory>();
        _converterRegistry = provider.GetRequiredService<IAttributeValueConverterRegistry>();
    }

    [Fact]
    public void SimpleOperations_ShouldGenerateValidSingleOperations()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.UpdateOperation(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 50);

        // Act & Assert
        foreach (var operation in samples)
        {
            // Verify it's a valid operation
            operation.Should().NotBeNull();

            // Verify it can be executed against a builder without throwing
            var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
            var buildAction = () =>
            {
                var updatedBuilder = operation(builder);
                return (UpdateExpressionBuilder<TestEntity>)updatedBuilder;
            };

            // Should not throw
            var finalBuilder = buildAction.Should().NotThrow().Subject;

            // Verify the builder produces a valid result
            var result = finalBuilder.Build();
            result.Should().NotBeNull();
            result.Expression.Should().NotBeNullOrWhiteSpace("expression should not be empty");
        }
    }

    [Fact]
    public void CompositeOperations_ShouldGenerateValidChainedOperations()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.UpdateOperation(Complexity.Composite);
        var samples = GenerateSamples(arbitrary, count: 50);

        // Act & Assert
        foreach (var operation in samples)
        {
            // Verify it's a valid operation
            operation.Should().NotBeNull();

            // Verify it can be executed against a builder without throwing
            var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
            var buildAction = () =>
            {
                var updatedBuilder = operation(builder);
                return (UpdateExpressionBuilder<TestEntity>)updatedBuilder;
            };

            // Should not throw
            var finalBuilder = buildAction.Should().NotThrow().Subject;

            // Verify the builder produces a valid result
            var result = finalBuilder.Build();
            result.Should().NotBeNull();
            result.Expression.Should().NotBeNullOrWhiteSpace("expression should not be empty");

        }
    }

    [Fact]
    public void ComplexOperations_ShouldGenerateValidMixedClauseOperations()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.UpdateOperation(Complexity.Complex);
        var samples = GenerateSamples(arbitrary, count: 50);

        // Act & Assert
        foreach (var operation in samples)
        {
            // Verify it's a valid operation
            operation.Should().NotBeNull();

            // Verify it can be executed against a builder without throwing
            var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
            var buildAction = () =>
            {
                var updatedBuilder = operation(builder);
                return (UpdateExpressionBuilder<TestEntity>)updatedBuilder;
            };

            // Should not throw
            var finalBuilder = buildAction.Should().NotThrow().Subject;

            // Verify the builder produces a valid result
            var result = finalBuilder.Build();
            result.Should().NotBeNull();
            result.Expression.Should().NotBeNullOrWhiteSpace("expression should not be empty");

            // Complex should have multiple clause types (SET, REMOVE, ADD, DELETE)
            var expression = result.Expression;
            ContainsMultipleClauses(expression).Should().BeTrue(
                "complex operations should have multiple clause types");
        }
    }

    [Fact]
    public void SimpleOperations_ShouldIncludeSetOperations()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.UpdateOperation(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Find SET operations
        var setOperations = samples.Where(op =>
        {
            var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
            var finalBuilder = (UpdateExpressionBuilder<TestEntity>)op(builder);
            var result = finalBuilder.Build();
            return result.Expression.StartsWith("SET");
        }).ToList();

        // Assert
        setOperations.Should().NotBeEmpty("should generate SET operations");
    }

    [Fact]
    public void SimpleOperations_ShouldIncludeRemoveOperations()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.UpdateOperation(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Find REMOVE operations
        var removeOperations = samples.Where(op =>
        {
            var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
            var finalBuilder = (UpdateExpressionBuilder<TestEntity>)op(builder);
            var result = finalBuilder.Build();
            return result.Expression.StartsWith("REMOVE");
        }).ToList();

        // Assert
        removeOperations.Should().NotBeEmpty("should generate REMOVE operations");
    }

    [Fact]
    public void SimpleOperations_ShouldIncludeIncrementDecrementOperations()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.UpdateOperation(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Find Increment/Decrement operations (SET with + or -)
        var mathOperations = samples.Where(op =>
        {
            var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
            var finalBuilder = (UpdateExpressionBuilder<TestEntity>)op(builder);
            var result = finalBuilder.Build();
            return result.Expression.Contains(" + ") || result.Expression.Contains(" - ");
        }).ToList();

        // Assert
        mathOperations.Should().NotBeEmpty("should generate increment/decrement operations");
    }

    [Fact]
    public void SimpleOperations_ShouldIncludeSetIfNotExistsOperations()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.UpdateOperation(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Find SetIfNotExists operations (contains if_not_exists)
        var ifNotExistsOps = samples.Where(op =>
        {
            var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
            var finalBuilder = (UpdateExpressionBuilder<TestEntity>)op(builder);
            var result = finalBuilder.Build();
            return result.Expression.Contains("if_not_exists");
        }).ToList();

        // Assert
        ifNotExistsOps.Should().NotBeEmpty("should generate SetIfNotExists operations");
    }

    [Fact]
    public void SimpleOperations_ShouldIncludeAddOperations()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.UpdateOperation(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Find ADD operations
        var addOperations = samples.Where(op =>
        {
            var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
            var finalBuilder = (UpdateExpressionBuilder<TestEntity>)op(builder);
            var result = finalBuilder.Build();
            return result.Expression.StartsWith("ADD");
        }).ToList();

        // Assert
        addOperations.Should().NotBeEmpty("should generate ADD operations");
    }

    [Fact]
    public void SimpleOperations_ShouldIncludeDeleteOperations()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.UpdateOperation(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Find DELETE operations
        var deleteOperations = samples.Where(op =>
        {
            var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
            var finalBuilder = (UpdateExpressionBuilder<TestEntity>)op(builder);
            var result = finalBuilder.Build();
            return result.Expression.StartsWith("DELETE");
        }).ToList();

        // Assert
        deleteOperations.Should().NotBeEmpty("should generate DELETE operations");
    }

    [Fact]
    public void ComplexOperations_ShouldCombineMultipleClauseTypes()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.UpdateOperation(Complexity.Complex);
        var samples = GenerateSamples(arbitrary, count: 100);

        // Act - Count clause types
        var clauseTypeCounts = samples.Select(op =>
        {
            var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
            var finalBuilder = (UpdateExpressionBuilder<TestEntity>)op(builder);
            var result = finalBuilder.Build();
            return CountClauseTypes(result.Expression);
        }).ToList();

        // Assert
        var multiClauseOps = clauseTypeCounts.Where(count => count >= 2).ToList();
        multiClauseOps.Should().NotBeEmpty("should generate operations with multiple clause types");
    }

    [Fact]
    public void AllComplexities_ShouldProduceNonEmptyExpressionAttributeValues()
    {
        // Arrange
        var simpleArbitrary = ExpressionGenerators.UpdateOperation(Complexity.Simple);
        var compositArbitrary = ExpressionGenerators.UpdateOperation(Complexity.Composite);
        var complexArbitrary = ExpressionGenerators.UpdateOperation(Complexity.Complex);

        var allSamples = GenerateSamples(simpleArbitrary, 30)
            .Concat(GenerateSamples(compositArbitrary, 30))
            .Concat(GenerateSamples(complexArbitrary, 30));

        // Act & Assert
        foreach (var operation in allSamples)
        {
            var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
            var finalBuilder = (UpdateExpressionBuilder<TestEntity>)operation(builder);
            var result = finalBuilder.Build();

            // Most operations should produce values (except pure REMOVE)
            if (!result.Expression.StartsWith("REMOVE") || result.Expression.Contains("SET"))
            {
                result.ExpressionAttributeValues.Should().NotBeEmpty(
                    "non-remove operations should have attribute values");
            }
        }
    }

    [Fact]
    public void AllComplexities_ShouldUseCorrectAliasPrefix()
    {
        // Arrange
        var simpleArbitrary = ExpressionGenerators.UpdateOperation(Complexity.Simple);
        var samples = GenerateSamples(simpleArbitrary, count: 50);

        // Act & Assert
        foreach (var operation in samples)
        {
            var builder = new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
            var finalBuilder = (UpdateExpressionBuilder<TestEntity>)operation(builder);
            var result = finalBuilder.Build();

            // All name aliases should start with #upd_
            foreach (var nameKey in result.ExpressionAttributeNames.Keys)
            {
                nameKey.Should().StartWith("#upd_", "update expression name aliases should use #upd_ prefix");
            }

            // All value aliases should start with :upd_v
            foreach (var valueKey in result.ExpressionAttributeValues.Keys)
            {
                valueKey.Should().StartWith(":upd_v", "update expression value aliases should use :upd_v prefix");
            }
        }
    }

    #region Helper Methods

    private static List<Func<UpdateExpressionBuilder<TestEntity>, IUpdateExpressionBuilder<TestEntity>>> GenerateSamples(
        FsCheck.Arbitrary<Func<UpdateExpressionBuilder<TestEntity>, IUpdateExpressionBuilder<TestEntity>>> arbitrary,
        int count)
    {
        var gen = arbitrary.Generator;
        var samples = new List<Func<UpdateExpressionBuilder<TestEntity>, IUpdateExpressionBuilder<TestEntity>>>();

        for (int i = 0; i < count; i++)
        {
            var sample = Gen.Sample(gen, 1, 10).Single();
            samples.Add(sample);
        }

        return samples;
    }

    private static bool ContainsMultipleClauses(string expression)
    {
        var clauseCount = 0;

        if (expression.Contains("SET")) clauseCount++;
        if (expression.Contains("REMOVE")) clauseCount++;
        if (expression.Contains("ADD")) clauseCount++;
        if (expression.Contains("DELETE")) clauseCount++;

        return clauseCount >= 2;
    }

    private static int CountClauseTypes(string expression)
    {
        var clauseCount = 0;

        if (expression.Contains("SET")) clauseCount++;
        if (expression.Contains("REMOVE")) clauseCount++;
        if (expression.Contains("ADD")) clauseCount++;
        if (expression.Contains("DELETE")) clauseCount++;

        return clauseCount;
    }

    #endregion
}
