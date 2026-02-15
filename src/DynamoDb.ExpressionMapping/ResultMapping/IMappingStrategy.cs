using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.ResultMapping;

/// <summary>
/// Strategy pattern for building mapping delegates based on projection shape.
/// Each strategy handles a specific projection pattern:
/// - Identity: p => p
/// - SingleProperty: p => p.Foo
/// - Composite: p => new { p.A, p.B }
/// </summary>
internal interface IMappingStrategy
{
    /// <summary>
    /// Builds a compiled delegate that maps AttributeValue dictionaries to TResult.
    /// </summary>
    /// <typeparam name="TSource">Source entity type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="selector">The projection expression</param>
    /// <returns>Compiled mapping function</returns>
    Func<Dictionary<string, AttributeValue>, TResult> BuildMapper<TSource, TResult>(
        Expression<Func<TSource, TResult>> selector);
}
