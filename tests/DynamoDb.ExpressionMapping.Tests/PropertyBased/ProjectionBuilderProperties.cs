using System.Linq.Expressions;
using System.Text.RegularExpressions;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;
using FsCheck;
using FsCheck.Fluent;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased;

/// <summary>
/// Property-based tests for ProjectionBuilder.
/// Verifies invariants PR-01.1: alias prefixing, reserved keyword aliasing, and well-formedness.
/// </summary>
[Trait("Category", "Property")]
public class ProjectionBuilderProperties
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly ReservedKeywordRegistry _reservedKeywords;
    private readonly Config _config;

    public ProjectionBuilderProperties()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _reservedKeywords = ReservedKeywordRegistry.Default;
        _config = Config.Quick.WithMaxTest(PropertyTestConfig.MaxTest);
    }

    #region PR-01.1 Invariant 1: Projection Never Produces Empty Alias for Reserved Keyword

    /// <summary>
    /// Invariant: If any projected attribute is a reserved keyword,
    /// the result must contain an alias for it.
    /// Simple projections.
    /// </summary>
    [Fact]
    public void ProjectionNeverProducesEmptyAliasForReservedKeyword_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.ProjectionSelector(Complexity.Simple),
            selector =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildProjection(selector);

                // Check if any resolved attribute names are reserved keywords
                var reservedKeywords = result.ResolvedAttributeNames
                    .Where(name => _reservedKeywords.IsReserved(name))
                    .ToList();

                if (reservedKeywords.Count == 0)
                    return Prop.Label(true, "No reserved keywords"); // No reserved keywords = no requirement

                // Reserved keyword detected: must have at least one alias
                if (result.ExpressionAttributeNames.Count == 0)
                {
                    return Prop.Label(
                        false,
                        $"Reserved keywords {string.Join(", ", reservedKeywords)} found, " +
                        $"but no aliases generated. Expression: {result.ProjectionExpression}");
                }

                // Verify each reserved keyword has an alias
                foreach (var keyword in reservedKeywords)
                {
                    if (!result.ExpressionAttributeNames.Values.Contains(keyword))
                    {
                        return Prop.Label(false, $"Reserved keyword '{keyword}' not aliased");
                    }
                }

                return Prop.Label(true, "All reserved keywords aliased");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for composite projections.
    /// </summary>
    [Fact]
    public void ProjectionNeverProducesEmptyAliasForReservedKeyword_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.ProjectionSelector(Complexity.Composite),
            selector =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildProjection(selector);

                // For each resolved attribute name that is a reserved keyword,
                // verify it appears in ExpressionAttributeNames
                foreach (var attributeName in result.ResolvedAttributeNames)
                {
                    if (_reservedKeywords.IsReserved(attributeName))
                    {
                        if (!result.ExpressionAttributeNames.Values.Contains(attributeName))
                        {
                            return Prop.Label(
                                false,
                                $"Reserved keyword '{attributeName}' not aliased in projection: {result.ProjectionExpression}");
                        }
                    }
                }

                return Prop.Label(true, "All reserved keywords aliased");
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for complex projections (nested properties).
    /// </summary>
    [Fact]
    public void ProjectionNeverProducesEmptyAliasForReservedKeyword_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.ProjectionSelector(Complexity.Complex),
            selector =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildProjection(selector);

                // For each path segment that is a reserved keyword, ensure it's aliased
                foreach (var path in result.PropertyPaths)
                {
                    foreach (var segment in path.Segments)
                    {
                        if (_reservedKeywords.IsReserved(segment))
                        {
                            // Check that this segment appears as a value in ExpressionAttributeNames
                            if (!result.ExpressionAttributeNames.Values.Contains(segment))
                            {
                                return Prop.Label(
                                    false,
                                    $"Reserved keyword segment '{segment}' in path not aliased");
                            }
                        }
                    }
                }

                return Prop.Label(true, "All reserved keyword segments aliased");
            });

        Check.One(_config, property);
    }

    #endregion

    #region PR-01.1 Invariant 2: Projection Expression is Valid Comma-Separated List

    /// <summary>
    /// Invariant: Output is always empty or a comma-separated list of
    /// valid attribute names / alias references / dotted paths.
    /// </summary>
    [Fact]
    public void ProjectionExpressionIsValidCommaSeparatedList_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.ProjectionSelector(Complexity.Simple),
            selector =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildProjection(selector);
                return ValidateCommaSeparatedFormat(result);
            });

        Check.One(_config, property);
    }

    [Fact]
    public void ProjectionExpressionIsValidCommaSeparatedList_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.ProjectionSelector(Complexity.Composite),
            selector =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildProjection(selector);
                return ValidateCommaSeparatedFormat(result);
            });

        Check.One(_config, property);
    }

    [Fact]
    public void ProjectionExpressionIsValidCommaSeparatedList_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.ProjectionSelector(Complexity.Complex),
            selector =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildProjection(selector);
                return ValidateCommaSeparatedFormat(result);
            });

        Check.One(_config, property);
    }

    private static Property ValidateCommaSeparatedFormat(ProjectionResult result)
    {
        if (string.IsNullOrEmpty(result.ProjectionExpression))
            return Prop.Label(true, "Empty projection is valid"); // Empty is valid (identity projection)

        var fragments = result.ProjectionExpression.Split(',', StringSplitOptions.TrimEntries);

        foreach (var fragment in fragments)
        {
            if (string.IsNullOrWhiteSpace(fragment))
            {
                return Prop.Label(
                    false,
                    $"Empty fragment in projection: {result.ProjectionExpression}");
            }

            var segments = fragment.Split('.');
            foreach (var segment in segments)
            {
                if (!IsValidSegment(segment))
                {
                    return Prop.Label(
                        false,
                        $"Invalid segment '{segment}' in projection: {result.ProjectionExpression}");
                }
            }
        }

        return Prop.Label(true, "Valid comma-separated format");
    }

    private static bool IsValidSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return false;

        // Alias reference: starts with #, followed by alphanumeric/underscore
        if (segment.StartsWith('#'))
            return Regex.IsMatch(segment, @"^#[a-zA-Z0-9_]+$");

        // Attribute name: alphanumeric, underscore, hyphen
        return Regex.IsMatch(segment, @"^[a-zA-Z0-9_-]+$");
    }

    #endregion

    #region PR-01.1 Invariant 3: Projection Aliases Always Use #proj_ Prefix

    /// <summary>
    /// Invariant: Every key in ExpressionAttributeNames starts with #proj_.
    /// </summary>
    [Fact]
    public void ProjectionAliasesAlwaysUseProjPrefix_Simple()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.ProjectionSelector(Complexity.Simple),
            selector =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildProjection(selector);
                return ValidateAliasPrefixes(result);
            });

        Check.One(_config, property);
    }

    [Fact]
    public void ProjectionAliasesAlwaysUseProjPrefix_Composite()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.ProjectionSelector(Complexity.Composite),
            selector =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildProjection(selector);
                return ValidateAliasPrefixes(result);
            });

        Check.One(_config, property);
    }

    [Fact]
    public void ProjectionAliasesAlwaysUseProjPrefix_Complex()
    {
        var property = Prop.ForAll(
            ExpressionGenerators.ProjectionSelector(Complexity.Complex),
            selector =>
            {
                var builder = CreateBuilder();
                var result = builder.BuildProjection(selector);
                return ValidateAliasPrefixes(result);
            });

        Check.One(_config, property);
    }

    private static Property ValidateAliasPrefixes(ProjectionResult result)
    {
        if (result.ExpressionAttributeNames.Count == 0)
            return Prop.Label(true, "No aliases to validate"); // No aliases = no requirement

        // Every alias key must start with #proj_
        foreach (var key in result.ExpressionAttributeNames.Keys)
        {
            if (!key.StartsWith("#proj_"))
            {
                return Prop.Label(false, $"Alias '{key}' does not start with #proj_");
            }
        }

        return Prop.Label(true, "All aliases use #proj_ prefix");
    }

    #endregion

    #region Helper Methods

    private ProjectionBuilder<TestEntity> CreateBuilder()
    {
        return new ProjectionBuilder<TestEntity>(
            _resolverFactory,
            _reservedKeywords,
            NullExpressionCache.Instance); // Disable caching for property tests
    }

    #endregion
}
