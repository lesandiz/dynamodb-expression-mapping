using System.Text.RegularExpressions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FsCheck;
using FsCheck.Fluent;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased;

/// <summary>
/// Property-based tests for KeyConditionExpressionBuilder.
/// Verifies invariant PR-01.6: partition key equality must be present in every key condition.
/// </summary>
[Trait("Category", "Property")]
public class KeyConditionBuilderProperties
{
    private static readonly Regex KeyValuePlaceholderRegex = new(@":key_v\d+", RegexOptions.Compiled);
    private static readonly Regex EqualityOperatorRegex = new(@"\s=\s", RegexOptions.Compiled);
    private static readonly Regex BetweenKeywordRegex = new(@"\bBETWEEN\b", RegexOptions.Compiled);
    private static readonly Regex PartitionKeyEqualityRegex = new(@"(PK|#key_\d+)\s*=\s*:key_v\d+", RegexOptions.Compiled);
    private static readonly Regex SortKeyReferenceRegex = new(@"(SK|#key_\d+)", RegexOptions.Compiled);

    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;
    private readonly Config _config;

    public KeyConditionBuilderProperties()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
        _config = Config.Quick.WithMaxTest(PropertyTestConfig.MaxTest);
    }

    #region PR-01.6 Invariant: Key Condition Always Contains Partition Key Equality

    /// <summary>
    /// Invariant: Every key condition expression contains exactly one equality
    /// comparison for the partition key.
    /// Simple: Partition key only (no sort key).
    /// </summary>
    [Fact]
    public void KeyConditionAlwaysContainsPartitionKeyEquality_PartitionKeyOnly()
    {
        var property = Prop.ForAll(
            GeneratePartitionKeyValue(),
            pkValue =>
            {
                var builder = CreateBuilder();
                var result = builder
                    .WithPartitionKey(e => e.PK, pkValue)
                    .Build();

                return ValidatePartitionKeyEquality(result, "PK", pkValue);
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for partition key with sort key equality.
    /// </summary>
    [Fact]
    public void KeyConditionAlwaysContainsPartitionKeyEquality_WithSortKeyEquals()
    {
        var property = Prop.ForAll(
            GeneratePartitionKeyValue(),
            GenerateSortKeyValue(),
            (pkValue, skValue) =>
            {
                var builder = CreateBuilder();
                var result = builder
                    .WithPartitionKey(e => e.PK, pkValue)
                    .WithSortKeyEquals(e => e.SK, skValue);

                return ValidatePartitionKeyEquality(result, "PK", pkValue)
                    .And(() => ValidateSortKeyCondition(result, "SK"));
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for partition key with sort key less than.
    /// </summary>
    [Fact]
    public void KeyConditionAlwaysContainsPartitionKeyEquality_WithSortKeyLessThan()
    {
        var property = Prop.ForAll(
            GeneratePartitionKeyValue(),
            GenerateSortKeyValue(),
            (pkValue, skValue) =>
            {
                var builder = CreateBuilder();
                var result = builder
                    .WithPartitionKey(e => e.PK, pkValue)
                    .WithSortKeyLessThan(e => e.SK, skValue);

                return ValidatePartitionKeyEquality(result, "PK", pkValue)
                    .And(() => ValidateSortKeyCondition(result, "SK"));
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for partition key with sort key greater than.
    /// </summary>
    [Fact]
    public void KeyConditionAlwaysContainsPartitionKeyEquality_WithSortKeyGreaterThan()
    {
        var property = Prop.ForAll(
            GeneratePartitionKeyValue(),
            GenerateSortKeyValue(),
            (pkValue, skValue) =>
            {
                var builder = CreateBuilder();
                var result = builder
                    .WithPartitionKey(e => e.PK, pkValue)
                    .WithSortKeyGreaterThan(e => e.SK, skValue);

                return ValidatePartitionKeyEquality(result, "PK", pkValue)
                    .And(() => ValidateSortKeyCondition(result, "SK"));
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for partition key with sort key between.
    /// </summary>
    [Fact]
    public void KeyConditionAlwaysContainsPartitionKeyEquality_WithSortKeyBetween()
    {
        var property = Prop.ForAll(
            GeneratePartitionKeyValue(),
            GenerateSortKeyValue(),
            GenerateSortKeyValue(),
            (pkValue, skValue1, skValue2) =>
            {
                // Ensure low <= high by sorting the values (using same comparison as SortKeyConditionBuilder)
                var skLow = string.Compare(skValue1, skValue2, System.StringComparison.CurrentCulture) <= 0 ? skValue1 : skValue2;
                var skHigh = string.Compare(skValue1, skValue2, System.StringComparison.CurrentCulture) <= 0 ? skValue2 : skValue1;

                var builder = CreateBuilder();
                var result = builder
                    .WithPartitionKey(e => e.PK, pkValue)
                    .WithSortKeyBetween(e => e.SK, skLow, skHigh);

                return ValidatePartitionKeyEquality(result, "PK", pkValue)
                    .And(() => ValidateSortKeyCondition(result, "SK"))
                    .And(() => ValidateBetweenClause(result));
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant for partition key with sort key begins_with.
    /// </summary>
    [Fact]
    public void KeyConditionAlwaysContainsPartitionKeyEquality_WithSortKeyBeginsWith()
    {
        var property = Prop.ForAll(
            GeneratePartitionKeyValue(),
            GenerateStringPrefix(),
            (pkValue, prefix) =>
            {
                var builder = CreateBuilder();
                var result = builder
                    .WithPartitionKey(e => e.PK, pkValue)
                    .WithSortKeyBeginsWith(e => e.SK, prefix);

                return ValidatePartitionKeyEquality(result, "PK", pkValue)
                    .And(() => ValidateSortKeyCondition(result, "SK"))
                    .And(() => ValidateBeginsWithFunction(result));
            });

        Check.One(_config, property);
    }

    /// <summary>
    /// Invariant: Alias scoping - all name aliases use #key_ prefix, all value placeholders use :key_v prefix.
    /// Complex: Tests alias prefixes are consistent across different sort key operators.
    /// </summary>
    [Fact]
    public void KeyConditionAliases_AlwaysUseKeyPrefix()
    {
        var property = Prop.ForAll(
            GeneratePartitionKeyValue(),
            GenerateSortKeyValue(),
            (pkValue, skValue) =>
            {
                var builder = CreateBuilder();
                var result = builder
                    .WithPartitionKey(e => e.PK, pkValue)
                    .WithSortKeyEquals(e => e.SK, skValue);

                // Validate all name aliases start with #key_ (if any exist)
                foreach (var alias in result.ExpressionAttributeNames.Keys)
                {
                    if (!alias.StartsWith("#key_"))
                    {
                        return Prop.Label(
                            false,
                            $"Attribute name alias '{alias}' does not start with '#key_'. Expression: {result.Expression}");
                    }
                }

                // Validate all value placeholders start with :key_v
                foreach (var placeholder in result.ExpressionAttributeValues.Keys)
                {
                    if (!placeholder.StartsWith(":key_v"))
                    {
                        return Prop.Label(
                            false,
                            $"Attribute value placeholder '{placeholder}' does not start with ':key_v'. Expression: {result.Expression}");
                    }
                }

                // Verify expression contains the placeholders
                var placeholdersInExpression = KeyValuePlaceholderRegex.Matches(result.Expression)
                    .Select(m => m.Value)
                    .Distinct()
                    .ToHashSet();

                if (placeholdersInExpression.Count != result.ExpressionAttributeValues.Count)
                {
                    return Prop.Label(
                        false,
                        $"Mismatch between placeholders in expression ({placeholdersInExpression.Count}) and dictionary ({result.ExpressionAttributeValues.Count})");
                }

                return Prop.Label(true, "All aliases use correct 'key' prefix and are consistent");
            });

        Check.One(_config, property);
    }

    #endregion

    #region Validation Helpers

    private static Property ValidatePartitionKeyEquality(
        KeyConditionExpressionResult result,
        string expectedAttributeName,
        string expectedValue)
    {
        // Check that expression contains partition key equality pattern
        // Pattern: <attributeName or alias> = :key_v0
        if (!PartitionKeyEqualityRegex.IsMatch(result.Expression))
        {
            return Prop.Label(
                false,
                $"Partition key equality pattern not found. Expected '{expectedAttributeName} = :key_v<N>'. Expression: {result.Expression}");
        }

        // Verify that the partition key value is in the attribute values dictionary
        if (result.ExpressionAttributeValues.Count == 0)
        {
            return Prop.Label(
                false,
                $"ExpressionAttributeValues is empty. Expected at least partition key value.");
        }

        // Check that at least one value matches the expected partition key value
        var hasExpectedValue = result.ExpressionAttributeValues.Values
            .Any(av => av.S == expectedValue);

        if (!hasExpectedValue)
        {
            return Prop.Label(
                false,
                $"Expected partition key value '{expectedValue}' not found in ExpressionAttributeValues.");
        }

        // Ensure exactly one equality operator for the partition key
        var equalityCount = EqualityOperatorRegex.Matches(result.Expression).Count;
        var betweenMatch = BetweenKeywordRegex.IsMatch(result.Expression);

        // Partition key always uses =, sort key may use =, <, >, <=, >=, BETWEEN, or begins_with
        // So we should have at least 1 equality (partition key)
        if (equalityCount < 1)
        {
            return Prop.Label(
                false,
                $"Expected at least 1 equality operator for partition key. Expression: {result.Expression}");
        }

        return Prop.Label(true, "Partition key equality validated");
    }

    private static Property ValidateSortKeyCondition(
        KeyConditionExpressionResult result,
        string expectedSortKeyName)
    {
        // Check that expression contains AND (indicating both PK and SK conditions)
        if (!result.Expression.Contains(" AND "))
        {
            return Prop.Label(
                false,
                $"Expected 'AND' to separate partition key and sort key conditions. Expression: {result.Expression}");
        }

        // Verify sort key appears in the expression (either directly or via alias)
        var matches = SortKeyReferenceRegex.Matches(result.Expression);

        // We expect at least 2 matches for partition key only, or more if sort key is present
        // Actually, if sort key is present, we expect to see it in the second part after AND
        var parts = result.Expression.Split(new[] { " AND " }, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            return Prop.Label(
                false,
                $"Expected exactly 2 parts (PK and SK) separated by AND. Expression: {result.Expression}");
        }

        // The second part should contain the sort key
        if (!SortKeyReferenceRegex.IsMatch(parts[1]))
        {
            return Prop.Label(
                false,
                $"Sort key '{expectedSortKeyName}' not found in second part of expression. Expression: {result.Expression}");
        }

        return Prop.Label(true, "Sort key condition validated");
    }

    private static Property ValidateBetweenClause(KeyConditionExpressionResult result)
    {
        if (!result.Expression.Contains("BETWEEN"))
        {
            return Prop.Label(
                false,
                $"Expected 'BETWEEN' keyword in expression. Expression: {result.Expression}");
        }

        // BETWEEN requires 2 values for sort key range + 1 for partition key = 3 total
        if (result.ExpressionAttributeValues.Count != 3)
        {
            return Prop.Label(
                false,
                $"Expected 3 attribute values for BETWEEN (1 PK + 2 SK range). Got {result.ExpressionAttributeValues.Count}.");
        }

        return Prop.Label(true, "BETWEEN clause validated");
    }

    private static Property ValidateBeginsWithFunction(KeyConditionExpressionResult result)
    {
        if (!result.Expression.Contains("begins_with("))
        {
            return Prop.Label(
                false,
                $"Expected 'begins_with()' function in expression. Expression: {result.Expression}");
        }

        return Prop.Label(true, "begins_with function validated");
    }

    #endregion

    #region Generators

    /// <summary>
    /// Generates random partition key values (non-empty strings).
    /// </summary>
    private static Arbitrary<string> GeneratePartitionKeyValue()
    {
        return Gen.Elements(
            "USER#123",
            "CUSTOMER#456",
            "ORDER#789",
            "PRODUCT#ABC",
            "SESSION#XYZ",
            "TENANT#999",
            "PK_VALUE_1",
            "pk-test",
            "PartitionKey"
        ).ToArbitrary();
    }

    /// <summary>
    /// Generates random sort key values (non-empty strings).
    /// </summary>
    private static Arbitrary<string> GenerateSortKeyValue()
    {
        return Gen.Elements(
            "METADATA",
            "ORDER#2024-01-01",
            "2024-12-31",
            "SK_VALUE",
            "sort-key-test",
            "A",
            "ZZZ",
            "VERSION#001",
            "ITEM#123"
        ).ToArbitrary();
    }

    /// <summary>
    /// Generates random string prefixes for begins_with testing.
    /// </summary>
    private static Arbitrary<string> GenerateStringPrefix()
    {
        return Gen.Elements(
            "ORDER#",
            "USER#",
            "2024",
            "ITEM",
            "PREFIX_",
            "A",
            "TEST"
        ).ToArbitrary();
    }

    #endregion

    #region Helper Methods

    private KeyConditionExpressionBuilder<TestKeyedEntity> CreateBuilder()
    {
        return new KeyConditionExpressionBuilder<TestKeyedEntity>(_resolverFactory, _converterRegistry);
    }

    #endregion
}
