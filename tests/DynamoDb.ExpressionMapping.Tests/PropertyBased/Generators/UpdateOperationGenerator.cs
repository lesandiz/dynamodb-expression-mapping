using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FsCheck;
using Gen = FsCheck.Fluent.Gen;
using Arb = FsCheck.Fluent.Arb;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased.Generators;

/// <summary>
/// FsCheck generator for update operation sequences.
/// Generates Action&lt;UpdateExpressionBuilder&lt;TestEntity&gt;&gt; across three complexity tiers.
/// </summary>
public static class UpdateOperationGenerator
{
    /// <summary>
    /// Generates random update operation sequences for TestEntity.
    /// </summary>
    /// <param name="complexity">Tier: Simple (single operation), Composite (2-3 operations), Complex (mixed clauses).</param>
    public static Arbitrary<Action<UpdateExpressionBuilder<TestEntity>>> Generate(Complexity complexity = Complexity.Simple)
    {
        var generator = complexity switch
        {
            Complexity.Simple => SimpleOperationGen(),
            Complexity.Composite => CompositeOperationGen(),
            Complexity.Complex => ComplexOperationGen(),
            _ => throw new ArgumentOutOfRangeException(nameof(complexity), complexity, "Invalid complexity tier")
        };

        return Arb.From(generator);
    }

    #region Simple Operations (Single Operation)

    /// <summary>
    /// Generates single update operation from one of: SET, REMOVE, ADD, DELETE
    /// </summary>
    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> SimpleOperationGen()
    {
        return Gen.OneOf(
            SetOperationGen(),
            RemoveOperationGen(),
            IncrementDecrementOperationGen(),
            SetIfNotExistsOperationGen(),
            AddOperationGen(),
            DeleteOperationGen()
        );
    }

    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> SetOperationGen()
    {
        return Gen.OneOf(
            // Set string property
            Gen.SelectMany(Gen.Elements("", "test", "foo", "bar", "updated", "new-value"), value =>
                Gen.Select(Gen.Elements(0, 1, 2, 3), propIndex =>
                {
                    Action<UpdateExpressionBuilder<TestEntity>> action = propIndex switch
                    {
                        0 => builder => builder.Set(x => x.OrderId, value),
                        1 => builder => builder.Set(x => x.CustomerId, value),
                        2 => builder => builder.Set(x => x.Title, value),
                        _ => builder => builder.Set(x => x.Name, value) // Reserved keyword
                    };
                    return action;
                })),

            // Set decimal property
            Gen.SelectMany(Gen.Elements(0m, 1m, 10m, 99.99m, 100m, 1000m), value =>
                Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                    builder => builder.Set(x => x.Price, value))),

            // Set boolean property
            Gen.SelectMany(Gen.Elements(true, false), value =>
                Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                    builder => builder.Set(x => x.IsActive, value))),

