using System.Linq.Expressions;
using System.Reflection;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FsCheck;
using Arb = FsCheck.Fluent.Arb;
using Gen = FsCheck.Fluent.Gen;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;

/// <summary>
/// FsCheck generator for filter predicate expression trees.
/// Generates Expression&lt;Func&lt;TestEntity, bool&gt;&gt; across three complexity tiers.
/// </summary>
public static class FilterPredicateGenerator
{
    /// <summary>
    /// Generates random filter predicates for TestEntity.
    /// </summary>
    /// <param name="complexity">Tier: Simple (single comparison), Composite (2-3 combined), Complex (nested + functions + NOT).</param>
    public static Arbitrary<Expression<Func<TestEntity, bool>>> Generate(Complexity complexity = Complexity.Simple)
    {
        var generator = complexity switch
        {
            Complexity.Simple => SimplePredicateGen(),
            Complexity.Composite => CompositePredicateGen(),
            Complexity.Complex => ComplexPredicateGen(),
            _ => throw new ArgumentOutOfRangeException(nameof(complexity), complexity, "Invalid complexity tier")
        };

        return Arb.From(generator);
    }

    #region Simple Predicates (Single Comparison)

    /// <summary>
    /// Generates single comparison: x => x.PropertyName == value
    /// Covers: ==, !=, >, >=, <, <=
    /// </summary>
    private static Gen<Expression<Func<TestEntity, bool>>> SimplePredicateGen()
    {
        return Gen.OneOf(
            StringComparisonGen(),
            NumericComparisonGen(),
            BooleanComparisonGen(),
            DateTimeComparisonGen(),
            NullableComparisonGen()
        );
    }

    private static Gen<Expression<Func<TestEntity, bool>>> StringComparisonGen()
    {
        var properties = new[]
        {
            typeof(TestEntity).GetProperty(nameof(TestEntity.OrderId))!,
            typeof(TestEntity).GetProperty(nameof(TestEntity.CustomerId))!,
            typeof(TestEntity).GetProperty(nameof(TestEntity.Title))!,
            typeof(TestEntity).GetProperty(nameof(TestEntity.Name))!, // Reserved keyword
            typeof(TestEntity).GetProperty(nameof(TestEntity.Status))! // Reserved keyword
        };

        var propGen = Gen.Elements(properties);
        var valueGen = Gen.OneOf(
            Gen.Constant(""),
            Gen.Elements("test", "foo", "bar", "alpha", "beta", "gamma"),
            Gen.Select(Gen.Elements(Guid.NewGuid()), g => g.ToString())
        );

        return Gen.SelectMany(propGen, prop =>
            Gen.SelectMany(valueGen, value =>
                Gen.Select(Gen.Elements(true, false), useEquality =>
                    CreateStringComparison(prop, value, useEquality))));
    }

    private static Expression<Func<TestEntity, bool>> CreateStringComparison(PropertyInfo property, string value, bool useEquality)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var propertyAccess = Expression.Property(parameter, property);
        var constant = Expression.Constant(value, typeof(string));

        var comparison = useEquality
            ? Expression.Equal(propertyAccess, constant)
            : Expression.NotEqual(propertyAccess, constant);

