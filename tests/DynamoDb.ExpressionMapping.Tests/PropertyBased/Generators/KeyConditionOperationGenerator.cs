using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FsCheck;
using Arb = FsCheck.Fluent.Arb;
using Gen = FsCheck.Fluent.Gen;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;

/// <summary>
/// FsCheck generator for key condition operation sequences.
/// Generates Func&lt;KeyConditionExpressionBuilder&lt;TestKeyedEntity&gt;, KeyConditionExpressionResult&gt; across three complexity tiers.
/// </summary>
public static class KeyConditionOperationGenerator
{
    /// <summary>
    /// Generates random key condition operations for TestKeyedEntity.
    /// </summary>
    /// <param name="complexity">Tier: Simple (PK only), Composite (PK + SK comparison), Complex (PK + BETWEEN/begins_with).</param>
    public static Arbitrary<Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult>> Generate(Complexity complexity = Complexity.Simple)
    {
        var generator = complexity switch
        {
            Complexity.Simple => SimpleOperationGen(),
            Complexity.Composite => CompositeOperationGen(),
            Complexity.Complex => ComplexOperationGen(),
            _ => throw new ArgumentOutOfRangeException(nameof(complexity), complexity, "Invalid complexity tier")
        };

        return Arb.From(generator);
    }

    #region Value Generators

    private static Gen<string> PartitionKeyValueGen()
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
        );
    }

    private static Gen<string> SortKeyValueGen()
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
        );
    }

    private static Gen<string> StringPrefixGen()
    {
        return Gen.Elements(
            "ORDER#",
            "USER#",
            "2024",
            "ITEM",
            "PREFIX_",
            "A",
            "TEST"
        );
    }

    #endregion

    #region Simple Operations (PK Only)

    private static Gen<Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult>> SimpleOperationGen()
    {
        return Gen.Select(PartitionKeyValueGen(), pkValue =>
        {
            Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult> action =
                builder => builder.WithPartitionKey(e => e.PK, pkValue).Build();
            return action;
        });
    }

    #endregion

    #region Composite Operations (PK + SK Comparison)

    private static Gen<Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult>> CompositeOperationGen()
    {
        return Gen.SelectMany(PartitionKeyValueGen(), pkValue =>
            Gen.SelectMany(SortKeyValueGen(), skValue =>
                Gen.Select(Gen.Choose(0, 4), opIndex =>
                {
                    Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult> action = opIndex switch
                    {
                        0 => builder => builder.WithPartitionKey(e => e.PK, pkValue).WithSortKeyEquals(e => e.SK, skValue),
                        1 => builder => builder.WithPartitionKey(e => e.PK, pkValue).WithSortKeyLessThan(e => e.SK, skValue),
                        2 => builder => builder.WithPartitionKey(e => e.PK, pkValue).WithSortKeyLessThanOrEqual(e => e.SK, skValue),
                        3 => builder => builder.WithPartitionKey(e => e.PK, pkValue).WithSortKeyGreaterThan(e => e.SK, skValue),
                        _ => builder => builder.WithPartitionKey(e => e.PK, pkValue).WithSortKeyGreaterThanOrEqual(e => e.SK, skValue),
                    };
                    return action;
                })));
    }

    #endregion

    #region Complex Operations (PK + BETWEEN or begins_with)

    private static Gen<Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult>> ComplexOperationGen()
    {
        var betweenGen = Gen.SelectMany(PartitionKeyValueGen(), pkValue =>
            Gen.SelectMany(SortKeyValueGen(), skValue1 =>
                Gen.Select(SortKeyValueGen(), skValue2 =>
                {
                    // Sort to ensure low <= high (using CurrentCulture to match string.CompareTo)
                    var low = string.Compare(skValue1, skValue2, StringComparison.CurrentCulture) <= 0 ? skValue1 : skValue2;
                    var high = string.Compare(skValue1, skValue2, StringComparison.CurrentCulture) <= 0 ? skValue2 : skValue1;

                    Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult> action =
                        builder => builder.WithPartitionKey(e => e.PK, pkValue).WithSortKeyBetween(e => e.SK, low, high);
                    return action;
                })));

        var beginsWithGen = Gen.SelectMany(PartitionKeyValueGen(), pkValue =>
            Gen.Select(StringPrefixGen(), prefix =>
            {
                Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult> action =
                    builder => builder.WithPartitionKey(e => e.PK, pkValue).WithSortKeyBeginsWith(e => e.SK, prefix);
                return action;
            }));

        return Gen.OneOf(betweenGen, beginsWithGen);
    }

    #endregion
}
