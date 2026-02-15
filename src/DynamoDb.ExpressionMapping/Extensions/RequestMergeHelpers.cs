using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;

namespace DynamoDb.ExpressionMapping.Extensions;

/// <summary>
/// Internal helpers for safely merging ExpressionAttributeNames and
/// ExpressionAttributeValues dictionaries from multiple expression results.
/// </summary>
internal static class RequestMergeHelpers
{
    /// <summary>
    /// Merges source attribute names into the target dictionary.
    /// </summary>
    /// <param name="target">The target dictionary to merge into.</param>
    /// <param name="source">The source dictionary to merge from.</param>
    /// <exception cref="ExpressionAttributeConflictException">
    /// Thrown when a key exists in both dictionaries with different values (Spec 14 §4).
    /// </exception>
    internal static void MergeAttributeNames(
        Dictionary<string, string> target,
        IReadOnlyDictionary<string, string> source)
    {
        foreach (var kvp in source)
        {
            if (target.TryGetValue(kvp.Key, out var existing))
            {
                if (existing != kvp.Value)
                {
                    throw new ExpressionAttributeConflictException(
                        kvp.Key, existing, kvp.Value);
                }
                continue; // Same mapping already exists
            }
            target[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Merges source attribute values into the target dictionary.
    /// </summary>
    /// <param name="target">The target dictionary to merge into.</param>
    /// <param name="source">The source dictionary to merge from.</param>
    /// <exception cref="ExpressionAttributeConflictException">
    /// Thrown when a placeholder key already exists in the target (Spec 14 §4).
    /// </exception>
    internal static void MergeAttributeValues(
        Dictionary<string, AttributeValue> target,
        IReadOnlyDictionary<string, AttributeValue> source)
    {
        foreach (var kvp in source)
        {
            if (target.TryGetValue(kvp.Key, out var existing))
            {
                throw new ExpressionAttributeConflictException(
                    kvp.Key, existing.S ?? existing.N ?? "(value)", null);
            }
            target[kvp.Key] = kvp.Value;
        }
    }
}