            // Set DateTime property
            Gen.SelectMany(Gen.Elements(DateTime.MinValue, DateTime.UtcNow, new DateTime(2024, 1, 1)), value =>
                Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                    builder => builder.Set(x => x.StartDate, value))),

            // Set nullable DateTime property
            Gen.SelectMany(Gen.Elements<DateTime?>(null, DateTime.MinValue, DateTime.UtcNow), value =>
                Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                    builder => builder.Set(x => x.EndDate, value)))
        );
    }

    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> RemoveOperationGen()
    {
        return Gen.Select(Gen.Elements(0, 1, 2, 3, 4), propIndex =>
        {
            Action<UpdateExpressionBuilder<TestEntity>> action = propIndex switch
            {
                0 => builder => builder.Remove(x => x.Title),
                1 => builder => builder.Remove(x => x.EndDate),
                2 => builder => builder.Remove(x => x.Tags),
                3 => builder => builder.Remove(x => x.Address),
                _ => builder => builder.Remove(x => x.Status) // Reserved keyword
            };
            return action;
        });
    }

    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> IncrementDecrementOperationGen()
    {
        var incrementGen = Gen.SelectMany(Gen.Elements(1m, 5m, 10m, 100m), amount =>
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Increment(x => x.Price, amount)));

        var decrementGen = Gen.SelectMany(Gen.Elements(1m, 5m, 10m, 50m), amount =>
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Decrement(x => x.Price, amount)));

        return Gen.OneOf(incrementGen, decrementGen);
    }

    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> SetIfNotExistsOperationGen()
    {
        return Gen.OneOf(
            // SetIfNotExists for string
            Gen.SelectMany(Gen.Elements("default", "fallback", "initial"), value =>
                Gen.Select(Gen.Elements(0, 1), propIndex =>
                {
                    Action<UpdateExpressionBuilder<TestEntity>> action = propIndex switch
                    {
                        0 => builder => builder.SetIfNotExists(x => x.Title, value),
                        _ => builder => builder.SetIfNotExists(x => x.Name, value)
                    };
                    return action;
                })),

            // SetIfNotExists for decimal
            Gen.SelectMany(Gen.Elements(0m, 1m, 10m), value =>
                Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                    builder => builder.SetIfNotExists(x => x.Price, value))),

            // SetIfNotExists for boolean
            Gen.SelectMany(Gen.Elements(true, false), value =>
                Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                    builder => builder.SetIfNotExists(x => x.IsActive, value)))
        );
    }

    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> AddOperationGen()
    {
        // ADD is used for number sets or incrementing numeric values in DynamoDB
        // For simplicity, we'll generate ADD operations for decimal properties
        return Gen.SelectMany(Gen.Elements(1m, 5m, 10m, 100m), value =>
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Add(x => x.Score, (int)value)));
    }

    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> DeleteOperationGen()
    {
        // DELETE is used for removing elements from a set
        // For TestEntity, we'll use Tags (string array/set)
        return Gen.SelectMany(
            Gen.Elements(
                new[] { "tag1" },
                new[] { "tag2", "tag3" },
                new[] { "test" }
            ),
            tags =>
                Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                    builder => builder.Delete(x => x.EnabledFeatures, new HashSet<string>(tags))));
    }

    #endregion

    #region Composite Operations (2-3 Operations)

    /// <summary>
    /// Generates 2-3 chained update operations.
    /// Ensures no conflicting operations on the same property.
    /// </summary>
    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> CompositeOperationGen()
    {
        var twoOpsGen = Gen.SelectMany(GetNonConflictingOperation(), op1 =>
            Gen.Select(GetNonConflictingOperation(), op2 =>
            {
                Action<UpdateExpressionBuilder<TestEntity>> combined = builder =>
                {
                    op1(builder);
                    op2(builder);
                };
                return combined;
            }));

        var threeOpsGen = Gen.SelectMany(GetNonConflictingOperation(), op1 =>
            Gen.SelectMany(GetNonConflictingOperation(), op2 =>
                Gen.Select(GetNonConflictingOperation(), op3 =>
                {
                    Action<UpdateExpressionBuilder<TestEntity>> combined = builder =>
                    {
                        op1(builder);
                        op2(builder);
                        op3(builder);
                    };
                    return combined;
                })));

        return Gen.OneOf(twoOpsGen, threeOpsGen);
    }

    /// <summary>
    /// Returns a generator that produces operations on different properties to avoid conflicts.
    /// Strategy: Use a diverse set of operations on different properties.
    /// </summary>
    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> GetNonConflictingOperation()
    {
        return Gen.OneOf(
            // Set operations on different string properties
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Set(x => x.OrderId, Guid.NewGuid().ToString())),
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Set(x => x.CustomerId, "CUST-" + Random.Shared.Next(1000))),
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Set(x => x.Title, "Title-" + Random.Shared.Next(100))),
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Set(x => x.Name, "Name-" + Random.Shared.Next(100))),

            // Increment/Decrement on Price
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Increment(x => x.Price, 10m)),
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Decrement(x => x.Price, 5m)),

            // Set boolean
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Set(x => x.IsActive, Random.Shared.Next(2) == 0)),

            // Set DateTime
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Set(x => x.StartDate, DateTime.UtcNow)),

            // Remove nullable properties
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Remove(x => x.EndDate)),
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Remove(x => x.Address))
        );
    }

    #endregion

    #region Complex Operations (Mixed Clause Types)

    /// <summary>
    /// Generates complex update operations combining multiple clause types: SET, REMOVE, ADD, DELETE
    /// Example: SET Price = 100, Name = "foo" REMOVE EndDate ADD Price 10 DELETE Tags {"tag1"}
    /// </summary>
    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> ComplexOperationGen()
    {
        // Simplified: Generate operations with 2-3 different clause types
        var setAndRemoveGen = Gen.SelectMany(GetSetClauseOperation(), setOp =>
            Gen.Select(GetRemoveClauseOperation(), removeOp =>
            {
                Action<UpdateExpressionBuilder<TestEntity>> combined = builder =>
                {
                    setOp(builder);
                    removeOp(builder);
                };
                return combined;
            }));

        var setRemoveAndAddGen = Gen.SelectMany(GetSetClauseOperation(), setOp =>
            Gen.SelectMany(GetRemoveClauseOperation(), removeOp =>
                Gen.Select(GetAddClauseOperation(), addOp =>
                {
                    Action<UpdateExpressionBuilder<TestEntity>> combined = builder =>
                    {
                        setOp(builder);
                        removeOp(builder);
                        addOp(builder);
                    };
                    return combined;
                })));

        var setRemoveAndDeleteGen = Gen.SelectMany(GetSetClauseOperation(), setOp =>
            Gen.SelectMany(GetRemoveClauseOperation(), removeOp =>
                Gen.Select(GetDeleteClauseOperation(), deleteOp =>
                {
                    Action<UpdateExpressionBuilder<TestEntity>> combined = builder =>
                    {
                        setOp(builder);
                        removeOp(builder);
                        deleteOp(builder);
                    };
                    return combined;
                })));

        // All four clause types
        var allClausesGen = Gen.SelectMany(GetSetClauseOperation(), setOp =>
            Gen.SelectMany(GetRemoveClauseOperation(), removeOp =>
                Gen.SelectMany(GetAddClauseOperation(), addOp =>
                    Gen.Select(GetDeleteClauseOperation(), deleteOp =>
                    {
                        Action<UpdateExpressionBuilder<TestEntity>> combined = builder =>
                        {
                            setOp(builder);
                            removeOp(builder);
                            addOp(builder);
                            deleteOp(builder);
                        };
                        return combined;
                    }))));

        return Gen.OneOf(setAndRemoveGen, setRemoveAndAddGen, setRemoveAndDeleteGen, allClausesGen);
    }
    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> GetSetClauseOperation()
    {
        return Gen.OneOf(
            // Multiple SET operations
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(builder =>
            {
                builder.Set(x => x.OrderId, "ORD-" + Guid.NewGuid());
                builder.Set(x => x.Title, "Updated Title");
                builder.Increment(x => x.Price, 10m);
            }),
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(builder =>
            {
                builder.Set(x => x.Name, "New Name");
                builder.Set(x => x.Status, "Active");
                builder.Set(x => x.IsActive, true);
            }),
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(builder =>
            {
                builder.SetIfNotExists(x => x.Title, "Default Title");
                builder.Set(x => x.StartDate, DateTime.UtcNow);
            })
        );
    }

    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> GetRemoveClauseOperation()
    {
        return Gen.OneOf(
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Remove(x => x.EndDate)),
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Remove(x => x.Address)),
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(builder =>
            {
                builder.Remove(x => x.EndDate);
                builder.Remove(x => x.Address);
            })
        );
    }

    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> GetAddClauseOperation()
    {
        return Gen.SelectMany(Gen.Elements(1m, 5m, 10m, 100m), value =>
            Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                builder => builder.Add(x => x.Score, (int)value)));
    }

    private static Gen<Action<UpdateExpressionBuilder<TestEntity>>> GetDeleteClauseOperation()
    {
        return Gen.SelectMany(
            Gen.Elements(
                new[] { "tag1" },
                new[] { "tag2", "tag3" },
                new[] { "test", "foo" }
            ),
            tags =>
                Gen.Constant<Action<UpdateExpressionBuilder<TestEntity>>>(
                    builder => builder.Delete(x => x.EnabledFeatures, new HashSet<string>(tags))));
    }

    #endregion
}
