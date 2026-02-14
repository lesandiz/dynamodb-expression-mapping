using DynamoDb.ExpressionMapping.ReservedKeywords;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.ReservedKeywords;

public class AliasGeneratorTests
{
    [Theory]
    [InlineData("proj")]
    [InlineData("filt")]
    [InlineData("cond")]
    [InlineData("upd")]
    [InlineData("key")]
    public void NextName_ShouldGenerateSequentialAliases(string scope)
    {
        // Arrange
        var generator = new AliasGenerator(scope);

        // Act
        var alias0 = generator.NextName();
        var alias1 = generator.NextName();
        var alias2 = generator.NextName();

        // Assert
        alias0.Should().Be($"#{scope}_0");
        alias1.Should().Be($"#{scope}_1");
        alias2.Should().Be($"#{scope}_2");
    }

    [Theory]
    [InlineData("proj")]
    [InlineData("filt")]
    [InlineData("cond")]
    [InlineData("upd")]
    [InlineData("key")]
    public void NextValue_ShouldGenerateSequentialPlaceholders(string scope)
    {
        // Arrange
        var generator = new AliasGenerator(scope);

        // Act
        var value0 = generator.NextValue();
        var value1 = generator.NextValue();
        var value2 = generator.NextValue();

        // Assert
        value0.Should().Be($":{scope}_v0");
        value1.Should().Be($":{scope}_v1");
        value2.Should().Be($":{scope}_v2");
    }

    [Fact]
    public void NextName_ShouldStartAtZero()
    {
        // Arrange
        var generator = new AliasGenerator("test");

        // Act
        var firstAlias = generator.NextName();

        // Assert
        firstAlias.Should().Be("#test_0", "first alias should start at index 0");
    }

    [Fact]
    public void NextValue_ShouldStartAtZero()
    {
        // Arrange
        var generator = new AliasGenerator("test");

        // Act
        var firstValue = generator.NextValue();

        // Assert
        firstValue.Should().Be(":test_v0", "first value should start at index 0");
    }

    [Fact]
    public void NextName_AndNextValue_ShouldHaveIndependentCounters()
    {
        // Arrange
        var generator = new AliasGenerator("test");

        // Act
        var name0 = generator.NextName();
        var value0 = generator.NextValue();
        var value1 = generator.NextValue();
        var name1 = generator.NextName();

        // Assert
        name0.Should().Be("#test_0");
        name1.Should().Be("#test_1");
        value0.Should().Be(":test_v0");
        value1.Should().Be(":test_v1");
    }

    [Fact]
    public void Reset_ShouldResetBothCountersToZero()
    {
        // Arrange
        var generator = new AliasGenerator("test");

        // Generate some aliases to increment counters
        generator.NextName();
        generator.NextName();
        generator.NextValue();
        generator.NextValue();
        generator.NextValue();

        // Act
        generator.Reset();
        var nameAfterReset = generator.NextName();
        var valueAfterReset = generator.NextValue();

        // Assert
        nameAfterReset.Should().Be("#test_0", "name counter should reset to 0");
        valueAfterReset.Should().Be(":test_v0", "value counter should reset to 0");
    }

    [Fact]
    public void DifferentScopes_ShouldProduceDifferentPrefixes()
    {
        // Arrange
        var projGenerator = new AliasGenerator("proj");
        var filtGenerator = new AliasGenerator("filt");
        var condGenerator = new AliasGenerator("cond");
        var updGenerator = new AliasGenerator("upd");
        var keyGenerator = new AliasGenerator("key");

        // Act
        var projName = projGenerator.NextName();
        var filtName = filtGenerator.NextName();
        var condName = condGenerator.NextName();
        var updName = updGenerator.NextName();
        var keyName = keyGenerator.NextName();

        var projValue = projGenerator.NextValue();
        var filtValue = filtGenerator.NextValue();
        var condValue = condGenerator.NextValue();
        var updValue = updGenerator.NextValue();
        var keyValue = keyGenerator.NextValue();

        // Assert
        projName.Should().Be("#proj_0");
        filtName.Should().Be("#filt_0");
        condName.Should().Be("#cond_0");
        updName.Should().Be("#upd_0");
        keyName.Should().Be("#key_0");

        projValue.Should().Be(":proj_v0");
        filtValue.Should().Be(":filt_v0");
        condValue.Should().Be(":cond_v0");
        updValue.Should().Be(":upd_v0");
        keyValue.Should().Be(":key_v0");
    }

    [Fact]
    public void SameScopeInstances_ShouldHaveIndependentCounters()
    {
        // Arrange
        var generator1 = new AliasGenerator("test");
        var generator2 = new AliasGenerator("test");

        // Act
        var gen1First = generator1.NextName();
        var gen1Second = generator1.NextName();
        var gen2First = generator2.NextName();

        // Assert
        gen1First.Should().Be("#test_0");
        gen1Second.Should().Be("#test_1");
        gen2First.Should().Be("#test_0", "separate instances should have independent counters");
    }

    [Fact]
    public void NextName_ShouldHandleLargeNumberOfAliases()
    {
        // Arrange
        var generator = new AliasGenerator("test");

        // Act - generate 1000 aliases
        string? lastAlias = null;
        for (int i = 0; i < 1000; i++)
        {
            lastAlias = generator.NextName();
        }

        // Assert
        lastAlias.Should().Be("#test_999", "should handle large index numbers");
    }

    [Fact]
    public void NextValue_ShouldHandleLargeNumberOfPlaceholders()
    {
        // Arrange
        var generator = new AliasGenerator("test");

        // Act - generate 1000 value placeholders
        string? lastValue = null;
        for (int i = 0; i < 1000; i++)
        {
            lastValue = generator.NextValue();
        }

        // Assert
        lastValue.Should().Be(":test_v999", "should handle large index numbers");
    }

    [Theory]
    [InlineData("custom")]
    [InlineData("myapp")]
    [InlineData("v2")]
    [InlineData("")]  // edge case: empty scope
    public void Constructor_ShouldAcceptCustomScopes(string scope)
    {
        // Arrange & Act
        var generator = new AliasGenerator(scope);
        var name = generator.NextName();
        var value = generator.NextValue();

        // Assert
        name.Should().Be($"#{scope}_0");
        value.Should().Be($":{scope}_v0");
    }

    [Fact]
    public void PredefinedScopes_ShouldFollowSpecification()
    {
        // This test documents the official scopes from Spec 08 §3

        // Projection scope (no value aliases needed for projections)
        var proj = new AliasGenerator("proj");
        proj.NextName().Should().Be("#proj_0");
        proj.NextValue().Should().Be(":proj_v0");  // Available but typically unused

        // Filter scope
        var filt = new AliasGenerator("filt");
        filt.NextName().Should().Be("#filt_0");
        filt.NextValue().Should().Be(":filt_v0");

        // Condition scope
        var cond = new AliasGenerator("cond");
        cond.NextName().Should().Be("#cond_0");
        cond.NextValue().Should().Be(":cond_v0");

        // Update scope
        var upd = new AliasGenerator("upd");
        upd.NextName().Should().Be("#upd_0");
        upd.NextValue().Should().Be(":upd_v0");

        // Key condition scope
        var key = new AliasGenerator("key");
        key.NextName().Should().Be("#key_0");
        key.NextValue().Should().Be(":key_v0");
    }
}
