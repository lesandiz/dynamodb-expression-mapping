using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.PropertyBased;

[Trait("Category", "Property")]
public class PropertyTestConfigTests
{
    [Fact]
    public void MaxTest_WhenNoEnvironmentVariable_ReturnsDefaultMaxTest()
    {
        // Arrange
        Environment.SetEnvironmentVariable("FSCHECK_MAX_TEST", null);

        // Act
        var result = PropertyTestConfig.MaxTest;

        // Assert
        Assert.Equal(PropertyTestConfig.DefaultMaxTest, result);
    }

    [Fact]
    public void MaxTest_WhenEnvironmentVariableSet_ReturnsEnvironmentValue()
    {
        // Arrange
        const int expected = 5000;
        Environment.SetEnvironmentVariable("FSCHECK_MAX_TEST", expected.ToString());

        try
        {
            // Act
            var result = PropertyTestConfig.MaxTest;

            // Assert
            Assert.Equal(expected, result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("FSCHECK_MAX_TEST", null);
        }
    }

    [Fact]
    public void MaxTest_WhenEnvironmentVariableInvalid_ReturnsDefaultMaxTest()
    {
        // Arrange
        Environment.SetEnvironmentVariable("FSCHECK_MAX_TEST", "invalid");

        try
        {
            // Act
            var result = PropertyTestConfig.MaxTest;

            // Assert
            Assert.Equal(PropertyTestConfig.DefaultMaxTest, result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("FSCHECK_MAX_TEST", null);
        }
    }

    [Fact]
    public void MaxTest_WhenEnvironmentVariableNegative_ReturnsDefaultMaxTest()
    {
        // Arrange
        Environment.SetEnvironmentVariable("FSCHECK_MAX_TEST", "-100");

        try
        {
            // Act
            var result = PropertyTestConfig.MaxTest;

            // Assert
            Assert.Equal(PropertyTestConfig.DefaultMaxTest, result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("FSCHECK_MAX_TEST", null);
        }
    }

    [Fact]
    public void DefaultMaxTest_Is100()
    {
        Assert.Equal(100, PropertyTestConfig.DefaultMaxTest);
    }
}
