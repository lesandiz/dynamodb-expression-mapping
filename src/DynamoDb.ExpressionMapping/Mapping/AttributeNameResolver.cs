using System.Collections.Concurrent;
using System.Reflection;
using DynamoDb.ExpressionMapping.Attributes;
using DynamoDb.ExpressionMapping.Exceptions;

namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Resolves C# property names to DynamoDB attribute names using reflection-based
/// attribute scanning. Caches type metadata for performance.
/// </summary>
/// <typeparam name="T">The entity type to resolve attribute names for.</typeparam>
public sealed class AttributeNameResolver<T> : IAttributeNameResolver<T>
{
    private static readonly ConcurrentDictionary<Type, TypeMetadata> MetadataCache = new();

    private readonly Dictionary<string, string> propertyToAttribute;  // C# name → DynamoDB name
    private readonly Dictionary<string, string> attributeToProperty;  // DynamoDB name → C# name
    private readonly HashSet<string> ignoredProperties;
    private readonly NameResolutionMode mode;

    /// <summary>
    /// Creates a new attribute name resolver for type T.
    /// </summary>
    /// <param name="mode">The resolution mode for handling ignored properties.</param>
    public AttributeNameResolver(NameResolutionMode mode = NameResolutionMode.Strict)
        : this(mode, null, null)
    {
    }

    /// <summary>
    /// Creates a new attribute name resolver for type T with fluent overrides.
    /// </summary>
    /// <param name="mode">The resolution mode for handling ignored properties.</param>
    /// <param name="fluentOverrides">Fluent API overrides for property-to-attribute mappings (highest priority).</param>
    /// <param name="fluentIgnores">Fluent API ignored properties (highest priority).</param>
    internal AttributeNameResolver(
        NameResolutionMode mode,
        Dictionary<string, string>? fluentOverrides,
        HashSet<string>? fluentIgnores)
    {
        this.mode = mode;

        // Get or create cached metadata for type T
        var metadata = MetadataCache.GetOrAdd(typeof(T), BuildTypeMetadata);

        // Start with attribute-based metadata
        propertyToAttribute = new Dictionary<string, string>(metadata.PropertyToAttribute);
        attributeToProperty = new Dictionary<string, string>(metadata.AttributeToProperty);
        ignoredProperties = new HashSet<string>(metadata.IgnoredProperties);

        // Apply fluent overrides (highest priority)
        if (fluentOverrides != null)
        {
            foreach (var kvp in fluentOverrides)
            {
                var propertyName = kvp.Key;
                var attributeName = kvp.Value;

                // Remove from ignored if it was previously marked as ignored
                ignoredProperties.Remove(propertyName);

                // Remove old bidirectional mappings if they exist
                if (propertyToAttribute.TryGetValue(propertyName, out var oldAttributeName))
                {
                    attributeToProperty.Remove(oldAttributeName);
                }

                // Add new bidirectional mapping
                propertyToAttribute[propertyName] = attributeName;
                attributeToProperty[attributeName] = propertyName;
            }
        }

        // Apply fluent ignores (highest priority)
        if (fluentIgnores != null)
        {
            foreach (var propertyName in fluentIgnores)
            {
                // Remove from mappings if it was previously mapped
                if (propertyToAttribute.TryGetValue(propertyName, out var attributeName))
                {
                    attributeToProperty.Remove(attributeName);
                    propertyToAttribute.Remove(propertyName);
                }

                // Mark as ignored
                ignoredProperties.Add(propertyName);
            }
        }
    }

    /// <summary>
    /// Gets the DynamoDB attribute name for a C# property.
    /// </summary>
    /// <param name="propertyName">The C# property name.</param>
    /// <returns>The DynamoDB attribute name.</returns>
    /// <exception cref="InvalidProjectionException">
    /// Thrown when the property is marked with [DynamoDbIgnore] in strict mode.
    /// </exception>
    public string GetAttributeName(string propertyName)
    {
        if (ignoredProperties.Contains(propertyName))
        {
            if (mode == NameResolutionMode.Strict)
            {
                throw new InvalidProjectionException(propertyName, typeof(T));
            }
            // In lenient mode, still return the property name as-is
            // The caller is responsible for filtering out ignored properties
            return propertyName;
        }

        return propertyToAttribute.TryGetValue(propertyName, out var attributeName)
            ? attributeName
            : propertyName;
    }