        return Expression.Lambda<Func<TestEntity, bool>>(comparison, parameter);
    }

    private static Gen<Expression<Func<TestEntity, bool>>> NumericComparisonGen()
    {
        var property = typeof(TestEntity).GetProperty(nameof(TestEntity.Price))!;
        var valueGen = Gen.OneOf(
            Gen.Constant(0m),
            Gen.Elements(1m, 10m, 99.99m, 100m, 1000m)
        );

        return Gen.SelectMany(valueGen, value =>
            Gen.Select(Gen.Choose(0, 5), op =>
                CreateNumericComparison(property, value, op)));
    }

    private static Expression<Func<TestEntity, bool>> CreateNumericComparison(PropertyInfo property, decimal value, int op)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var propertyAccess = Expression.Property(parameter, property);
        var constant = Expression.Constant(value, typeof(decimal));

        var comparison = op switch
        {
            0 => Expression.Equal(propertyAccess, constant),
            1 => Expression.NotEqual(propertyAccess, constant),
            2 => Expression.GreaterThan(propertyAccess, constant),
            3 => Expression.GreaterThanOrEqual(propertyAccess, constant),
            4 => Expression.LessThan(propertyAccess, constant),
            _ => Expression.LessThanOrEqual(propertyAccess, constant)
        };

        return Expression.Lambda<Func<TestEntity, bool>>(comparison, parameter);
    }

    private static Gen<Expression<Func<TestEntity, bool>>> BooleanComparisonGen()
    {
        var property = typeof(TestEntity).GetProperty(nameof(TestEntity.IsActive))!;
        var valueGen = Gen.Elements(true, false);

        return Gen.Select(valueGen, value => CreateBooleanComparison(property, value));
    }

    private static Expression<Func<TestEntity, bool>> CreateBooleanComparison(PropertyInfo property, bool value)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var propertyAccess = Expression.Property(parameter, property);
        var constant = Expression.Constant(value, typeof(bool));

        var comparison = Expression.Equal(propertyAccess, constant);
        return Expression.Lambda<Func<TestEntity, bool>>(comparison, parameter);
    }

    private static Gen<Expression<Func<TestEntity, bool>>> DateTimeComparisonGen()
    {
        var property = typeof(TestEntity).GetProperty(nameof(TestEntity.StartDate))!;
        var valueGen = Gen.OneOf(
            Gen.Constant(DateTime.MinValue),
            Gen.Constant(DateTime.UtcNow),
            Gen.Constant(new DateTime(2024, 1, 1)),
            Gen.Constant(new DateTime(2025, 12, 31))
        );

        return Gen.SelectMany(valueGen, value =>
            Gen.Select(Gen.Choose(0, 5), op =>
                CreateDateTimeComparison(property, value, op)));
    }

    private static Expression<Func<TestEntity, bool>> CreateDateTimeComparison(PropertyInfo property, DateTime value, int op)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var propertyAccess = Expression.Property(parameter, property);
        var constant = Expression.Constant(value, typeof(DateTime));

        var comparison = op switch
        {
            0 => Expression.Equal(propertyAccess, constant),
            1 => Expression.NotEqual(propertyAccess, constant),
            2 => Expression.GreaterThan(propertyAccess, constant),
            3 => Expression.GreaterThanOrEqual(propertyAccess, constant),
            4 => Expression.LessThan(propertyAccess, constant),
            _ => Expression.LessThanOrEqual(propertyAccess, constant)
        };

        return Expression.Lambda<Func<TestEntity, bool>>(comparison, parameter);
    }

    private static Gen<Expression<Func<TestEntity, bool>>> NullableComparisonGen()
    {
        var property = typeof(TestEntity).GetProperty(nameof(TestEntity.EndDate))!;

        // Generate either null check or value comparison
        return Gen.OneOf(
            Gen.Constant(CreateNullCheck(property, true)),
            Gen.Constant(CreateNullCheck(property, false)),
            Gen.SelectMany(Gen.Elements(DateTime.MinValue, DateTime.UtcNow), val =>
                Gen.Select(Gen.Choose(0, 3), op =>
                    CreateNullableValueComparison(property, val, op)))
        );
    }

    private static Expression<Func<TestEntity, bool>> CreateNullCheck(PropertyInfo property, bool checkForNull)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var propertyAccess = Expression.Property(parameter, property);
        var nullConstant = Expression.Constant(null, typeof(DateTime?));

        var comparison = checkForNull
            ? Expression.Equal(propertyAccess, nullConstant)
            : Expression.NotEqual(propertyAccess, nullConstant);

        return Expression.Lambda<Func<TestEntity, bool>>(comparison, parameter);
    }

    private static Expression<Func<TestEntity, bool>> CreateNullableValueComparison(PropertyInfo property, DateTime value, int op)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var propertyAccess = Expression.Property(parameter, property);
        var constant = Expression.Constant(value, typeof(DateTime));

        // Access .Value property for comparison
        var valueProperty = typeof(DateTime?).GetProperty("Value")!;
        var valueAccess = Expression.Property(propertyAccess, valueProperty);

        var comparison = op switch
        {
            0 => Expression.GreaterThan(valueAccess, constant),
            1 => Expression.GreaterThanOrEqual(valueAccess, constant),
            2 => Expression.LessThan(valueAccess, constant),
            _ => Expression.LessThanOrEqual(valueAccess, constant)
        };

        return Expression.Lambda<Func<TestEntity, bool>>(comparison, parameter);
    }

    #endregion

    #region Composite Predicates (2-3 Combined)

    /// <summary>
    /// Generates composite predicates using && and ||:
    /// x => x.Name == "foo" && x.Price > 10
    /// x => x.IsActive || x.Price > 100
    /// </summary>
    private static Gen<Expression<Func<TestEntity, bool>>> CompositePredicateGen()
    {
        var simpleGen = SimplePredicateGen();

        var twoPredicatesGen = Gen.SelectMany(simpleGen, pred1 =>
            Gen.SelectMany(simpleGen, pred2 =>
                Gen.Select(Gen.Elements(true, false), useAnd =>
                    CombinePredicates(pred1, pred2, useAnd))));

        var threePredicatesGen = Gen.SelectMany(simpleGen, pred1 =>
            Gen.SelectMany(simpleGen, pred2 =>
                Gen.SelectMany(simpleGen, pred3 =>
                    Gen.Select(Gen.Elements(true, false, true, false), useAnd =>
                        CombineThreePredicates(pred1, pred2, pred3, useAnd)))));

        return Gen.OneOf(twoPredicatesGen, threePredicatesGen);
    }

    private static Expression<Func<TestEntity, bool>> CombinePredicates(
        Expression<Func<TestEntity, bool>> pred1,
        Expression<Func<TestEntity, bool>> pred2,
        bool useAnd)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");

        // Replace parameters in both predicates
        var body1 = new ParameterReplacer(pred1.Parameters[0], parameter).Visit(pred1.Body);
        var body2 = new ParameterReplacer(pred2.Parameters[0], parameter).Visit(pred2.Body);

        var combined = useAnd
            ? Expression.AndAlso(body1!, body2!)
            : Expression.OrElse(body1!, body2!);

        return Expression.Lambda<Func<TestEntity, bool>>(combined, parameter);
    }

    private static Expression<Func<TestEntity, bool>> CombineThreePredicates(
        Expression<Func<TestEntity, bool>> pred1,
        Expression<Func<TestEntity, bool>> pred2,
        Expression<Func<TestEntity, bool>> pred3,
        bool useAnd)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");

        var body1 = new ParameterReplacer(pred1.Parameters[0], parameter).Visit(pred1.Body);
        var body2 = new ParameterReplacer(pred2.Parameters[0], parameter).Visit(pred2.Body);
        var body3 = new ParameterReplacer(pred3.Parameters[0], parameter).Visit(pred3.Body);

        // Combine first two
        var partial = useAnd
            ? Expression.AndAlso(body1!, body2!)
            : Expression.OrElse(body1!, body2!);

        // Combine with third
        var combined = useAnd
            ? Expression.AndAlso(partial, body3!)
            : Expression.OrElse(partial, body3!);

        return Expression.Lambda<Func<TestEntity, bool>>(combined, parameter);
    }

    #endregion

    #region Complex Predicates (Nested + Functions + NOT)

    /// <summary>
    /// Generates complex predicates with:
    /// - Nested property access (x.Address.City)
    /// - DynamoDB functions (.StartsWith(), .Contains())
    /// - Logical NOT (!)
    /// </summary>
    private static Gen<Expression<Func<TestEntity, bool>>> ComplexPredicateGen()
    {
        return Gen.OneOf(
            NestedPropertyPredicateGen(),
            StringFunctionPredicateGen(),
            NotPredicateGen(),
            CombinedComplexPredicateGen()
        );
    }

    private static Gen<Expression<Func<TestEntity, bool>>> NestedPropertyPredicateGen()
    {
        var nestedPaths = new[]
        {
            new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Address))!, typeof(Address).GetProperty(nameof(Address.City))! },
            new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Address))!, typeof(Address).GetProperty(nameof(Address.Street))! },
            new[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Address))!, typeof(Address).GetProperty(nameof(Address.ZipCode))! },
            new[]
            {
                typeof(TestEntity).GetProperty(nameof(TestEntity.Address))!,
                typeof(Address).GetProperty(nameof(Address.Country))!,
                typeof(Country).GetProperty(nameof(Country.Code))!
            },
            new[]
            {
                typeof(TestEntity).GetProperty(nameof(TestEntity.Address))!,
                typeof(Address).GetProperty(nameof(Address.Country))!,
                typeof(Country).GetProperty(nameof(Country.Name))!
            }
        };

        var pathGen = Gen.Elements(nestedPaths);
        var valueGen = Gen.Elements("", "test", "foo", "US", "Portland", "12345");

        return Gen.SelectMany(pathGen, path =>
            Gen.SelectMany(valueGen, value =>
                Gen.Select(Gen.Elements(true, false), useEquality =>
                    CreateNestedPropertyComparison(path, value, useEquality))));
    }

    private static Expression<Func<TestEntity, bool>> CreateNestedPropertyComparison(PropertyInfo[] path, string value, bool useEquality)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var propertyAccess = CreateNestedPropertyAccess(parameter, path);
        var constant = Expression.Constant(value, typeof(string));

        var comparison = useEquality
            ? Expression.Equal(propertyAccess, constant)
            : Expression.NotEqual(propertyAccess, constant);

        return Expression.Lambda<Func<TestEntity, bool>>(comparison, parameter);
    }

    private static Gen<Expression<Func<TestEntity, bool>>> StringFunctionPredicateGen()
    {
        var properties = new[]
        {
            typeof(TestEntity).GetProperty(nameof(TestEntity.Name))!,
            typeof(TestEntity).GetProperty(nameof(TestEntity.Title))!,
            typeof(TestEntity).GetProperty(nameof(TestEntity.Status))!
        };

        var propGen = Gen.Elements(properties);
        var valueGen = Gen.Elements("test", "foo", "bar", "alpha");

        return Gen.SelectMany(propGen, prop =>
            Gen.SelectMany(valueGen, value =>
                Gen.Select(Gen.Elements(0, 1), funcType =>
                    CreateStringFunctionPredicate(prop, value, funcType))));
    }

    private static Expression<Func<TestEntity, bool>> CreateStringFunctionPredicate(PropertyInfo property, string value, int funcType)
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var propertyAccess = Expression.Property(parameter, property);
        var constant = Expression.Constant(value, typeof(string));

        MethodInfo method;
        if (funcType == 0)
        {
            // StartsWith
            method = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;
        }
        else
        {
            // Contains
            method = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
        }

        var methodCall = Expression.Call(propertyAccess, method, constant);
        return Expression.Lambda<Func<TestEntity, bool>>(methodCall, parameter);
    }

    private static Gen<Expression<Func<TestEntity, bool>>> NotPredicateGen()
    {
        var simpleGen = SimplePredicateGen();

        return Gen.Select(simpleGen, pred =>
        {
            var parameter = Expression.Parameter(typeof(TestEntity), "x");
            var body = new ParameterReplacer(pred.Parameters[0], parameter).Visit(pred.Body);
            var notExpression = Expression.Not(body!);
            return Expression.Lambda<Func<TestEntity, bool>>(notExpression, parameter);
        });
    }

    private static Gen<Expression<Func<TestEntity, bool>>> CombinedComplexPredicateGen()
    {
        var nestedGen = NestedPropertyPredicateGen();
        var funcGen = StringFunctionPredicateGen();
        var notGen = NotPredicateGen();

        return Gen.SelectMany(Gen.OneOf(nestedGen, funcGen), pred1 =>
            Gen.SelectMany(Gen.OneOf(nestedGen, notGen), pred2 =>
                Gen.Select(Gen.Elements(true, false), useAnd =>
                    CombinePredicates(pred1, pred2, useAnd))));
    }

    #endregion

    #region Helper Methods

    private static Expression CreateNestedPropertyAccess(Expression root, PropertyInfo[] properties)
    {
        Expression current = root;

        foreach (var property in properties)
        {
            current = Expression.Property(current, property);
        }

        return current;
    }

    /// <summary>
    /// Expression visitor that replaces parameter references.
    /// </summary>
    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParameter ? _newParameter : base.VisitParameter(node);
        }
    }

    #endregion
}
