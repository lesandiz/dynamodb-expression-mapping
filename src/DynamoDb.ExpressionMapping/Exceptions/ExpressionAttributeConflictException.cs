namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when merging <c>ExpressionAttributeNames</c> or
/// <c>ExpressionAttributeValues</c> from multiple expression results
/// produces a key collision with different values.
/// </summary>
public sealed class ExpressionAttributeConflictException : ExpressionMappingException
{
    /// <summary>
    /// The alias key that conflicted (e.g. "#filt_0" or ":filt_v0").
    /// </summary>
    public string AliasKey { get; }

    /// <summary>
    /// The value already present in the target dictionary.
    /// </summary>
    public string ExistingValue { get; }

    /// <summary>
    /// The incoming value that conflicted, if available.
    /// Null for value placeholder conflicts where deep comparison is impractical.
    /// </summary>
    public string? ConflictingValue { get; }

    public ExpressionAttributeConflictException(
        string aliasKey, string existingValue, string? conflictingValue = null)
        : base(conflictingValue is not null
            ? $"Attribute alias '{aliasKey}' conflicts: existing '{existingValue}', incoming '{conflictingValue}'."
            : $"Attribute alias '{aliasKey}' already exists with value '{existingValue}'.")
    {
        AliasKey = aliasKey;
        ExistingValue = existingValue;
        ConflictingValue = conflictingValue;
    }
}
