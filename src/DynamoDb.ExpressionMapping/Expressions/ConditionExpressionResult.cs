using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// The result of building a DynamoDB ConditionExpression.
/// Immutable after construction.
/// </summary>
public sealed class ConditionExpressionResult
{
    /// <summary>
    /// The DynamoDB expression string.
    /// E.g. "#cond_0 = :cond_v0 AND #cond_1 = :cond_v1"
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// Attribute name aliases for reserved keywords.
    /// E.g. { "#cond_0": "Status", "#cond_1": "Enabled" }
    /// </summary>
    public IReadOnlyDictionary<string, string> ExpressionAttributeNames { get; }

    /// <summary>
    /// Attribute value placeholders.
    /// E.g. { ":cond_v0": { S: "Live" }, ":cond_v1": { BOOL: true } }
    /// </summary>
    public IReadOnlyDictionary<string, AttributeValue> ExpressionAttributeValues { get; }

    /// <summary>
    /// Whether this expression is empty (always-true predicate).
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(Expression);

    public ConditionExpressionResult(
        string expression,
        IReadOnlyDictionary<string, string> expressionAttributeNames,
        IReadOnlyDictionary<string, AttributeValue> expressionAttributeValues)
    {
        Expression = expression ?? string.Empty;
        ExpressionAttributeNames = expressionAttributeNames ?? new Dictionary<string, string>();
        ExpressionAttributeValues = expressionAttributeValues ?? new Dictionary<string, AttributeValue>();
    }

    /// <summary>
    /// Combines two condition expressions with logical AND.
    /// Re-aliases the right operand to prevent alias collisions.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A new ConditionExpressionResult with combined expressions.</returns>
    /// <exception cref="ArgumentNullException">Thrown if either operand is null.</exception>
    public static ConditionExpressionResult And(
        ConditionExpressionResult left,
        ConditionExpressionResult right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.IsEmpty) return right;
        if (right.IsEmpty) return left;

        var (rewrittenExpr, rewrittenNames, rewrittenValues) =
            ReAlias(right, left);

        var mergedNames = new Dictionary<string, string>(left.ExpressionAttributeNames);
        MergeAttributeNames(mergedNames, rewrittenNames);

        var mergedValues = new Dictionary<string, AttributeValue>(left.ExpressionAttributeValues);
        MergeAttributeValues(mergedValues, rewrittenValues);

