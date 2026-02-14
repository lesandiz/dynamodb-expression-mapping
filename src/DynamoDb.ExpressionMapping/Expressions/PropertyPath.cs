using System.Reflection;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Represents a property access path extracted from an expression tree.
/// Immutable value object.
/// </summary>
public sealed class PropertyPath
{
    /// <summary>
    /// The C# property name segments. E.g. ["Address", "City"] for p.Address.City
    /// </summary>
    public IReadOnlyList<string> Segments { get; }

    /// <summary>
    /// The full dotted path. E.g. "Address.City"
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// The leaf property name. E.g. "City"
    /// </summary>
    public string LeafName { get; }

    /// <summary>
    /// The PropertyInfo for every segment, parallel to <see cref="Segments"/>.
    /// E.g. for p.Address.City: [PropertyInfo(Address), PropertyInfo(City)].
    /// Each entry provides DeclaringType (for resolver lookup) and PropertyType
    /// (for determining the next segment's declaring type), eliminating the
    /// need for additional reflection during attribute name resolution.
    /// </summary>
    public IReadOnlyList<PropertyInfo> SegmentProperties { get; }

    /// <summary>
    /// Convenience accessor for the leaf property's PropertyInfo.
    /// Equivalent to SegmentProperties[^1]. Used for type-aware converter
    /// selection, [DynamoDbIgnore]/[DynamoDbAttribute] detection, and
    /// validation that the property is readable.
    /// </summary>
    public PropertyInfo PropertyInfo => SegmentProperties[^1];

    /// <summary>
    /// Whether this is a nested path (more than one segment).
    /// </summary>
    public bool IsNested => Segments.Count > 1;

    /// <summary>
    /// Constructs a PropertyPath from segments and their PropertyInfo objects.
    /// </summary>
    /// <param name="segments">Property name segments</param>
    /// <param name="segmentProperties">PropertyInfo for each segment (parallel to segments)</param>
    public PropertyPath(IReadOnlyList<string> segments, IReadOnlyList<PropertyInfo> segmentProperties)
    {
        if (segments == null || segments.Count == 0)
            throw new ArgumentException("Segments cannot be null or empty", nameof(segments));
        if (segmentProperties == null || segmentProperties.Count == 0)
            throw new ArgumentException("SegmentProperties cannot be null or empty", nameof(segmentProperties));
        if (segments.Count != segmentProperties.Count)
            throw new ArgumentException("Segments and SegmentProperties must have the same count");

        Segments = segments;
        SegmentProperties = segmentProperties;
        FullPath = string.Join(".", segments);
        LeafName = segments[^1];
    }

    /// <summary>
    /// Equality based on FullPath (case-sensitive).
    /// </summary>
    public override bool Equals(object? obj) =>
        obj is PropertyPath other && FullPath == other.FullPath;

    /// <summary>
    /// Hash code based on FullPath.
    /// </summary>
    public override int GetHashCode() => FullPath.GetHashCode();

    /// <summary>
    /// String representation returns FullPath.
    /// </summary>
    public override string ToString() => FullPath;
}
