using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;
using Gen = FsCheck.Fluent.Gen;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;

/// <summary>
/// Smoke tests for KeyConditionOperationGenerator to verify it produces valid output.
/// </summary>
[Trait("Category", "Property")]
public class KeyConditionOperationGeneratorTests
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;

    public KeyConditionOperationGeneratorTests()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
    }

    [Fact]
    public void SimpleOperations_ShouldGenerateValidPKOnlyConditions()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.KeyConditionOperation(Complexity.Simple);
        var samples = GenerateSamples(arbitrary, count: 50);

        // Act & Assert
        foreach (var operation in samples)
        {
            operation.Should().NotBeNull();

            var builder = new KeyConditionExpressionBuilder<TestKeyedEntity>(_resolverFactory, _converterRegistry);
            var result = operation(builder);

            result.Should().NotBeNull();
            result.Expression.Should().NotBeNullOrWhiteSpace();
            result.Expression.Should().Contain("=", "PK-only condition must contain equality operator");
            result.ExpressionAttributeValues.Should().HaveCountGreaterOrEqualTo(1, "must have at least the PK value");
            result.Expression.Should().NotContain("AND", "PK-only condition should not contain AND");
        }
    }

    [Fact]
    public void CompositeOperations_ShouldGenerateValidPKPlusSKConditions()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.KeyConditionOperation(Complexity.Composite);
        var samples = GenerateSamples(arbitrary, count: 50);

        // Act & Assert
        foreach (var operation in samples)
        {
            operation.Should().NotBeNull();

            var builder = new KeyConditionExpressionBuilder<TestKeyedEntity>(_resolverFactory, _converterRegistry);
            var result = operation(builder);

            result.Should().NotBeNull();
            result.Expression.Should().Contain("AND", "PK+SK condition must contain AND");
            result.ExpressionAttributeValues.Should().HaveCountGreaterOrEqualTo(2, "must have PK and SK values");
        }
    }

    [Fact]
    public void ComplexOperations_ShouldGenerateValidBetweenOrBeginsWithConditions()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.KeyConditionOperation(Complexity.Complex);
        var samples = GenerateSamples(arbitrary, count: 50);

        // Act & Assert
        foreach (var operation in samples)
        {
            operation.Should().NotBeNull();

            var builder = new KeyConditionExpressionBuilder<TestKeyedEntity>(_resolverFactory, _converterRegistry);
            var result = operation(builder);

            result.Should().NotBeNull();
            var hasBetween = result.Expression.Contains("BETWEEN");
            var hasBeginsWith = result.Expression.Contains("begins_with(");
            (hasBetween || hasBeginsWith).Should().BeTrue(
                $"complex condition must contain BETWEEN or begins_with. Got: {result.Expression}");
        }
    }

    [Fact]
    public void CompositeOperations_ShouldIncludeAllComparisonOperators()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.KeyConditionOperation(Complexity.Composite);
        var samples = GenerateSamples(arbitrary, count: 200);

        // Act - collect SK operators from the part after AND
        var skOperators = new HashSet<string>();
        foreach (var operation in samples)
        {
            var builder = new KeyConditionExpressionBuilder<TestKeyedEntity>(_resolverFactory, _converterRegistry);
            var result = operation(builder);
            var parts = result.Expression.Split(" AND ");
            if (parts.Length == 2)
            {
                var skPart = parts[1];
                if (skPart.Contains(" = ")) skOperators.Add("=");
                else if (skPart.Contains(" <= ")) skOperators.Add("<=");
                else if (skPart.Contains(" >= ")) skOperators.Add(">=");
                else if (skPart.Contains(" < ")) skOperators.Add("<");
                else if (skPart.Contains(" > ")) skOperators.Add(">");
            }
        }

        // Assert - all 5 comparison operators should appear
        skOperators.Should().Contain("=");
        skOperators.Should().Contain("<");
        skOperators.Should().Contain("<=");
        skOperators.Should().Contain(">");
        skOperators.Should().Contain(">=");
    }

    [Fact]
    public void AllComplexities_ShouldUseCorrectAliasPrefix()
    {
        // Arrange
        var simpleArb = ExpressionGenerators.KeyConditionOperation(Complexity.Simple);
        var compositeArb = ExpressionGenerators.KeyConditionOperation(Complexity.Composite);
        var complexArb = ExpressionGenerators.KeyConditionOperation(Complexity.Complex);

        var allSamples = GenerateSamples(simpleArb, 30)
            .Concat(GenerateSamples(compositeArb, 30))
            .Concat(GenerateSamples(complexArb, 30));

        // Act & Assert
        foreach (var operation in allSamples)
        {
            var builder = new KeyConditionExpressionBuilder<TestKeyedEntity>(_resolverFactory, _converterRegistry);
            var result = operation(builder);

            foreach (var nameKey in result.ExpressionAttributeNames.Keys)
            {
                nameKey.Should().StartWith("#key_", "key condition name aliases should use #key_ prefix");
            }

            foreach (var valueKey in result.ExpressionAttributeValues.Keys)
            {
                valueKey.Should().StartWith(":key_v", "key condition value aliases should use :key_v prefix");
            }
        }
    }

    [Fact]
    public void AllComplexities_ShouldAlwaysContainPartitionKeyEquality()
    {
        // Arrange
        var simpleArb = ExpressionGenerators.KeyConditionOperation(Complexity.Simple);
        var compositeArb = ExpressionGenerators.KeyConditionOperation(Complexity.Composite);
        var complexArb = ExpressionGenerators.KeyConditionOperation(Complexity.Complex);

        var allSamples = GenerateSamples(simpleArb, 30)
            .Concat(GenerateSamples(compositeArb, 30))
            .Concat(GenerateSamples(complexArb, 30));

        // Act & Assert
        foreach (var operation in allSamples)
        {
            var builder = new KeyConditionExpressionBuilder<TestKeyedEntity>(_resolverFactory, _converterRegistry);
            var result = operation(builder);

            // The expression must always start with the PK equality pattern
            // Either "PK = :key_v0" or "#key_0 = :key_v0"
            var pkPart = result.Expression.Contains(" AND ")
                ? result.Expression.Split(" AND ")[0]
                : result.Expression;

            pkPart.Should().Contain("=", "partition key condition must use equality operator");
        }
    }

    [Fact]
    public void ComplexOperations_ShouldIncludeBothBetweenAndBeginsWith()
    {
        // Arrange
        var arbitrary = ExpressionGenerators.KeyConditionOperation(Complexity.Complex);
        var samples = GenerateSamples(arbitrary, count: 200);

        // Act
        var hasBetween = false;
        var hasBeginsWith = false;
        foreach (var operation in samples)
        {
            var builder = new KeyConditionExpressionBuilder<TestKeyedEntity>(_resolverFactory, _converterRegistry);
            var result = operation(builder);
            if (result.Expression.Contains("BETWEEN")) hasBetween = true;
            if (result.Expression.Contains("begins_with(")) hasBeginsWith = true;
        }

        // Assert
        hasBetween.Should().BeTrue("generator should produce BETWEEN conditions");
        hasBeginsWith.Should().BeTrue("generator should produce begins_with conditions");
    }

    #region Helper Methods

    private static List<Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult>> GenerateSamples(
        FsCheck.Arbitrary<Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult>> arbitrary,
        int count)
    {
        var gen = arbitrary.Generator;
        var samples = new List<Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult>>();

        for (int i = 0; i < count; i++)
        {
            var sample = Gen.Sample(gen, 1, 10).Single();
            samples.Add(sample);
        }

        return samples;
    }

    #endregion
}
