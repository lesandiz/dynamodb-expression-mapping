using System.Reflection;
using DynamoDb.ExpressionMapping.Expressions;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

public class PropertyPathTests
{
    [Fact]
    public void Constructor_WithValidArguments_SetsPropertiesCorrectly()
    {
        // Arrange
        var propInfo = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
        var segments = new[] { "Name" };
        var segmentProperties = new[] { propInfo };

        // Act
        var path = new PropertyPath(segments, segmentProperties);

        // Assert
        path.Segments.Should().Equal(segments);
        path.SegmentProperties.Should().Equal(segmentProperties);
        path.FullPath.Should().Be("Name");
        path.LeafName.Should().Be("Name");
        path.PropertyInfo.Should().BeSameAs(propInfo);
        path.IsNested.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNestedPath_SetsPropertiesCorrectly()
    {
        // Arrange
        var addressProp = typeof(TestClass).GetProperty(nameof(TestClass.Address))!;
        var cityProp = typeof(Address).GetProperty(nameof(Address.City))!;
        var segments = new[] { "Address", "City" };
        var segmentProperties = new[] { addressProp, cityProp };

        // Act
        var path = new PropertyPath(segments, segmentProperties);

        // Assert
        path.Segments.Should().Equal("Address", "City");
        path.SegmentProperties.Should().Equal(addressProp, cityProp);
        path.FullPath.Should().Be("Address.City");
        path.LeafName.Should().Be("City");
        path.PropertyInfo.Should().BeSameAs(cityProp);
        path.IsNested.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullSegments_ThrowsArgumentException()
    {
        // Arrange
        var propInfo = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;

        // Act
        var act = () => new PropertyPath(null!, new[] { propInfo });

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("segments");
    }

    [Fact]
    public void Constructor_WithEmptySegments_ThrowsArgumentException()
    {
        // Arrange
        var propInfo = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;

        // Act
        var act = () => new PropertyPath(Array.Empty<string>(), new[] { propInfo });

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("segments");
    }

    [Fact]
    public void Constructor_WithNullSegmentProperties_ThrowsArgumentException()
    {
        // Act
        var act = () => new PropertyPath(new[] { "Name" }, null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("segmentProperties");
    }

    [Fact]
    public void Constructor_WithEmptySegmentProperties_ThrowsArgumentException()
    {
        // Act
        var act = () => new PropertyPath(new[] { "Name" }, Array.Empty<PropertyInfo>());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("segmentProperties");
    }

    [Fact]
    public void Constructor_WithMismatchedCounts_ThrowsArgumentException()
    {
        // Arrange
        var propInfo = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;

        // Act
        var act = () => new PropertyPath(new[] { "Name", "Other" }, new[] { propInfo });

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Segments and SegmentProperties must have the same count");
    }

    [Fact]
    public void Equals_WithSameFullPath_ReturnsTrue()
    {
        // Arrange
        var propInfo = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
        var path1 = new PropertyPath(new[] { "Name" }, new[] { propInfo });
        var path2 = new PropertyPath(new[] { "Name" }, new[] { propInfo });

        // Act & Assert
        path1.Equals(path2).Should().BeTrue();
        (path1 == path2).Should().BeFalse(); // Different object references
    }

    [Fact]
    public void Equals_WithDifferentFullPath_ReturnsFalse()
    {
        // Arrange
        var nameProp = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
        var ageProp = typeof(TestClass).GetProperty(nameof(TestClass.Age))!;
        var path1 = new PropertyPath(new[] { "Name" }, new[] { nameProp });
        var path2 = new PropertyPath(new[] { "Age" }, new[] { ageProp });

        // Act & Assert
        path1.Equals(path2).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_WithSameFullPath_ReturnsSameHashCode()
    {
        // Arrange
        var propInfo = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
        var path1 = new PropertyPath(new[] { "Name" }, new[] { propInfo });
        var path2 = new PropertyPath(new[] { "Name" }, new[] { propInfo });

        // Act & Assert
        path1.GetHashCode().Should().Be(path2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsFullPath()
    {
        // Arrange
        var addressProp = typeof(TestClass).GetProperty(nameof(TestClass.Address))!;
        var cityProp = typeof(Address).GetProperty(nameof(Address.City))!;
        var path = new PropertyPath(new[] { "Address", "City" }, new[] { addressProp, cityProp });

        // Act
        var result = path.ToString();

        // Assert
        result.Should().Be("Address.City");
    }

    // Test helper classes
    private class TestClass
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public Address? Address { get; set; }
    }

    private class Address
    {
        public string City { get; set; } = string.Empty;
    }
}
