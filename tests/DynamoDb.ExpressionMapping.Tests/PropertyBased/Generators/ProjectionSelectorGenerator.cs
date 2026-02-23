using System.Linq.Expressions;
using System.Reflection;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FsCheck;
using Gen = FsCheck.Fluent.Gen;
using Arb = FsCheck.Fluent.Arb;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;

/// <summary>
/// FsCheck generator for projection selector expression trees.
/// Generates Expression&lt;Func&lt;TestEntity, object&gt;&gt; across three complexity tiers.
/// </summary>
/// <remarks>
/// Avoids Gen.Where to prevent test host crashes caused by FsCheck 3.0.0-rc3's
/// retry mechanism overflowing the stack. Uses pre-computed combinations instead.
/// </remarks>
public static class ProjectionSelectorGenerator
{
    private static readonly PropertyInfo[] SimpleProperties = GetSimpleProperties();
    private static readonly (PropertyInfo, PropertyInfo)[] UniquePairs = ComputeUniquePairs(SimpleProperties);
    private static readonly (PropertyInfo, PropertyInfo, PropertyInfo)[] UniqueTriples = ComputeUniqueTriples(SimpleProperties);

    private static readonly PropertyPath[] ComplexPaths = GetComplexPropertyPaths();
    private static readonly PropertyPath[] NestedPaths = ComplexPaths.Where(p => p.Properties.Length > 1).ToArray();
    private static readonly (PropertyPath, PropertyPath)[] ComplexPairs = ComputeUniquePathPairsWithNested();
    private static readonly (PropertyPath, PropertyPath, PropertyPath)[] ComplexTriples = ComputeUniquePathTriplesWithNested();
    private static readonly (PropertyPath, PropertyPath, PropertyPath, PropertyPath)[] ComplexQuads = ComputeUniquePathQuadsWithNested();

    /// <summary>
    /// Generates random projection selectors for TestEntity.
    /// </summary>
    /// <param name="complexity">Tier: Simple (single property), Composite (2-3 properties), Complex (nested + multiple).</param>
    public static Arbitrary<Expression<Func<TestEntity, object>>> Generate(Complexity complexity = Complexity.Simple)
    {
        var generator = complexity switch
        {
            Complexity.Simple => SimpleProjectionGen(),
            Complexity.Composite => CompositeProjectionGen(),
            Complexity.Complex => ComplexProjectionGen(),
            _ => throw new ArgumentOutOfRangeException(nameof(complexity), complexity, "Invalid complexity tier")
        };

        return Arb.From(generator);
    }

    #region Simple Projections (Single Property Access)

    /// <summary>
    /// Generates single property access: x => x.PropertyName
    /// Includes reserved keywords (Name, Status) to test aliasing.
    /// </summary>
    private static Gen<Expression<Func<TestEntity, object>>> SimpleProjectionGen()
    {
        return Gen.Select(Gen.Elements(SimpleProperties), CreateSinglePropertySelector);
    }

    private static PropertyInfo[] GetSimpleProperties()
    {
        return new[]
        {
            typeof(TestEntity).GetProperty(nameof(TestEntity.OrderId))!,
            typeof(TestEntity).GetProperty(nameof(TestEntity.CustomerId))!,
            typeof(TestEntity).GetProperty(nameof(TestEntity.Title))!,
            typeof(TestEntity).GetProperty(nameof(TestEntity.Name))!, // Reserved keyword
            typeof(TestEntity).GetProperty(nameof(TestEntity.Status))!, // Reserved keyword
            typeof(TestEntity).GetProperty(nameof(TestEntity.Price))!,
            typeof(TestEntity).GetProperty(nameof(TestEntity.IsActive))!,
            typeof(TestEntity).GetProperty(nameof(TestEntity.StartDate))!,
            typeof(TestEntity).GetProperty(nameof(TestEntity.EndDate))!,
            typeof(TestEntity).GetProperty(nameof(TestEntity.Tags))!
        };
    }

    private static Expression<Func<TestEntity, object>> CreateSinglePropertySelector(PropertyInfo property)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var propertyAccess = Expression.Property(parameter, property);

