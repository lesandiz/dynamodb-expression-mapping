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
public static class ProjectionSelectorGenerator
{
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
        return Gen.Select(Gen.Elements(GetSimpleProperties()), CreateSinglePropertySelector);
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
    /// Randomly selects 2-3 properties from simple properties.
    /// Uses ValueTuple for simplicity instead of anonymous types.
    /// </summary>
    private static Gen<Expression<Func<TestEntity, object>>> CompositeProjectionGen()
    {
        var propGen = Gen.Elements(GetSimpleProperties());

        var twoPropertyGen = Gen.SelectMany(propGen, prop1 =>
            Gen.Select(Gen.Where(propGen, prop2 => prop1 != prop2),
                       prop2 => CreateTwoPropertySelector(prop1, prop2)));

        var threePropertyGen = Gen.SelectMany(propGen, prop1 =>
            Gen.SelectMany(propGen, prop2 =>
                Gen.Select(Gen.Where(propGen, prop3 => prop1 != prop2 && prop1 != prop3 && prop2 != prop3),
                           prop3 => CreateThreePropertySelector(prop1, prop2, prop3))));

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
    /// Uses tuples to represent multiple property projections with nested access.
    /// </summary>
    private static Gen<Expression<Func<TestEntity, object>>> ComplexProjectionGen()
    {
        var pathGen = Gen.Elements(GetComplexPropertyPaths());

        var twoPathGen = Gen.SelectMany(pathGen, path1 =>
            Gen.Select(Gen.Where(pathGen, path2 => !path1.Equals(path2)),
                       path2 => CreateTwoPathSelector(path1, path2)));

        var threePathGen = Gen.SelectMany(pathGen, path1 =>
            Gen.SelectMany(pathGen, path2 =>
                Gen.Select(Gen.Where(pathGen, path3 => !path1.Equals(path2) && !path1.Equals(path3) && !path2.Equals(path3)),
                           path3 => CreateThreePathSelector(path1, path2, path3))));

        var fourPathGen = Gen.SelectMany(pathGen, path1 =>
            Gen.SelectMany(pathGen, path2 =>
                Gen.SelectMany(pathGen, path3 =>
                    Gen.Select(Gen.Where(pathGen, path4 => AreAllUnique(path1, path2, path3, path4)),
                               path4 => CreateFourPathSelector(path1, path2, path3, path4)))));

        return Gen.OneOf(twoPathGen, threePathGen, fourPathGen);
    }

    private static bool AreAllUnique(PropertyPath p1, PropertyPath p2, PropertyPath p3, PropertyPath p4)
    {
        var paths = new[] { p1, p2, p3, p4 };
        return paths.Distinct().Count() == 4;
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

    /// <summary>
    /// Represents a property access path (e.g., Address.City.Name).
    /// </summary>
    private class PropertyPath : IEquatable<PropertyPath>
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