        return new ConditionExpressionResult(
            expression: $"({left.Expression}) AND ({rewrittenExpr})",
            expressionAttributeNames: mergedNames,
            expressionAttributeValues: mergedValues);
    }

    /// <summary>
    /// Combines two condition expressions with logical OR.
    /// Re-aliases the right operand to prevent alias collisions.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A new ConditionExpressionResult with combined expressions.</returns>
    /// <exception cref="ArgumentNullException">Thrown if either operand is null.</exception>
    public static ConditionExpressionResult Or(
        ConditionExpressionResult left,
        ConditionExpressionResult right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.IsEmpty) return right;
        if (right.IsEmpty) return left;

        var (rewrittenExpr, rewrittenNames, rewrittenValues) =
            ReAlias(right, left);

        var mergedNames = new Dictionary<string, string>(left.ExpressionAttributeNames);
        MergeAttributeNames(mergedNames, rewrittenNames);

        var mergedValues = new Dictionary<string, AttributeValue>(left.ExpressionAttributeValues);
        MergeAttributeValues(mergedValues, rewrittenValues);

        return new ConditionExpressionResult(
            expression: $"({left.Expression}) OR ({rewrittenExpr})",
            expressionAttributeNames: mergedNames,
            expressionAttributeValues: mergedValues);
    }

    /// <summary>
    /// Re-aliases the source operand using the reference's max indices to prevent collisions.
    /// </summary>
    /// <param name="source">The operand to re-alias.</param>
    /// <param name="reference">The reference operand whose max indices determine the offset.</param>
    /// <returns>A tuple containing the rewritten expression, names, and values.</returns>
    private static (string Expression,
        IReadOnlyDictionary<string, string> Names,
        IReadOnlyDictionary<string, AttributeValue> Values)
        ReAlias(ConditionExpressionResult source, ConditionExpressionResult reference)
    {
        int nameOffset = MaxAliasIndex(reference.ExpressionAttributeNames.Keys, "#cond_") + 1;
        int valueOffset = MaxAliasIndex(reference.ExpressionAttributeValues.Keys, ":cond_v") + 1;

        var expr = source.Expression;
        var newNames = new Dictionary<string, string>();
        var newValues = new Dictionary<string, AttributeValue>();

        // Rewrite name aliases — process in descending index order
        foreach (var (oldKey, attr) in source.ExpressionAttributeNames
            .OrderByDescending(kvp => ExtractIndex(kvp.Key, "#cond_")))
        {
            int idx = ExtractIndex(oldKey, "#cond_");
            string newKey = $"#cond_{idx + nameOffset}";
            expr = expr.Replace(oldKey, newKey);
            newNames[newKey] = attr;
        }

        // Rewrite value aliases — process in descending index order
        foreach (var (oldKey, val) in source.ExpressionAttributeValues
            .OrderByDescending(kvp => ExtractIndex(kvp.Key, ":cond_v")))
        {
            int idx = ExtractIndex(oldKey, ":cond_v");
            string newKey = $":cond_v{idx + valueOffset}";
            expr = expr.Replace(oldKey, newKey);
            newValues[newKey] = val;
        }

        return (expr, newNames, newValues);
    }

    /// <summary>
    /// Finds the maximum index in a collection of alias keys with the given prefix.
    /// </summary>
    /// <param name="keys">The collection of alias keys.</param>
    /// <param name="prefix">The prefix to match (e.g., "#cond_" or ":cond_v").</param>
    /// <returns>The maximum index found, or -1 if no matching keys exist.</returns>
    private static int MaxAliasIndex(
        IEnumerable<string> keys, string prefix)
    {
        int max = -1;
        foreach (var key in keys)
        {
            int idx = ExtractIndex(key, prefix);
            if (idx > max) max = idx;
        }
        return max;
    }

    /// <summary>
    /// Extracts the integer index from an alias key.
    /// </summary>
    /// <param name="key">The alias key (e.g., "#cond_5" or ":cond_v3").</param>
    /// <param name="prefix">The prefix to remove (e.g., "#cond_" or ":cond_v").</param>
    /// <returns>The extracted index.</returns>
    private static int ExtractIndex(string key, string prefix)
    {
        // Returns the integer N from a key like "#cond_N" or ":cond_vN"
        return int.Parse(key[prefix.Length..]);
    }

    /// <summary>
    /// Merges source attribute names into target dictionary.
    /// Throws if a key already exists with a different value.
    /// </summary>
    /// <exception cref="ExpressionAttributeConflictException">
    /// Thrown when a placeholder key already exists with a different value.
    /// </exception>
    private static void MergeAttributeNames(
        Dictionary<string, string> target,
        IReadOnlyDictionary<string, string> source)
    {
        foreach (var kvp in source)
        {
            if (target.TryGetValue(kvp.Key, out var existing))
            {
                if (existing != kvp.Value)
                {
                    throw new ExpressionAttributeConflictException(kvp.Key, existing, kvp.Value);
                }
                continue; // Same mapping already exists
            }
            target[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Merges source attribute values into target dictionary.
    /// Throws if a key already exists.
    /// </summary>
    /// <exception cref="ExpressionAttributeConflictException">
    /// Thrown when a placeholder key already exists.
    /// </exception>
    private static void MergeAttributeValues(
        Dictionary<string, AttributeValue> target,
        IReadOnlyDictionary<string, AttributeValue> source)
    {
        foreach (var kvp in source)
        {
            if (target.TryGetValue(kvp.Key, out var existing))
            {
                throw new ExpressionAttributeConflictException(kvp.Key, existing.ToString()!);
            }
            target[kvp.Key] = kvp.Value;
        }
    }
}
