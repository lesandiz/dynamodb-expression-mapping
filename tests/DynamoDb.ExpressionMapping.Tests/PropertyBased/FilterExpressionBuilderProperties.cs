using System.Linq.Expressions;
using System.Text.RegularExpressions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;
using FsCheck;
using FsCheck.Fluent;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased;

/// <summary>
/// Property-based tests for FilterExpressionBuilder.
/// Verifies invariants PR-01.2: non-empty expressions, balanced parentheses,
/// placeholder/dictionary consistency, and scope isolation.
/// </summary>
public class FilterExpressionBuilderProperties
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;
    private readonly Config _config;

    public FilterExpressionBuilderProperties()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
        _config = Config.Quick.WithMaxTest(PropertyTestConfig.MaxTest);
    }

    #region PR-01.2 Invariant 1: Filter Never Produces Empty Expression

    /// <summary>
    /// Invariant: Non-trivial predicate always produces non-empty expression string.
    /// Simple predicates.
    /// </summary>
    [Fact]
    public void FilterNeverProducesEmptyExpression_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            predicate =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildFilter(predicate);

                // Non-trivial predicates must produce non-empty expression
                if (string.IsNullOrWhiteSpace(result.Expression))
                {
                    return Prop.Label(
                        false,
                        $"Empty expression produced for predicate: {predicate}");
                }

                return Prop.Label(true, "Non-empty expression produced");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for composite predicates.
    /// </summary>
    [Fact]
    public void FilterNeverProducesEmptyExpression_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            predicate =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildFilter(predicate);

                if (string.IsNullOrWhiteSpace(result.Expression))
                {
                    return Prop.Label(
                        false,
                        $"Empty expression produced for composite predicate");
                }

                return Prop.Label(true, "Non-empty expression produced");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for complex predicates (nested properties, functions, NOT).
    /// </summary>
    [Fact]
    public void FilterNeverProducesEmptyExpression_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            predicate =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildFilter(predicate);

                if (string.IsNullOrWhiteSpace(result.Expression))
                {
                    return Prop.Label(
                        false,
                        $"Empty expression produced for complex predicate");
                }

                return Prop.Label(true, "Non-empty expression produced");
            });

        Check.One(_config, property);
    }

    #endregion

    #region PR-01.2 Invariant 2: Filter Parentheses Are Balanced

    /// <summary>
    /// Invariant: Count of '(' equals count of ')' in expression string.
    /// Simple predicates.
    /// </summary>
    [Fact]
    public void FilterParenthesesAreBalanced_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            predicate =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildFilter(predicate);
                return ValidateBalancedParentheses(result);
            });

        Check.One(_config, property);
    }

    [Fact]
    public void FilterParenthesesAreBalanced_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            predicate =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildFilter(predicate);
                return ValidateBalancedParentheses(result);
            });

        Check.One(_config, property);
    }

    [Fact]
    public void FilterParenthesesAreBalanced_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            predicate =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildFilter(predicate);
                return ValidateBalancedParentheses(result);
            });

        Check.One(_config, property);
    }

    private static Property ValidateBalancedParentheses(FilterExpressionResult result)
    {
        int openCount = result.Expression.Count(c => c == '(');
        int closeCount = result.Expression.Count(c => c == ')');

        if (openCount != closeCount)
        {
            return Prop.Label(
                false,
                $"Unbalanced parentheses: {openCount} open, {closeCount} close. Expression: {result.Expression}");
        }

        return Prop.Label(true, "Parentheses balanced");
    }

    #endregion

    #region PR-01.2 Invariant 3: Filter Value Placeholders Match Dictionary

    /// <summary>
    /// Invariant: Every :filt_vN in expression string has a corresponding
    /// entry in ExpressionAttributeValues, and vice versa.
    /// </summary>
    [Fact]
    public void FilterValuePlaceholdersMatchDictionary_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            predicate =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildFilter(predicate);
                return ValidateValuePlaceholders(result);
            });

        Check.One(_config, property);
    }

    [Fact]
    public void FilterValuePlaceholdersMatchDictionary_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            predicate =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildFilter(predicate);
                return ValidateValuePlaceholders(result);
            });

        Check.One(_config, property);
    }

    [Fact]
    public void FilterValuePlaceholdersMatchDictionary_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            predicate =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildFilter(predicate);
                return ValidateValuePlaceholders(result);
            });

        Check.One(_config, property);
    }

    private static Property ValidateValuePlaceholders(FilterExpressionResult result)
    {
        // Extract all :filt_vN placeholders from the expression
        var placeholderPattern = new Regex(@":filt_v\d+");
        var placeholdersInExpression = placeholderPattern.Matches(result.Expression)
            .Select(m => m.Value)
            .ToHashSet();

        var placeholdersInDictionary = result.ExpressionAttributeValues.Keys.ToHashSet();

        // Check if all expression placeholders exist in dictionary
        var missingInDict = placeholdersInExpression.Except(placeholdersInDictionary).ToList();
        if (missingInDict.Count > 0)
        {
            return Prop.Label(
                false,
                $"Placeholders in expression missing from dictionary: {string.Join(", ", missingInDict)}");
        }

        // Check if all dictionary placeholders are used in expression
        var unusedInExpression = placeholdersInDictionary.Except(placeholdersInExpression).ToList();
        if (unusedInExpression.Count > 0)
        {
            return Prop.Label(
                false,
                $"Placeholders in dictionary not used in expression: {string.Join(", ", unusedInExpression)}");
        }

        return Prop.Label(true, "All value placeholders consistent");
    }

    #endregion

    #region PR-01.2 Invariant 4: Filter Name Aliases Match Dictionary

    /// <summary>
    /// Invariant: Every #filt_N in expression string has a corresponding
    /// entry in ExpressionAttributeNames, and vice versa.
    /// </summary>
    [Fact]
    public void FilterNameAliasesMatchDictionary_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            predicate =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildFilter(predicate);
                return ValidateNameAliases(result);
            });

        Check.One(_config, property);
    }

    [Fact]
    public void FilterNameAliasesMatchDictionary_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            predicate =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildFilter(predicate);
                return ValidateNameAliases(result);
            });

        Check.One(_config, property);
    }

    [Fact]
    public void FilterNameAliasesMatchDictionary_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            predicate =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildFilter(predicate);
                return ValidateNameAliases(result);
            });

        Check.One(_config, property);
    }

    private static Property ValidateNameAliases(FilterExpressionResult result)
    {
        // Extract all #filt_N aliases from the expression
        var aliasPattern = new Regex(@"#filt_\d+");
        var aliasesInExpression = aliasPattern.Matches(result.Expression)
            .Select(m => m.Value)
            .ToHashSet();

        var aliasesInDictionary = result.ExpressionAttributeNames.Keys.ToHashSet();

        // Check if all expression aliases exist in dictionary
        var missingInDict = aliasesInExpression.Except(aliasesInDictionary).ToList();
        if (missingInDict.Count > 0)
        {
            return Prop.Label(
                false,
                $"Aliases in expression missing from dictionary: {string.Join(", ", missingInDict)}");
        }

        // Check if all dictionary aliases are used in expression
        var unusedInExpression = aliasesInDictionary.Except(aliasesInExpression).ToList();
        if (unusedInExpression.Count > 0)
        {
            return Prop.Label(
                false,
                $"Aliases in dictionary not used in expression: {string.Join(", ", unusedInExpression)}");
        }

        return Prop.Label(true, "All name aliases consistent");
    }

    #endregion

    #region PR-01.2 Invariant 5: Filter and Condition Aliases Never Collide

    /// <summary>
    /// Invariant: Building same predicate as filter (#filt_) and condition (#cond_)
    /// produces disjoint alias sets.
    /// </summary>
    [Fact]
    public void FilterAndConditionAliasesNeverCollide_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Simple),
            predicate =>
            {
                var filterBuilder = CreateFilterBuilder();
                var conditionBuilder = CreateConditionBuilder();

                var filterResult = filterBuilder.BuildFilter(predicate);
                var conditionResult = conditionBuilder.BuildCondition(predicate);

                return ValidateScopeIsolation(filterResult, conditionResult);
            });

        Check.One(_config, property);
    }

    [Fact]
    public void FilterAndConditionAliasesNeverCollide_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Composite),
            predicate =>
            {
                var filterBuilder = CreateFilterBuilder();
                var conditionBuilder = CreateConditionBuilder();

                var filterResult = filterBuilder.BuildFilter(predicate);
                var conditionResult = conditionBuilder.BuildCondition(predicate);

                return ValidateScopeIsolation(filterResult, conditionResult);
            });

        Check.One(_config, property);
    }

    [Fact]
    public void FilterAndConditionAliasesNeverCollide_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.FilterPredicate(Complexity.Complex),
            predicate =>
            {
                var filterBuilder = CreateFilterBuilder();
                var conditionBuilder = CreateConditionBuilder();

                var filterResult = filterBuilder.BuildFilter(predicate);
                var conditionResult = conditionBuilder.BuildCondition(predicate);

                return ValidateScopeIsolation(filterResult, conditionResult);
            });

        Check.One(_config, property);
    }

    private static Property ValidateScopeIsolation(
        FilterExpressionResult filterResult,
        ConditionExpressionResult conditionResult)
    {
        // Check name alias prefixes
        foreach (var filterAlias in filterResult.ExpressionAttributeNames.Keys)
        {
            if (!filterAlias.StartsWith("#filt_"))
            {
                return Prop.Label(false, $"Filter alias '{filterAlias}' does not use #filt_ prefix");
            }
        }

        foreach (var conditionAlias in conditionResult.ExpressionAttributeNames.Keys)
        {
            if (!conditionAlias.StartsWith("#cond_"))
            {
                return Prop.Label(false, $"Condition alias '{conditionAlias}' does not use #cond_ prefix");
            }
        }

        // Check value placeholder prefixes
        foreach (var filterValue in filterResult.ExpressionAttributeValues.Keys)
        {
            if (!filterValue.StartsWith(":filt_v"))
            {
                return Prop.Label(false, $"Filter value placeholder '{filterValue}' does not use :filt_v prefix");
            }
        }

        foreach (var conditionValue in conditionResult.ExpressionAttributeValues.Keys)
        {
            if (!conditionValue.StartsWith(":cond_v"))
            {
                return Prop.Label(false, $"Condition value placeholder '{conditionValue}' does not use :cond_v prefix");
            }
        }

        // Verify no collisions - alias sets should be completely disjoint
        var filterNameAliases = filterResult.ExpressionAttributeNames.Keys.ToHashSet();
        var conditionNameAliases = conditionResult.ExpressionAttributeNames.Keys.ToHashSet();
        var nameCollisions = filterNameAliases.Intersect(conditionNameAliases).ToList();

        if (nameCollisions.Count > 0)
        {
            return Prop.Label(
                false,
                $"Name alias collision detected: {string.Join(", ", nameCollisions)}");
        }

        var filterValuePlaceholders = filterResult.ExpressionAttributeValues.Keys.ToHashSet();
        var conditionValuePlaceholders = conditionResult.ExpressionAttributeValues.Keys.ToHashSet();
        var valueCollisions = filterValuePlaceholders.Intersect(conditionValuePlaceholders).ToList();

        if (valueCollisions.Count > 0)
        {
            return Prop.Label(
                false,
                $"Value placeholder collision detected: {string.Join(", ", valueCollisions)}");
        }

        return Prop.Label(true, "No alias collisions between filter and condition scopes");
    }

    #endregion

    #region Helper Methods

    private FilterExpressionBuilder<TestEntity> CreateFilterBuilder()
    {
        return new FilterExpressionBuilder<TestEntity>(
            _resolverFactory,
            _converterRegistry);
    }

    private ConditionExpressionBuilder<TestEntity> CreateConditionBuilder()
    {
        return new ConditionExpressionBuilder<TestEntity>(
            _resolverFactory,
            _converterRegistry);
    }

    private FilterExpressionBuilder<TestEntity> CreateBuilder() => CreateFilterBuilder();

    #endregion
}
