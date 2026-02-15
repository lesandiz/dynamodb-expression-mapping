namespace DynamoDb.ExpressionMapping;

/// <summary>
/// Provides static marker methods for DynamoDB-specific functions used in filter and condition expressions.
/// These methods are only valid within expression trees and will throw if invoked directly at runtime.
/// </summary>
public static class DynamoDbFunctions
{
    /// <summary>
    /// Expression tree marker for the DynamoDB attribute_exists() function.
    /// Evaluates to true if the attribute exists in the item.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="property">The property to check for existence.</param>
    /// <returns>True if the attribute exists; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Always thrown when invoked at runtime. This method is an expression tree marker only.</exception>
    public static bool AttributeExists<T>(T property)
    {
        throw new InvalidOperationException("Expression marker only");
    }

    /// <summary>
    /// Expression tree marker for the DynamoDB attribute_not_exists() function.
    /// Evaluates to true if the attribute does not exist in the item.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="property">The property to check for non-existence.</param>
    /// <returns>True if the attribute does not exist; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Always thrown when invoked at runtime. This method is an expression tree marker only.</exception>
    public static bool AttributeNotExists<T>(T property)
    {
        throw new InvalidOperationException("Expression marker only");
    }

    /// <summary>
    /// Expression tree marker for the DynamoDB BETWEEN operator.
    /// Evaluates to true if the property value is between the low and high values (inclusive).
    /// </summary>
    /// <typeparam name="T">The comparable property type.</typeparam>
    /// <param name="property">The property to compare.</param>
    /// <param name="low">The lower bound (inclusive).</param>
    /// <param name="high">The upper bound (inclusive).</param>
    /// <returns>True if the property value is between low and high; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Always thrown when invoked at runtime. This method is an expression tree marker only.</exception>
    public static bool Between<T>(T property, T low, T high) where T : IComparable<T>
    {
        throw new InvalidOperationException("Expression marker only");
    }

    /// <summary>
    /// Expression tree marker for the DynamoDB size() function.
    /// Returns the size of the attribute (string length, binary length, or collection element count).
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="property">The property to measure.</param>
    /// <returns>The size of the attribute.</returns>
    /// <exception cref="InvalidOperationException">Always thrown when invoked at runtime. This method is an expression tree marker only.</exception>
    public static int Size<T>(T property)
    {
        throw new InvalidOperationException("Expression marker only");
    }

    /// <summary>
    /// Expression tree marker for the DynamoDB attribute_type() function.
    /// Evaluates to true if the attribute is of the specified DynamoDB type.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="property">The property to check.</param>
    /// <param name="dynamoDbType">The DynamoDB type code (S, N, B, SS, NS, BS, M, L, NULL, BOOL).</param>
    /// <returns>True if the attribute matches the specified type; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Always thrown when invoked at runtime. This method is an expression tree marker only.</exception>
    public static bool AttributeType<T>(T property, string dynamoDbType)
    {
        throw new InvalidOperationException("Expression marker only");
    }
}
