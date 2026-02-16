using System.Linq.Expressions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;
using FsCheck;
using FsCheck.Fluent;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased;

/// <summary>
/// Property-based tests for filter expression composability.
/// Verifies invariants PR-01.4: no alias collisions after composition,
/// and composed expressions contain both operands.
/// </summary>
public class ComposabilityProperties
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;
    private readonly Config _config;

    public ComposabilityProperties()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
        _config = Config.Quick.WithMaxTest(PropertyTestConfig.MaxTest);
    }

    #region PR-01.4 Invariant 1: Composed Filters Never Have Alias Collisions

    /// <summary>
    /// Invariant: After And() / Or(), no alias key appears in both
    /// original left and re-aliased right dictionaries.
    /// Simple predicates with AND composition.
    /// </summary>
    [Fact]
    public void ComposedFiltersNeverHaveAliasCollisions_And_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            (left, right) =>
            {
                var builder = CreateBuilder();
                var leftResult = builder.BuildFilter(left);
                var rightResult = builder.BuildFilter(right);

                var composed = FilterExpressionResult.And(leftResult, rightResult);

                return ValidateNoAliasCollisions(leftResult, rightResult, composed, "AND");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for composite predicates with AND composition.
    /// </summary>
    [Fact]
    public void ComposedFiltersNeverHaveAliasCollisions_And_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            (left, right) =>
            {
                var builder = CreateBuilder();
                var leftResult = builder.BuildFilter(left);
                var rightResult = builder.BuildFilter(right);

                var composed = FilterExpressionResult.And(leftResult, rightResult);

                return ValidateNoAliasCollisions(leftResult, rightResult, composed, "AND");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for complex predicates with AND composition.
    /// </summary>
    [Fact]
    public void ComposedFiltersNeverHaveAliasCollisions_And_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            (left, right) =>
            {
                var builder = CreateBuilder();
                var leftResult = builder.BuildFilter(left);
                var rightResult = builder.BuildFilter(right);

                var composed = FilterExpressionResult.And(leftResult, rightResult);

                return ValidateNoAliasCollisions(leftResult, rightResult, composed, "AND");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant: After And() / Or(), no alias key appears in both
    /// original left and re-aliased right dictionaries.
    /// Simple predicates with OR composition.
    /// </summary>
    [Fact]
    public void ComposedFiltersNeverHaveAliasCollisions_Or_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            (left, right) =>
            {
                var builder = CreateBuilder();
                var leftResult = builder.BuildFilter(left);
                var rightResult = builder.BuildFilter(right);

                var composed = FilterExpressionResult.Or(leftResult, rightResult);

                return ValidateNoAliasCollisions(leftResult, rightResult, composed, "OR");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for composite predicates with OR composition.
    /// </summary>
    [Fact]
    public void ComposedFiltersNeverHaveAliasCollisions_Or_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            (left, right) =>
            {
                var builder = CreateBuilder();
                var leftResult = builder.BuildFilter(left);
                var rightResult = builder.BuildFilter(right);

                var composed = FilterExpressionResult.Or(leftResult, rightResult);

                return ValidateNoAliasCollisions(leftResult, rightResult, composed, "OR");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for complex predicates with OR composition.
    /// </summary>
    [Fact]
    public void ComposedFiltersNeverHaveAliasCollisions_Or_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            (left, right) =>
            {
                var builder = CreateBuilder();
                var leftResult = builder.BuildFilter(left);
                var rightResult = builder.BuildFilter(right);

                var composed = FilterExpressionResult.Or(leftResult, rightResult);

                return ValidateNoAliasCollisions(leftResult, rightResult, composed, "OR");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Validates that no alias collisions occur in composed filter expressions.
    /// </summary>
    private static Property ValidateNoAliasCollisions(
        FilterExpressionResult left,
        FilterExpressionResult right,
        FilterExpressionResult composed,
        string operation)
    {
        // Skip validation if either operand is empty (they get short-circuited)
        if (left.IsEmpty || right.IsEmpty)
        {
            return Prop.Label(true, "Skipped - empty operand");
        }

        // Extract all aliases from composed expression
        var composedNameAliases = composed.ExpressionAttributeNames.Keys.ToHashSet();
        var composedValueAliases = composed.ExpressionAttributeValues.Keys.ToHashSet();

        // Check that all left aliases are preserved in composed expression
        foreach (var leftNameAlias in left.ExpressionAttributeNames.Keys)
        {
            if (!composedNameAliases.Contains(leftNameAlias))
            {
                return Prop.Label(
                    false,
                    $"Left name alias '{leftNameAlias}' missing in composed {operation} expression");
            }
        }

        foreach (var leftValueAlias in left.ExpressionAttributeValues.Keys)
        {
            if (!composedValueAliases.Contains(leftValueAlias))
            {
                return Prop.Label(
                    false,
                    $"Left value alias '{leftValueAlias}' missing in composed {operation} expression");
            }
        }

        // Check that right operand's aliases were re-aliased to prevent collisions
        var leftNameAliases = left.ExpressionAttributeNames.Keys.ToHashSet();
        var rightNameAliases = right.ExpressionAttributeNames.Keys.ToHashSet();

        // If any right alias appears in left, it MUST have been re-aliased
        var potentialNameCollisions = rightNameAliases.Intersect(leftNameAliases).ToList();
        if (potentialNameCollisions.Count > 0)
        {
            // These aliases should NOT appear in the composed expression in their original form
            foreach (var collision in potentialNameCollisions)
            {
                // Count how many times this alias appears in composed - should be exactly once (from left)
                var countInComposed = composedNameAliases.Count(a => a == collision);
                if (countInComposed != 1)
                {
                    return Prop.Label(
                        false,
                        $"Name alias collision not resolved: '{collision}' appears {countInComposed} times in composed {operation} expression (expected 1)");
                }
            }
        }

        var leftValueAliases = left.ExpressionAttributeValues.Keys.ToHashSet();
        var rightValueAliases = right.ExpressionAttributeValues.Keys.ToHashSet();

        var potentialValueCollisions = rightValueAliases.Intersect(leftValueAliases).ToList();
        if (potentialValueCollisions.Count > 0)
        {
            foreach (var collision in potentialValueCollisions)
            {
                var countInComposed = composedValueAliases.Count(a => a == collision);
                if (countInComposed != 1)
                {
                    return Prop.Label(
                        false,
                        $"Value alias collision not resolved: '{collision}' appears {countInComposed} times in composed {operation} expression (expected 1)");
                }
            }
        }

        // Verify all aliases in composed have #filt_ or :filt_v prefixes
        foreach (var alias in composedNameAliases)
        {
            if (!alias.StartsWith("#filt_"))
            {
                return Prop.Label(
                    false,
                    $"Composed name alias '{alias}' does not use #filt_ prefix");
            }
        }

        foreach (var alias in composedValueAliases)
        {
            if (!alias.StartsWith(":filt_v"))
            {
                return Prop.Label(
                    false,
                    $"Composed value alias '{alias}' does not use :filt_v prefix");
            }
        }

        return Prop.Label(true, $"No alias collisions in {operation} composition");
    }

    #endregion

    #region PR-01.4 Invariant 2: Composed Filter Is Semantically Superset of Both

    /// <summary>
    /// Invariant: Composed expression string contains substrings
    /// corresponding to both operands.
    /// Simple predicates with AND composition.
    /// </summary>
    [Fact]
    public void ComposedFilterIsSemanticallySupersetOfBoth_And_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            (left, right) =>
            {
                var builder = CreateBuilder();
                var leftResult = builder.BuildFilter(left);
                var rightResult = builder.BuildFilter(right);

                var composed = FilterExpressionResult.And(leftResult, rightResult);

                return ValidateContainsBothOperands(leftResult, rightResult, composed, "AND");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for composite predicates with AND composition.
    /// </summary>
    [Fact]
    public void ComposedFilterIsSemanticallySupersetOfBoth_And_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            (left, right) =>
            {
                var builder = CreateBuilder();
                var leftResult = builder.BuildFilter(left);
                var rightResult = builder.BuildFilter(right);

                var composed = FilterExpressionResult.And(leftResult, rightResult);

                return ValidateContainsBothOperands(leftResult, rightResult, composed, "AND");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for complex predicates with AND composition.
    /// </summary>
    [Fact]
    public void ComposedFilterIsSemanticallySupersetOfBoth_And_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            (left, right) =>
            {
                var builder = CreateBuilder();
                var leftResult = builder.BuildFilter(left);
                var rightResult = builder.BuildFilter(right);

                var composed = FilterExpressionResult.And(leftResult, rightResult);

                return ValidateContainsBothOperands(leftResult, rightResult, composed, "AND");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant: Composed expression string contains substrings
    /// corresponding to both operands.
    /// Simple predicates with OR composition.
    /// </summary>
    [Fact]
    public void ComposedFilterIsSemanticallySupersetOfBoth_Or_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            (left, right) =>
            {
                var builder = CreateBuilder();
                var leftResult = builder.BuildFilter(left);
                var rightResult = builder.BuildFilter(right);

                var composed = FilterExpressionResult.Or(leftResult, rightResult);

                return ValidateContainsBothOperands(leftResult, rightResult, composed, "OR");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for composite predicates with OR composition.
    /// </summary>
    [Fact]
    public void ComposedFilterIsSemanticallySupersetOfBoth_Or_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            (left, right) =>
            {
                var builder = CreateBuilder();
                var leftResult = builder.BuildFilter(left);
                var rightResult = builder.BuildFilter(right);

                var composed = FilterExpressionResult.Or(leftResult, rightResult);

                return ValidateContainsBothOperands(leftResult, rightResult, composed, "OR");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for complex predicates with OR composition.
    /// </summary>
    [Fact]
    public void ComposedFilterIsSemanticallySupersetOfBoth_Or_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            (left, right) =>
            {
                var builder = CreateBuilder();
                var leftResult = builder.BuildFilter(left);
                var rightResult = builder.BuildFilter(right);

                var composed = FilterExpressionResult.Or(leftResult, rightResult);

                return ValidateContainsBothOperands(leftResult, rightResult, composed, "OR");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Validates that the composed expression contains both operands.
    /// </summary>
    private static Property ValidateContainsBothOperands(
        FilterExpressionResult left,
        FilterExpressionResult right,
        FilterExpressionResult composed,
        string operation)
    {
        // Skip validation if either operand is empty
        if (left.IsEmpty || right.IsEmpty)
        {
            return Prop.Label(true, "Skipped - empty operand");
        }

        // The composed expression should contain the operation keyword
        if (!composed.Expression.Contains($" {operation} ", StringComparison.Ordinal))
        {
            return Prop.Label(
                false,
                $"Composed expression does not contain ' {operation} ' keyword");
        }

        // The composed expression should be wrapped in parentheses
        // Format: (left.Expression) AND/OR (right.Expression)
        if (!composed.Expression.StartsWith("(") || !composed.Expression.EndsWith(")"))
        {
            return Prop.Label(
                false,
                $"Composed expression is not properly wrapped: {composed.Expression}");
        }

        // Check that left expression appears in the composed expression
        // The left side should appear as-is (not re-aliased)
        var expectedPattern = $"({left.Expression})";
        if (!composed.Expression.Contains(expectedPattern, StringComparison.Ordinal))
        {
            return Prop.Label(
                false,
                $"Composed expression does not contain left operand. Expected substring: {expectedPattern}");
        }

        // The right operand might have been re-aliased, so we can't check for exact substring match
        // Instead, verify that the right side exists after the operation keyword
        var parts = composed.Expression.Split($" {operation} ", 2);
        if (parts.Length != 2)
        {
            return Prop.Label(
                false,
                $"Composed expression does not split correctly on ' {operation} '");
        }

        // Verify that the right part is non-empty and properly parenthesized
        var rightPart = parts[1];
        if (!rightPart.StartsWith("(") || !rightPart.EndsWith(")"))
        {
            return Prop.Label(
                false,
                $"Right operand in composed expression is not properly wrapped: {rightPart}");
        }

        return Prop.Label(true, $"Composed expression contains both operands with {operation}");
    }

    #endregion

    #region Helper Methods

    private FilterExpressionBuilder<TestEntity> CreateBuilder()
    {
        return new FilterExpressionBuilder<TestEntity>(
            _resolverFactory,
            _converterRegistry);
    }

    #endregion
}
