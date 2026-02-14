namespace DynamoDb.ExpressionMapping.ReservedKeywords;

/// <summary>
/// Generates unique, scoped aliases for DynamoDB expression attribute names and values.
/// </summary>
public sealed class AliasGenerator
{
    private readonly string namePrefix;
    private readonly string valuePrefix;
    private uint nameIndex;
    private uint valueIndex;

    /// <summary>
    /// Creates a generator with a specific prefix scope.
    /// </summary>
    /// <param name="scope">Scope prefix, e.g. "proj", "filt", "cond", "upd", "key"</param>
    public AliasGenerator(string scope)
    {
        this.namePrefix = $"#{scope}_";
        this.valuePrefix = $":{scope}_v";
    }

    /// <summary>Generates next attribute name alias. E.g. "#proj_0", "#proj_1"</summary>
    public string NextName() => $"{namePrefix}{nameIndex++}";

    /// <summary>Generates next attribute value placeholder. E.g. ":filt_v0", ":filt_v1"</summary>
    public string NextValue() => $"{valuePrefix}{valueIndex++}";

    /// <summary>Resets the counters.</summary>
    public void Reset() { nameIndex = 0; valueIndex = 0; }
}
