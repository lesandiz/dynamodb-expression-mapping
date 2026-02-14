using System.Collections.Concurrent;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Mapping.Converters;

namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Concrete implementation of <see cref="IAttributeValueConverterRegistry"/>.
/// Provides a frozen default singleton with all built-in converters pre-registered,
/// and supports cloning for customization.
/// </summary>
public sealed class AttributeValueConverterRegistry : IAttributeValueConverterRegistry
{
    /// <summary>
    /// Default registry with all built-in converters pre-registered.
    /// This instance is frozen — calling <see cref="Register{T}"/> on it
    /// throws <see cref="InvalidOperationException"/>.
    /// Use <see cref="Clone"/> to obtain a mutable copy for customisation.
    /// </summary>
    public static readonly AttributeValueConverterRegistry Default = CreateDefault();

    private readonly ConcurrentDictionary<Type, IAttributeValueConverter> converters;
    private readonly bool frozen;

    private AttributeValueConverterRegistry(bool frozen)
    {
        this.frozen = frozen;
        this.converters = new ConcurrentDictionary<Type, IAttributeValueConverter>();
    }

    /// <summary>
    /// Gets the converter for a .NET type.
    /// Implements resolution order from Spec 05 §8:
    /// 2. Exact type match in dictionary
    /// 3. Nullable&lt;T&gt; wrapper if type is Nullable&lt;&gt; and converter exists for T
    /// 4. EnumConverter&lt;T&gt; if type is enum
    /// 5. Open-generic collection resolution (List&lt;&gt;, HashSet&lt;&gt;, Dictionary&lt;,&gt;)
    /// 6. Throw MissingConverterException
    /// </summary>
    /// <exception cref="MissingConverterException">No converter registered for the type.</exception>
    public IAttributeValueConverter GetConverter(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        // Step 2: Exact type match in dictionary
        if (converters.TryGetValue(type, out var converter))
            return converter;

        // Step 3: Nullable<T> wrapper
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlyingType = Nullable.GetUnderlyingType(type)!;
            var underlyingConverter = GetConverter(underlyingType);

            var nullableConverterType = typeof(NullableConverter<>).MakeGenericType(underlyingType);
            var nullableConverter = (IAttributeValueConverter)Activator.CreateInstance(
                nullableConverterType,
                underlyingConverter)!;

            // Cache the composed converter
            converters[type] = nullableConverter;
            return nullableConverter;
        }

        // Step 4: EnumConverter<T> if type is enum
        if (type.IsEnum)
        {
            var enumConverterType = typeof(EnumConverter<>).MakeGenericType(type);
            var enumConverter = (IAttributeValueConverter)Activator.CreateInstance(
                enumConverterType,
                EnumStorageMode.String)!;

            // Cache the composed converter
            converters[type] = enumConverter;
            return enumConverter;
        }

        // Step 5: Open-generic collection resolution
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var genericArgs = type.GetGenericArguments();

            // List<T> or IReadOnlyList<T>
            if (genericTypeDef == typeof(List<>) || genericTypeDef == typeof(IReadOnlyList<>))
            {
                var elementType = genericArgs[0];
                var elementConverter = GetConverter(elementType);

                var listConverterType = typeof(ListConverter<>).MakeGenericType(elementType);
                var listConverter = (IAttributeValueConverter)Activator.CreateInstance(
                    listConverterType,
                    elementConverter)!;

                // Cache the composed converter
                converters[type] = listConverter;
                return listConverter;
            }

            // HashSet<T> or ISet<T>
            if (genericTypeDef == typeof(HashSet<>) || genericTypeDef == typeof(ISet<>))
            {
                var elementType = genericArgs[0];
                var elementConverter = GetConverter(elementType);

                var setConverterType = typeof(SetConverter<>).MakeGenericType(elementType);
                var setConverter = (IAttributeValueConverter)Activator.CreateInstance(
                    setConverterType,
                    elementConverter)!;

                // Cache the composed converter
                converters[type] = setConverter;
                return setConverter;
            }

