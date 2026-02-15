namespace DynamoDb.ExpressionMapping.Tests.PropertyBased;

/// <summary>
/// Configuration for FsCheck property-based tests.
/// Controls the number of test cases generated per property.
/// </summary>
public static class PropertyTestConfig
{
    /// <summary>
    /// Default maximum test count for local development: 10,000 cases.
    /// </summary>
    public const int DefaultMaxTest = 10_000;

    /// <summary>
    /// CI-friendly maximum test count: 1,000 cases.
    /// Balances execution speed with coverage.
    /// </summary>
    public const int QuietOnSuccessMaxTest = 1_000;

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
