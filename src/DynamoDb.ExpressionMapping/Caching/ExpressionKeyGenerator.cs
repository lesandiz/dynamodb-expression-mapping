using System.Linq.Expressions;

namespace DynamoDb.ExpressionMapping.Caching;

/// <summary>
/// Generates structural hash keys for expression trees.
/// Two expressions with the same structure produce the same key,
/// regardless of where they were defined.
/// </summary>
public static class ExpressionKeyGenerator
{
    /// <summary>
    /// Generates a cache key from an expression tree's structure.
    /// Ignores captured variable VALUES (cache is shape-based, not value-based).
    /// </summary>
    /// <typeparam name="TSource">The source entity type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="expression">The expression to generate a key for.</param>
    /// <returns>A deterministic cache key based on expression structure.</returns>
    public static string GenerateKey<TSource, TResult>(Expression<Func<TSource, TResult>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Uses Expression.ToString() as baseline (captures structure)
        // Prefixed with source and result type names for disambiguation
        var sourceTypeName = typeof(TSource).FullName ?? typeof(TSource).Name;
        var resultTypeName = typeof(TResult).FullName ?? typeof(TResult).Name;

        return $"{sourceTypeName}→{resultTypeName}:{expression}";
    }
}