            // Dictionary<string, TValue> or IReadOnlyDictionary<string, TValue>
            if ((genericTypeDef == typeof(Dictionary<,>) || genericTypeDef == typeof(IReadOnlyDictionary<,>))
                && genericArgs[0] == typeof(string))
            {
                var valueType = genericArgs[1];
                var valueConverter = GetConverter(valueType);

                var mapConverterType = typeof(MapConverter<>).MakeGenericType(valueType);
                var mapConverter = (IAttributeValueConverter)Activator.CreateInstance(
                    mapConverterType,
                    valueConverter)!;

                // Cache the composed converter
                converters[type] = mapConverter;
                return mapConverter;
            }
        }

        // Step 6: Throw MissingConverterException
        throw new MissingConverterException(type);
    }

    /// <summary>
    /// Gets a strongly-typed converter.
    /// </summary>
    public IAttributeValueConverter<T> GetConverter<T>()
    {
        return (IAttributeValueConverter<T>)GetConverter(typeof(T));
    }

    /// <summary>
    /// Checks if a converter is registered for the type.
    /// Returns true if a converter can be resolved, false otherwise.
    /// Does not throw exceptions.
    /// </summary>
    public bool HasConverter(Type type)
    {
        try
        {
            GetConverter(type);
            return true;
        }
        catch (MissingConverterException)
        {
            return false;
        }
    }

    /// <summary>
    /// Registers a custom converter. Overwrites any existing converter for the same type.
    /// Throws <see cref="InvalidOperationException"/> if this registry is frozen
    /// (i.e. the <see cref="Default"/> singleton).
    /// </summary>
    public void Register<T>(IAttributeValueConverter<T> converter)
    {
        if (converter == null)
            throw new ArgumentNullException(nameof(converter));

        if (frozen)
            throw new InvalidOperationException(
                "Cannot mutate the default converter registry. " +
                "Use Clone() to create a mutable copy, or register converters " +
                "via DynamoDbExpressionConfig.Builder.WithConverter().");

        converters[typeof(T)] = converter;
    }

    /// <summary>
    /// Creates a deep copy of this registry, including all registered converters.
    /// The clone is mutable regardless of whether the source is frozen.
    /// Used by <see cref="DynamoDbExpressionConfig.Builder"/> to customise converters
    /// without mutating the shared <see cref="Default"/> instance (Spec 11 §2).
    /// </summary>
    public AttributeValueConverterRegistry Clone()
    {
        var clone = new AttributeValueConverterRegistry(frozen: false);
        foreach (var kvp in this.converters)
            clone.converters[kvp.Key] = kvp.Value;
        return clone;
    }

    private static AttributeValueConverterRegistry CreateDefault()
    {
        var registry = new AttributeValueConverterRegistry(frozen: false);
        registry.RegisterBuiltIns();
        // Return a frozen copy so the singleton cannot be mutated.
        var frozen = new AttributeValueConverterRegistry(frozen: true);
        foreach (var kvp in registry.converters)
            frozen.converters[kvp.Key] = kvp.Value;
        return frozen;
    }

    private void RegisterBuiltIns()
    {
        // Primitive converters (Spec 05 §3)
        Register(new StringConverter());
        Register(new GuidConverter());
        Register(new BoolConverter());
        Register(new Int32Converter());
        Register(new Int64Converter());
        Register(new DecimalConverter());
        Register(new DoubleConverter());
        Register(new DateTimeConverter());
        Register(new DateTimeOffsetConverter());
        Register(new ByteArrayConverter());

        // Built-in collection converters that take precedence over generic resolution
        // (Spec 05 §8a design notes)
        Register(new ListConverter<string>(new StringConverter()));
        Register(new ListConverter<int>(new Int32Converter()));
        Register(new SetConverter<string>(new StringConverter()));
        Register(new MapConverter<string>(new StringConverter()));
    }
}
