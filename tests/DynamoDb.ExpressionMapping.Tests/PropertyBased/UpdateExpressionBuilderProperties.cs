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
/// Property-based tests for UpdateExpressionBuilder.
/// Verifies invariants PR-01.3: well-formed clauses and correct alias prefixes.
/// </summary>
[Trait("Category", "Property")]
public class UpdateExpressionBuilderProperties
{
    private static readonly Dictionary<string, Regex> ClauseKeywordRegexes = new()
    {
        ["SET"] = new Regex(@"\bSET\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["REMOVE"] = new Regex(@"\bREMOVE\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["ADD"] = new Regex(@"\bADD\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["DELETE"] = new Regex(@"\bDELETE\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    };
    private static readonly Regex UppercaseWordRegex = new(@"\b[A-Z]+\b", RegexOptions.Compiled);
    private static readonly Regex ClausePatternRegex = new(@"^(SET|REMOVE|ADD|DELETE)\s|(\s)(SET|REMOVE|ADD|DELETE)\s", RegexOptions.Compiled);
    private static readonly Regex ClauseStartRegex = new(@"^(SET|REMOVE|ADD|DELETE)\s", RegexOptions.Compiled);
    private static readonly Regex UpdNameAliasRegex = new(@"#upd_\d+", RegexOptions.Compiled);
    private static readonly Regex UpdValuePlaceholderRegex = new(@":upd_v\d+", RegexOptions.Compiled);

    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;
    private readonly Config _config;

    public UpdateExpressionBuilderProperties()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
        _config = Config.Quick.WithMaxTest(PropertyTestConfig.MaxTest);
    }

    #region PR-01.3 Invariant 1: Update Clauses Are Well-Formed

    /// <summary>
    /// Invariant: Output contains only valid clause keywords: SET, REMOVE, ADD, DELETE.
    /// Each clause keyword appears at most once.
    /// Simple operations.
    /// </summary>
    [Fact]
    public void UpdateClausesAreWellFormed_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.UpdateOperation(Complexity.Simple),
            operations =>
            {
                var builder = CreateBuilder();
                operations(builder);
                var result = builder.Build();

                return ValidateClauseWellFormedness(result);
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for composite operations.
    /// </summary>
    [Fact]
    public void UpdateClausesAreWellFormed_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.UpdateOperation(Complexity.Composite),
            operations =>
            {
                var builder = CreateBuilder();
                operations(builder);
                var result = builder.Build();

                return ValidateClauseWellFormedness(result);
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for complex operations (mixed clauses).
    /// </summary>
    [Fact]
    public void UpdateClausesAreWellFormed_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.UpdateOperation(Complexity.Complex),
            operations =>
            {
                var builder = CreateBuilder();
                operations(builder);
                var result = builder.Build();

                return ValidateClauseWellFormedness(result);
            });

        Check.One(_config, property);
    }

    private static Property ValidateClauseWellFormedness(UpdateExpressionResult result)
    {
        if (result.IsEmpty)
        {
            return Prop.Label(true, "Empty result (no operations)");
        }

        var expression = result.Expression;
        var validKeywords = new[] { "SET", "REMOVE", "ADD", "DELETE" };

        // Each valid keyword should appear at most once
        foreach (var keyword in validKeywords)
        {
            var regex = ClauseKeywordRegexes[keyword];
            var matches = regex.Matches(expression);

            if (matches.Count > 1)
            {
                return Prop.Label(
                    false,
                    $"Clause keyword '{keyword}' appears {matches.Count} times (should appear at most once). Expression: {expression}");
            }
        }

        // Extract all potential clause keywords from the expression
        var keywords = UppercaseWordRegex.Matches(expression)
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct()
            .ToList();

        // Verify all keywords are valid
        foreach (var keyword in keywords)
        {
            if (!validKeywords.Contains(keyword))
            {
                return Prop.Label(
                    false,
                    $"Invalid clause keyword '{keyword}' found. Expression: {expression}");
            }
        }

        // Verify basic structure: clause keywords should be at the start or after a space
        // and followed by a space or end of string
        if (!ClausePatternRegex.IsMatch(expression))
        {
            // Check if it's a single clause at the beginning
            if (!ClauseStartRegex.IsMatch(expression))
            {
                return Prop.Label(
                    false,
                    $"Expression does not start with a valid clause keyword. Expression: {expression}");
            }
        }

        return Prop.Label(true, "All clauses well-formed");
    }

    #endregion

    #region PR-01.3 Invariant 2: Update Aliases Always Use 'upd' Prefix

    /// <summary>
    /// Invariant: Every alias uses #upd_ / :upd_v prefix.
    /// Simple operations.
    /// </summary>
    [Fact]
    public void UpdateAliasesAlwaysUseUpdPrefix_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.UpdateOperation(Complexity.Simple),
            operations =>
            {
                var builder = CreateBuilder();
                operations(builder);
                var result = builder.Build();

                return ValidateAliasPrefix(result);
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for composite operations.
    /// </summary>
    [Fact]
    public void UpdateAliasesAlwaysUseUpdPrefix_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.UpdateOperation(Complexity.Composite),
            operations =>
            {
                var builder = CreateBuilder();
                operations(builder);
                var result = builder.Build();

                return ValidateAliasPrefix(result);
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for complex operations (mixed clauses).
    /// </summary>
    [Fact]
    public void UpdateAliasesAlwaysUseUpdPrefix_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.UpdateOperation(Complexity.Complex),
            operations =>
            {
                var builder = CreateBuilder();
                operations(builder);
                var result = builder.Build();

                return ValidateAliasPrefix(result);
            });

        Check.One(_config, property);
    }

    private static Property ValidateAliasPrefix(UpdateExpressionResult result)
    {
        if (result.IsEmpty)
        {
            return Prop.Label(true, "Empty result (no operations)");
        }

        // Validate attribute name aliases (should start with #upd_)
        foreach (var alias in result.ExpressionAttributeNames.Keys)
        {
            if (!alias.StartsWith("#upd_"))
            {
                return Prop.Label(
                    false,
                    $"Attribute name alias '{alias}' does not start with '#upd_'. Expression: {result.Expression}");
            }
        }

        // Validate attribute value placeholders (should start with :upd_v)
        foreach (var placeholder in result.ExpressionAttributeValues.Keys)
        {
            if (!placeholder.StartsWith(":upd_v"))
            {
                return Prop.Label(
                    false,
                    $"Attribute value placeholder '{placeholder}' does not start with ':upd_v'. Expression: {result.Expression}");
            }
        }

        // Verify all aliases in the expression match the dictionaries
        var nameAliasesInExpression = UpdNameAliasRegex.Matches(result.Expression)
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct()
            .ToHashSet();

        var valuePlaceholdersInExpression = UpdValuePlaceholderRegex.Matches(result.Expression)
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct()
            .ToHashSet();

        // Every alias in the expression should exist in the dictionary
        foreach (var alias in nameAliasesInExpression)
        {
            if (!result.ExpressionAttributeNames.ContainsKey(alias))
            {
                return Prop.Label(
                    false,
                    $"Name alias '{alias}' found in expression but not in ExpressionAttributeNames. Expression: {result.Expression}");
            }
        }

        foreach (var placeholder in valuePlaceholdersInExpression)
        {
            if (!result.ExpressionAttributeValues.ContainsKey(placeholder))
            {
                return Prop.Label(
                    false,
                    $"Value placeholder '{placeholder}' found in expression but not in ExpressionAttributeValues. Expression: {result.Expression}");
            }
        }

        // Every dictionary entry should appear in the expression
        foreach (var alias in result.ExpressionAttributeNames.Keys)
        {
            if (!nameAliasesInExpression.Contains(alias))
            {
                return Prop.Label(
                    false,
                    $"Name alias '{alias}' in ExpressionAttributeNames but not found in expression. Expression: {result.Expression}");
            }
        }

        foreach (var placeholder in result.ExpressionAttributeValues.Keys)
        {
            if (!valuePlaceholdersInExpression.Contains(placeholder))
            {
                return Prop.Label(
                    false,
                    $"Value placeholder '{placeholder}' in ExpressionAttributeValues but not found in expression. Expression: {result.Expression}");
            }
        }

        return Prop.Label(true, "All aliases use correct 'upd' prefix and are consistent");
    }

    #endregion

    #region Helper Methods

    private UpdateExpressionBuilder<TestEntity> CreateBuilder()
    {
        return new UpdateExpressionBuilder<TestEntity>(_resolverFactory, _converterRegistry);
    }

    #endregion
}
