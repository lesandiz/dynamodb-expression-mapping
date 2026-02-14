using System.Linq.Expressions;

namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Fluent API for configuring property-to-attribute mappings without modifying entity classes.
/// Useful for third-party types or shared models that cannot be annotated.
/// Fluent overrides take highest priority in the resolution order.
/// </summary>
/// <typeparam name="T">The entity type to configure attribute name mappings for.</typeparam>
public sealed class AttributeNameResolverBuilder<T>
{
    private readonly Dictionary<string, string> _overrides = new();
    private readonly HashSet<string> _ignores = new();
    private NameResolutionMode _mode = NameResolutionMode.Strict;

    /// <summary>
    /// Maps a property to a DynamoDB attribute name.
    /// This takes precedence over attribute annotations.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Expression selecting the property to map.</param>
    /// <param name="attributeName">The DynamoDB attribute name to map to.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if selector or attributeName is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown if attributeName is empty or if selector does not select a single property.
    /// </exception>
    public AttributeNameResolverBuilder<T> Map<TProperty>(
        Expression<Func<T, TProperty>> selector,
        string attributeName)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);

        var propertyName = ExtractPropertyName(selector);

        // If property was previously marked as ignored, remove it
        _ignores.Remove(propertyName);

        // Store the override
        _overrides[propertyName] = attributeName;

        return this;
    }

    /// <summary>
    /// Marks a property as ignored (not stored in DynamoDB).
    /// This takes precedence over attribute annotations.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">Expression selecting the property to ignore.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if selector is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown if selector does not select a single property.
    /// </exception>
    public AttributeNameResolverBuilder<T> Ignore<TProperty>(
        Expression<Func<T, TProperty>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var propertyName = ExtractPropertyName(selector);

        // If property was previously mapped, remove it
        _overrides.Remove(propertyName);

        // Mark as ignored
        _ignores.Add(propertyName);

        return this;
    }

    /// <summary>
    /// Sets the resolution mode for handling ignored properties.
    /// </summary>
    /// <param name="mode">The resolution mode.</param>
    /// <returns>The builder for method chaining.</returns>
    public AttributeNameResolverBuilder<T> WithMode(NameResolutionMode mode)
    {
        _mode = mode;
        return this;
    }

    /// <summary>
    /// Builds the configured attribute name resolver.
    /// </summary>
    /// <returns>A configured <see cref="IAttributeNameResolver{T}"/> instance.</returns>
    public IAttributeNameResolver<T> Build()
    {
        return new AttributeNameResolver<T>(
            _mode,
            _overrides.Count > 0 ? _overrides : null,
            _ignores.Count > 0 ? _ignores : null);
    }

    /// <summary>
    /// Extracts the property name from a member access expression.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="selector">The property selector expression.</param>
    /// <returns>The property name.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the expression is not a simple member access (e.g., x => x.Property).
    /// </exception>
    private static string ExtractPropertyName<TProperty>(Expression<Func<T, TProperty>> selector)
    {
        if (selector.Body is not MemberExpression memberExpr)
        {
            throw new ArgumentException(
                "Selector must be a simple property access expression (e.g., x => x.Property)",
                nameof(selector));
        }

        if (memberExpr.Member.DeclaringType != typeof(T))
        {
            throw new ArgumentException(
                $"Property must be declared on type {typeof(T).Name}",
                nameof(selector));
        }

        return memberExpr.Member.Name;
    }
}