    /// <summary>
    /// Returns whether a property represents a stored DynamoDB attribute.
    /// </summary>
    /// <param name="propertyName">The C# property name.</param>
    /// <returns>False for properties marked with [DynamoDbIgnore], true otherwise.</returns>
    public bool IsStoredAttribute(string propertyName)
    {
        return !ignoredProperties.Contains(propertyName);
    }

    /// <summary>
    /// Gets the C# property name for a DynamoDB attribute name (reverse lookup).
    /// </summary>
    /// <param name="attributeName">The DynamoDB attribute name.</param>
    /// <returns>The C# property name.</returns>
    public string GetPropertyName(string attributeName)
    {
        return attributeToProperty.TryGetValue(attributeName, out var propertyName)
            ? propertyName
            : attributeName;
    }

    /// <summary>
    /// Builds type metadata for the specified type by scanning properties and attributes.
    /// </summary>
    /// <param name="entityType">The entity type to build metadata for.</param>
    /// <returns>Type metadata containing all mapping information.</returns>
    private static TypeMetadata BuildTypeMetadata(Type entityType)
    {
        var propertyToAttribute = new Dictionary<string, string>();
        var attributeToProperty = new Dictionary<string, string>();
        var ignoredProperties = new HashSet<string>();

        var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var propertyName = property.Name;

            // Check for [DynamoDbIgnore] (this library)
            if (property.GetCustomAttribute<DynamoDbIgnoreAttribute>() != null)
            {
                ignoredProperties.Add(propertyName);
                continue;
            }

            // Check for [DynamoDBIgnore] (AWS SDK)
            // Using string name to avoid hard dependency on AWS SDK types
            var awsIgnoreAttr = property.GetCustomAttributes()
                .FirstOrDefault(a => a.GetType().FullName == "Amazon.DynamoDBv2.DataModel.DynamoDBIgnoreAttribute");
            if (awsIgnoreAttr != null)
            {
                ignoredProperties.Add(propertyName);
                continue;
            }

            // Resolution order for attribute name:
            // 1. Fluent overrides - not handled here (will be in builder)
            // 2. [DynamoDbAttribute] (this library)
            // 3. [DynamoDBProperty] (AWS SDK)
            // 4. Property name (convention)

            string? attributeName = null;

            // Check [DynamoDbAttribute]
            var dynamoDbAttr = property.GetCustomAttribute<DynamoDbAttributeAttribute>();
            if (dynamoDbAttr != null)
            {
                attributeName = dynamoDbAttr.AttributeName;
            }

            // Check [DynamoDBProperty] (AWS SDK)
            if (attributeName == null)
            {
                var awsPropertyAttr = property.GetCustomAttributes()
                    .FirstOrDefault(a => a.GetType().FullName == "Amazon.DynamoDBv2.DataModel.DynamoDBPropertyAttribute");
                if (awsPropertyAttr != null)
                {
                    // Use reflection to get AttributeName property
                    var attributeNameProp = awsPropertyAttr.GetType().GetProperty("AttributeName");
                    if (attributeNameProp != null)
                    {
                        attributeName = attributeNameProp.GetValue(awsPropertyAttr) as string;
                    }
                }
            }

            // Fall back to property name (convention)
            if (attributeName == null || attributeName == propertyName)
            {
                // If no mapping or mapping is same as property name, don't add to dictionary
                // This saves memory and allows pass-through behavior
                continue;
            }

            // Store bidirectional mapping
            propertyToAttribute[propertyName] = attributeName;
            attributeToProperty[attributeName] = propertyName;
        }

        return new TypeMetadata(propertyToAttribute, attributeToProperty, ignoredProperties);
    }

    /// <summary>
    /// Internal class that holds cached metadata for a type.
    /// </summary>
    private sealed class TypeMetadata
    {
        public Dictionary<string, string> PropertyToAttribute { get; }
        public Dictionary<string, string> AttributeToProperty { get; }
        public HashSet<string> IgnoredProperties { get; }

        public TypeMetadata(
            Dictionary<string, string> propertyToAttribute,
            Dictionary<string, string> attributeToProperty,
            HashSet<string> ignoredProperties)
        {
            PropertyToAttribute = propertyToAttribute;
            AttributeToProperty = attributeToProperty;
            IgnoredProperties = ignoredProperties;
        }
    }
}
