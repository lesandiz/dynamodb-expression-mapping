namespace DynamoDb.ExpressionMapping.Tests.PropertyBased;

/// <summary>
/// Configuration for FsCheck property-based tests.
/// Controls the number of test cases generated per property.
/// </summary>
public static class PropertyTestConfig
{
    /// <summary>
    /// Default maximum test count: 100 cases (fast local feedback).
    /// CI sets FSCHECK_MAX_TEST=10000 for full validation runs.
    /// </summary>
    public const int DefaultMaxTest = 100;

    /// <summary>
    /// Gets the effective maximum test count based on environment configuration.
    /// Reads from FSCHECK_MAX_TEST environment variable; defaults to <see cref="DefaultMaxTest"/>.
    /// </summary>
    public static int MaxTest
    {
        get
        {
            var envValue = Environment.GetEnvironmentVariable("FSCHECK_MAX_TEST");
            if (!string.IsNullOrWhiteSpace(envValue) && int.TryParse(envValue, out var parsed) && parsed > 0)
            {
                return parsed;
            }
            return DefaultMaxTest;
        }
    }
}
