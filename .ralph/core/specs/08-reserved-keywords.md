# Spec 08: Reserved Keyword Handling

## Motivation

DynamoDB has 573+ reserved keywords that cannot be used directly in expressions. When an attribute name matches a reserved word (e.g. `Status`, `Name`, `Date`, `Hidden`, `Comment`), it must be aliased via `ExpressionAttributeNames`. This library must handle aliasing automatically and prevent collisions when multiple expression types (projection, filter, condition, update) are applied to the same request.

## Design

### 1. ReservedKeywordRegistry

```csharp
namespace DynamoDb.ExpressionMapping.ReservedKeywords;

/// <summary>
/// Detects DynamoDB reserved keywords. Thread-safe, singleton.
/// Based on the official AWS DynamoDB reserved words list.
/// https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ReservedWords.html
/// </summary>
public sealed class ReservedKeywordRegistry
{
    /// <summary>
    /// Default registry with the complete official reserved words list.
    /// </summary>
    public static readonly ReservedKeywordRegistry Default = new();

    private readonly HashSet<string> reservedWords;

    public ReservedKeywordRegistry()
    {
        this.reservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Complete official list (573+ words, A-Z)
        };
    }

    /// <summary>
    /// Checks if a name is a DynamoDB reserved keyword. Case-insensitive.
    /// </summary>
    public bool IsReserved(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return this.reservedWords.Contains(name);
    }

    /// <summary>
    /// Checks if a name needs escaping (reserved keyword OR contains special characters).
    /// </summary>
    public bool NeedsEscaping(string name)
    {
        return IsReserved(name) || ContainsSpecialCharacters(name);
    }

    private static bool ContainsSpecialCharacters(string name)
    {
        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
                return true;
        }
        return false;
    }
}
```

### 2. Scoped Alias Generator

Each expression type gets its own alias scope to prevent collisions:

```csharp
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
    /// <param name="scope">Scope prefix, e.g. "proj", "filt", "cond", "upd"</param>
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
```

### 3. Predefined Scopes

| Expression Type | Name Prefix | Value Prefix |
|---|---|---|
| Projection | `#proj_` | (none — projections have no values) |
| Filter | `#filt_` | `:filt_v` |
| Condition | `#cond_` | `:cond_v` |
| Update | `#upd_` | `:upd_v` |
| Key Condition | `#key_` | `:key_v` |

### 4. Complete Reserved Words List

The registry must include the complete official list from AWS documentation (573+ words). Notable entries that commonly overlap with DynamoDB attribute names:

`COMMENT`, `DATA`, `DATE`, `DOMAIN`, `ENABLE`, `HIDDEN`, `LIST`, `MAP`, `NAME`, `NUMBER`, `ONLINE`, `OWNER`, `PATH`, `RANK`, `REGION`, `SOURCE`, `STATE`, `STATUS`, `STRING`, `TABLE`, `TEXT`, `TIME`, `TOKEN`, `TYPE`, `URL`, `USER`, `VALUE`, `VALUES`, `VIEW`, `ZONE`

### 5. Collision Prevention

When merging expression results into an AWS SDK request, aliases from different scopes cannot collide because of the scope prefix:

```csharp
// Projection alias for "Status"
"#proj_0" → "Status"

// Filter alias for "Status"
"#filt_0" → "Status"

// Both can coexist safely on the same request
request.ExpressionAttributeNames = {
    "#proj_0": "Status",
    "#filt_0": "Status"
}
```

### 6. Smart Aliasing

Only alias attributes that actually need it (reserved keyword or special characters). Non-reserved names are used directly:

```csharp
// "OrderId" — not reserved, no special chars → used directly
// "Status"  — reserved → "#proj_0"
// "Name"    — reserved → "#proj_1"
// "cust_id" — has underscore but that is allowed → used directly
```

This keeps expressions readable in logs and CloudWatch.
