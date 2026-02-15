using System.Linq.Expressions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FsCheck;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;

/// <summary>
/// Factory methods for generating random expression trees for property-based testing.
/// All generators produce valid expression trees targeting TestEntity or TestKeyedEntity.
/// </summary>
public static class ExpressionGenerators
{
    /// <summary>
    /// Generates random projection selectors for TestEntity.
    /// Produces: Arbitrary&lt;Expression&lt;Func&lt;TestEntity, object&gt;&gt;&gt;
    /// Covers: single properties, nested properties (e.g., x.Address.City), reserved keywords.
    /// </summary>
    /// <param name="complexity">Tier: Simple (single property), Composite (2-3 properties), Complex (nested + multiple).</param>
    public static Arbitrary<Expression<Func<TestEntity, object>>> ProjectionSelector(Complexity complexity = Complexity.Simple)
    {
        return ProjectionSelectorGenerator.Generate(complexity);
    }

    /// <summary>
    /// Generates random filter predicates for TestEntity.
    /// Produces: Arbitrary&lt;Expression&lt;Func&lt;TestEntity, bool&gt;&gt;&gt;
    /// Combines: comparison operators (==, >, etc.), logical operators (&amp;&amp;, ||, !), DynamoDB functions (.StartsWith(), .Contains()).
    /// </summary>
    /// <param name="complexity">Tier: Simple (single comparison), Composite (2-3 combined), Complex (nested + functions + NOT).</param>
    public static Arbitrary<Expression<Func<TestEntity, bool>>> FilterPredicate(Complexity complexity = Complexity.Simple)
    {
        // TODO: Implementation in task 1.4
        throw new NotImplementedException("FilterPredicateGenerator - see task 1.4");
    }

    /// <summary>
    /// Generates random update operation sequences for TestEntity.
    /// Produces: Arbitrary&lt;Action&lt;UpdateExpressionBuilder&lt;TestEntity&gt;&gt;&gt;
    /// Combines: SET, REMOVE, ADD, DELETE operations chained via fluent API.
    /// </summary>
    /// <param name="complexity">Tier: Simple (single operation), Composite (2-3 operations), Complex (mixed clauses).</param>
    public static Arbitrary<Action<UpdateExpressionBuilder<TestEntity>>> UpdateOperation(Complexity complexity = Complexity.Simple)
    {
        // TODO: Implementation in task 1.5
        throw new NotImplementedException("UpdateOperationGenerator - see task 1.5");
    }

    /// <summary>
    /// Generates random key condition predicates for TestKeyedEntity.
    /// Produces: Arbitrary&lt;Expression&lt;Func&lt;TestKeyedEntity, bool&gt;&gt;&gt;
    /// Ensures: Partition key equality present, optional sort key conditions (begins_with, between, comparison).
    /// </summary>
    /// <param name="complexity">Tier: Simple (PK only), Composite (PK + SK comparison), Complex (PK + SK range/begins_with).</param>
    public static Arbitrary<Expression<Func<TestKeyedEntity, bool>>> KeyConditionPredicate(Complexity complexity = Complexity.Simple)
    {
        // TODO: Implementation for KeyConditionExpressionBuilder tests
        throw new NotImplementedException("KeyConditionPredicateGenerator - deferred");
    }
}

/// <summary>
/// Complexity tiers for expression generation.
/// </summary>
public enum Complexity
{
    /// <summary>Simple: Single property access/comparison (x => x.Name == "foo").</summary>
    Simple,

    /// <summary>Composite: 2-3 combined predicates (x => x.Name == "foo" &amp;&amp; x.Count > 5).</summary>
    Composite,

    /// <summary>Complex: Nested properties + functions + NOT (x => x.Address.City.StartsWith("L") &amp;&amp; !(x.Count > 10)).</summary>
    Complex
}