        // Convert to object if needed
        Expression body;
        if (propertyAccess.Type == typeof(object))
        {
            body = propertyAccess;
        }
        else
        {
            body = Expression.Convert(propertyAccess, typeof(object));
        }

        return Expression.Lambda<Func<TestEntity, object>>(body, parameter);
    }

    #endregion

    #region Composite Projections (2-3 Properties - Using Tuples)

    /// <summary>
    /// Generates tuple projection: x => new { x.Name, x.Price }
    /// Randomly selects 2-3 unique properties from simple properties.
    /// Uses ValueTuple for simplicity instead of anonymous types.
    /// </summary>
    private static Gen<Expression<Func<TestEntity, object>>> CompositeProjectionGen()
    {
        var twoPropertyGen = Gen.Select(
            Gen.Elements(UniquePairs),
            pair => CreateTwoPropertySelector(pair.Item1, pair.Item2));

        var threePropertyGen = Gen.Select(
            Gen.Elements(UniqueTriples),
            triple => CreateThreePropertySelector(triple.Item1, triple.Item2, triple.Item3));

        return Gen.OneOf(twoPropertyGen, threePropertyGen);
    }

    private static Expression<Func<TestEntity, object>> CreateTwoPropertySelector(PropertyInfo prop1, PropertyInfo prop2)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var access1 = Expression.Property(parameter, prop1);
        var access2 = Expression.Property(parameter, prop2);

        // Create ValueTuple<object, object>
        var tuple = Expression.New(
            typeof(ValueTuple<object, object>).GetConstructors()[0],
            Expression.Convert(access1, typeof(object)),
            Expression.Convert(access2, typeof(object))
        );

        var converted = Expression.Convert(tuple, typeof(object));
        return Expression.Lambda<Func<TestEntity, object>>(converted, parameter);
    }

    private static Expression<Func<TestEntity, object>> CreateThreePropertySelector(PropertyInfo prop1, PropertyInfo prop2, PropertyInfo prop3)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var access1 = Expression.Property(parameter, prop1);
        var access2 = Expression.Property(parameter, prop2);
        var access3 = Expression.Property(parameter, prop3);

        // Create ValueTuple<object, object, object>
        var tuple = Expression.New(
            typeof(ValueTuple<object, object, object>).GetConstructors()[0],
            Expression.Convert(access1, typeof(object)),
            Expression.Convert(access2, typeof(object)),
            Expression.Convert(access3, typeof(object))
        );

        var converted = Expression.Convert(tuple, typeof(object));
        return Expression.Lambda<Func<TestEntity, object>>(converted, parameter);
    }

    #endregion

    #region Complex Projections (Nested Properties + Multiple Properties)

    /// <summary>
    /// Generates complex projection with nested properties:
    /// x => new { x.Name, x.Address.City, x.Address.Country.Code }
    /// Every generated projection includes at least one nested property path.
    /// Uses tuples to represent multiple property projections with nested access.
    /// </summary>
    private static Gen<Expression<Func<TestEntity, object>>> ComplexProjectionGen()
    {
        var twoPathGen = Gen.Select(
            Gen.Elements(ComplexPairs),
            pair => CreateTwoPathSelector(pair.Item1, pair.Item2));

        var threePathGen = Gen.Select(
            Gen.Elements(ComplexTriples),
            triple => CreateThreePathSelector(triple.Item1, triple.Item2, triple.Item3));

        var fourPathGen = Gen.Select(
            Gen.Elements(ComplexQuads),
            quad => CreateFourPathSelector(quad.Item1, quad.Item2, quad.Item3, quad.Item4));

        return Gen.OneOf(twoPathGen, threePathGen, fourPathGen);
    }

    private static PropertyPath[] GetComplexPropertyPaths()
    {
        return new[]
        {
            // Simple properties
            new PropertyPath(new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.OrderId))! }),
            new PropertyPath(new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.CustomerId))! }),
            new PropertyPath(new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Name))! }), // Reserved
            new PropertyPath(new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Status))! }), // Reserved
            new PropertyPath(new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Price))! }),
            new PropertyPath(new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.IsActive))! }),

            // Nested properties (Address.*)
            new PropertyPath(new[]
            {
                typeof(TestEntity).GetProperty(nameof(TestEntity.Address))!,
                typeof(Address).GetProperty(nameof(Address.Street))!
            }),
            new PropertyPath(new[]
            {
                typeof(TestEntity).GetProperty(nameof(TestEntity.Address))!,
                typeof(Address).GetProperty(nameof(Address.City))!
            }),
            new PropertyPath(new[]
            {
                typeof(TestEntity).GetProperty(nameof(TestEntity.Address))!,
                typeof(Address).GetProperty(nameof(Address.ZipCode))!
            }),

            // Deeply nested properties (Address.Country.*)
            new PropertyPath(new[]
            {
                typeof(TestEntity).GetProperty(nameof(TestEntity.Address))!,
                typeof(Address).GetProperty(nameof(Address.Country))!,
                typeof(Country).GetProperty(nameof(Country.Code))!
            }),
            new PropertyPath(new[]
            {
                typeof(TestEntity).GetProperty(nameof(TestEntity.Address))!,
                typeof(Address).GetProperty(nameof(Address.Country))!,
                typeof(Country).GetProperty(nameof(Country.Name))!
            })
        };
    }

    private static Expression<Func<TestEntity, object>> CreateTwoPathSelector(PropertyPath path1, PropertyPath path2)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var access1 = CreateNestedPropertyAccess(parameter, path1.Properties);
        var access2 = CreateNestedPropertyAccess(parameter, path2.Properties);

        var tuple = Expression.New(
            typeof(ValueTuple<object, object>).GetConstructors()[0],
            Expression.Convert(access1, typeof(object)),
            Expression.Convert(access2, typeof(object))
        );

        var converted = Expression.Convert(tuple, typeof(object));
        return Expression.Lambda<Func<TestEntity, object>>(converted, parameter);
    }

    private static Expression<Func<TestEntity, object>> CreateThreePathSelector(PropertyPath path1, PropertyPath path2, PropertyPath path3)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var access1 = CreateNestedPropertyAccess(parameter, path1.Properties);
        var access2 = CreateNestedPropertyAccess(parameter, path2.Properties);
        var access3 = CreateNestedPropertyAccess(parameter, path3.Properties);

        var tuple = Expression.New(
            typeof(ValueTuple<object, object, object>).GetConstructors()[0],
            Expression.Convert(access1, typeof(object)),
            Expression.Convert(access2, typeof(object)),
            Expression.Convert(access3, typeof(object))
        );

        var converted = Expression.Convert(tuple, typeof(object));
        return Expression.Lambda<Func<TestEntity, object>>(converted, parameter);
    }

    private static Expression<Func<TestEntity, object>> CreateFourPathSelector(PropertyPath path1, PropertyPath path2, PropertyPath path3, PropertyPath path4)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var access1 = CreateNestedPropertyAccess(parameter, path1.Properties);
        var access2 = CreateNestedPropertyAccess(parameter, path2.Properties);
        var access3 = CreateNestedPropertyAccess(parameter, path3.Properties);
        var access4 = CreateNestedPropertyAccess(parameter, path4.Properties);

        var tuple = Expression.New(
            typeof(ValueTuple<object, object, object, object>).GetConstructors()[0],
            Expression.Convert(access1, typeof(object)),
            Expression.Convert(access2, typeof(object)),
            Expression.Convert(access3, typeof(object)),
            Expression.Convert(access4, typeof(object))
        );

        var converted = Expression.Convert(tuple, typeof(object));
        return Expression.Lambda<Func<TestEntity, object>>(converted, parameter);
    }

    private static Expression CreateNestedPropertyAccess(Expression root, PropertyInfo[] properties)
    {
        Expression current = root;

        foreach (var property in properties)
        {
            current = Expression.Property(current, property);
        }

        return current;
    }

    #endregion

    #region Combination Helpers

    private static (PropertyInfo, PropertyInfo)[] ComputeUniquePairs(PropertyInfo[] props)
    {
        var pairs = new List<(PropertyInfo, PropertyInfo)>();
        for (int i = 0; i < props.Length; i++)
            for (int j = 0; j < props.Length; j++)
                if (i != j)
                    pairs.Add((props[i], props[j]));
        return pairs.ToArray();
    }

    private static (PropertyInfo, PropertyInfo, PropertyInfo)[] ComputeUniqueTriples(PropertyInfo[] props)
    {
        var triples = new List<(PropertyInfo, PropertyInfo, PropertyInfo)>();
        for (int i = 0; i < props.Length; i++)
            for (int j = 0; j < props.Length; j++)
                for (int k = 0; k < props.Length; k++)
                    if (i != j && i != k && j != k)
                        triples.Add((props[i], props[j], props[k]));
        return triples.ToArray();
    }

    /// <summary>
    /// Computes unique path pairs where at least one path is nested (has > 1 segment).
    /// </summary>
    private static (PropertyPath, PropertyPath)[] ComputeUniquePathPairsWithNested()
    {
        var pairs = new List<(PropertyPath, PropertyPath)>();
        for (int i = 0; i < ComplexPaths.Length; i++)
            for (int j = 0; j < ComplexPaths.Length; j++)
                if (i != j && (ComplexPaths[i].Properties.Length > 1 || ComplexPaths[j].Properties.Length > 1))
                    pairs.Add((ComplexPaths[i], ComplexPaths[j]));
        return pairs.ToArray();
    }

    /// <summary>
    /// Computes unique path triples where at least one path is nested.
    /// </summary>
    private static (PropertyPath, PropertyPath, PropertyPath)[] ComputeUniquePathTriplesWithNested()
    {
        var triples = new List<(PropertyPath, PropertyPath, PropertyPath)>();
        for (int i = 0; i < ComplexPaths.Length; i++)
            for (int j = 0; j < ComplexPaths.Length; j++)
                for (int k = 0; k < ComplexPaths.Length; k++)
                    if (i != j && i != k && j != k &&
                        (ComplexPaths[i].Properties.Length > 1 || ComplexPaths[j].Properties.Length > 1 || ComplexPaths[k].Properties.Length > 1))
                        triples.Add((ComplexPaths[i], ComplexPaths[j], ComplexPaths[k]));
        return triples.ToArray();
    }

    /// <summary>
    /// Computes unique path quads where at least one path is nested.
    /// Limited to a reasonable sample to avoid combinatorial explosion (11^4 = 14641).
    /// </summary>
    private static (PropertyPath, PropertyPath, PropertyPath, PropertyPath)[] ComputeUniquePathQuadsWithNested()
    {
        var quads = new List<(PropertyPath, PropertyPath, PropertyPath, PropertyPath)>();
        // Use nested paths as the first element to guarantee nested access and limit combinations
        for (int i = 0; i < NestedPaths.Length; i++)
            for (int j = 0; j < ComplexPaths.Length; j++)
                for (int k = 0; k < ComplexPaths.Length; k++)
                    for (int l = 0; l < ComplexPaths.Length; l++)
                    {
                        var p = new[] { NestedPaths[i], ComplexPaths[j], ComplexPaths[k], ComplexPaths[l] };
                        if (p.Distinct().Count() == 4)
                            quads.Add((p[0], p[1], p[2], p[3]));
                    }
        return quads.ToArray();
    }

    #endregion

    /// <summary>
    /// Represents a property access path (e.g., Address.City.Name).
    /// </summary>
    internal class PropertyPath : IEquatable<PropertyPath>
    {
        public PropertyInfo[] Properties { get; }

        public PropertyPath(PropertyInfo[] properties)
        {
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        public bool Equals(PropertyPath? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Properties.SequenceEqual(other.Properties);
        }

        public override bool Equals(object? obj) => Equals(obj as PropertyPath);

        public override int GetHashCode()
        {
            return Properties.Aggregate(0, (hash, prop) => hash ^ prop.GetHashCode());
        }
    }
}
