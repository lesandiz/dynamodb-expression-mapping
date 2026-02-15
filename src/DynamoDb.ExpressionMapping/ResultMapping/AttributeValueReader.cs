using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.ResultMapping;

/// <summary>
/// Low-level attribute readers that extract and convert single AttributeValue
/// entries from dictionaries. Handles null/missing attributes gracefully by
/// returning default values.
/// </summary>
internal static class AttributeValueReader
{
    /// <summary>
    /// Navigates intermediate Map segments and returns the inner dictionary
    /// containing the leaf attribute key. Returns null if any intermediate
    /// segment is missing or not a Map.
    /// </summary>
    public static Dictionary<string, AttributeValue>? NavigateToLeaf(
        Dictionary<string, AttributeValue> attrs,
        string[] path)
    {
        var current = attrs;
        for (var i = 0; i < path.Length - 1; i++)
        {
            if (!current.TryGetValue(path[i], out var av) || av.M == null)
                return null;
            current = av.M;
        }
        return current;
    }

    public static string? ReadString(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return null;
        return av.S;
    }

    public static Guid ReadGuid(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return Guid.Empty;
        return Guid.TryParse(av.S, out var result) ? result : Guid.Empty;
    }

    public static Guid? ReadNullableGuid(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return null;
        return Guid.TryParse(av.S, out var result) ? result : null;
    }

    public static bool ReadBool(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av))
            return false;
        return av.BOOL;
    }

    public static bool? ReadNullableBool(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return null;
        return av.BOOL;
    }

    public static int ReadInt(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return 0;
        return int.TryParse(av.N, out var result) ? result : 0;
    }

    public static int? ReadNullableInt(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return null;
        return int.TryParse(av.N, out var result) ? result : null;
    }

    public static long ReadLong(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return 0;
        return long.TryParse(av.N, out var result) ? result : 0;
    }

    public static long? ReadNullableLong(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return null;
        return long.TryParse(av.N, out var result) ? result : null;
    }

    public static decimal ReadDecimal(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return 0;
        return decimal.TryParse(av.N, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result : 0;
    }

    public static decimal? ReadNullableDecimal(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return null;
        return decimal.TryParse(av.N, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result : null;
    }

    public static double ReadDouble(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return 0;
        return double.TryParse(av.N, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result : 0;
    }

    public static double? ReadNullableDouble(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return null;
        return double.TryParse(av.N, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result : null;
    }

    public static float ReadFloat(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return 0;
        return float.TryParse(av.N, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result : 0;
    }

    public static float? ReadNullableFloat(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return null;
        return float.TryParse(av.N, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result : null;
    }

    public static DateTime ReadDateTime(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return DateTime.MinValue;
        return DateTime.TryParse(av.S, null, DateTimeStyles.RoundtripKind, out var result)
            ? result : DateTime.MinValue;
    }

    public static DateTime? ReadNullableDateTime(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return null;
        return DateTime.TryParse(av.S, null, DateTimeStyles.RoundtripKind, out var result)
            ? result : null;
    }

    public static DateTimeOffset ReadDateTimeOffset(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return DateTimeOffset.MinValue;
        return DateTimeOffset.TryParse(av.S, null, DateTimeStyles.RoundtripKind, out var result)
            ? result : DateTimeOffset.MinValue;
    }

    public static DateTimeOffset? ReadNullableDateTimeOffset(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return null;
        return DateTimeOffset.TryParse(av.S, null, DateTimeStyles.RoundtripKind, out var result)
            ? result : null;
    }

    public static List<string>? ReadStringList(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return null;
        return av.SS?.ToList();
    }

    public static byte[]? ReadBytes(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL)
            return null;
        return av.B?.ToArray();
    }
}
