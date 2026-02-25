using System.Linq.Expressions;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Expressions;

public class ProjectionExpressionVisitorTests
{
    #region Property Extraction Tests

    [Fact]
    public void SingleProperty_ExtractsOnePath()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.OrderId;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(1);
        paths[0].FullPath.Should().Be("OrderId");
        paths[0].LeafName.Should().Be("OrderId");
        paths[0].IsNested.Should().BeFalse();
    }

    [Fact]
    public void AnonymousType_ExtractsAllProperties()
    {
        // Arrange
        Expression<Func<TestEntity, object>> expr = p => new { p.OrderId, p.CustomerId, p.Title };

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(3);
        paths.Select(p => p.FullPath).Should().Equal("OrderId", "CustomerId", "Title");
    }

    [Fact]
    public void ObjectInitialiser_ExtractsSourceProperties()
    {
        // Arrange
        Expression<Func<TestEntity, OrderSummary>> expr = p => new OrderSummary
        {
            Id = p.OrderId,
            Name = p.Title,
            Price = p.Price
        };

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(3);
        paths.Select(p => p.FullPath).Should().Equal("OrderId", "Title", "Price");
    }

    [Fact]
    public void NestedProperty_ExtractsFullPath()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.Address!.City;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(1);
        paths[0].FullPath.Should().Be("Address.City");
        paths[0].LeafName.Should().Be("City");
        paths[0].IsNested.Should().BeTrue();
        paths[0].Segments.Should().Equal("Address", "City");
    }

    [Fact]
    public void IdentityExpression_ReturnsEmptyPaths()
    {
        // Arrange
        Expression<Func<TestEntity, TestEntity>> expr = p => p;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().BeEmpty();
    }

    // Note: ValueTuple literals are not supported in expression trees (compiler limitation)
    // Users can use anonymous types instead: p => new { p.OrderId, p.CustomerId }

    [Fact]
    public void DuplicateProperty_Deduplicated()
    {
        // Arrange
        Expression<Func<TestEntity, object>> expr = p => new { p.OrderId, Same = p.OrderId };

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(1);
        paths[0].FullPath.Should().Be("OrderId");
    }

    [Fact]
    public void IntermediateNode_NotAddedAsSeparatePath()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.Address!.City;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(1);
        paths[0].FullPath.Should().Be("Address.City");
        // Should NOT have a separate path for just "Address"
    }

    [Fact]
    public void MultipleNestedPaths_InSameAnonymousType_ExtractsAll()
    {
        // Arrange
        Expression<Func<TestEntity, object>> expr = p => new
        {
            p.Address!.City,
            p.Address.Street,
            p.OrderId
        };

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(3);
        paths.Select(p => p.FullPath).Should().Equal("Address.City", "Address.Street", "OrderId");
    }

    [Fact]
    public void DeeplyNestedPath_ThreeLevels_ExtractsFullPath()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.Address!.Country!.Code;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(1);
        paths[0].FullPath.Should().Be("Address.Country.Code");
        paths[0].Segments.Should().Equal("Address", "Country", "Code");
        paths[0].IsNested.Should().BeTrue();
    }

    #endregion

    #region Method Call Expression Tests

    [Fact]
    public void StaticMethodCall_EnumParse_ExtractsPropertyFromArgument()
    {
        // Arrange
        Expression<Func<TestEntity, object>> expr =
            p => new { Status = Enum.Parse<OrderStatus>(p.Status) };

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(1);
        paths[0].FullPath.Should().Be("Status");
    }

    [Fact]
    public void MultipleMethodCalls_ExtractsAllProperties()
    {
        // Arrange
        Expression<Func<TestEntity, object>> expr =
            p => new { Status = Enum.Parse<OrderStatus>(p.Status), p.OrderId };

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(2);
        paths.Select(p => p.FullPath).Should().Equal("Status", "OrderId");
    }

    [Fact]
    public void InstanceMethodCall_ToString_ExtractsProperty()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.Price.ToString();

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(1);
        paths[0].FullPath.Should().Be("Price");
    }

    [Fact]
    public void ChainedInstanceMethods_ExtractsUnderlyingProperty()
    {
        // Arrange — node.Object is itself a MethodCallExpression
        Expression<Func<TestEntity, string>> expr = p => p.Name.Trim().ToUpper();

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(1);
        paths[0].FullPath.Should().Be("Name");
    }

    [Fact]
    public void NestedMethodCalls_StaticWrappingInstance_ExtractsProperty()
    {
        // Arrange — method call as argument to another method call
        Expression<Func<TestEntity, object>> expr =
            p => new { Status = Enum.Parse<OrderStatus>(p.Status.Trim()) };

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(1);
        paths[0].FullPath.Should().Be("Status");
    }

    [Fact]
    public void CompositeWithMixedMethodCalls_ExtractsAllDistinctProperties()
    {
        // Arrange — chained calls, nested calls, and a plain property
        Expression<Func<TestEntity, object>> expr = p => new
        {
            Upper = p.Name.Trim().ToUpper(),
            Status = Enum.Parse<OrderStatus>(p.Status.Trim()),
            p.Price
        };

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(3);
        paths.Select(p => p.FullPath).Should().Equal("Name", "Status", "Price");
    }

    [Fact]
    public void StaticMethodWithMultiplePropertyArgs_ExtractsAllProperties()
    {
        // Arrange — string.Equals receives two distinct property arguments
        Expression<Func<TestEntity, bool>> expr =
            p => string.Equals(p.Name, p.Title);

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths.Should().HaveCount(2);
        paths.Select(p => p.FullPath).Should().Equal("Name", "Title");
    }

    #endregion

    #region Unsupported Expression Tests

    [Fact]
    public void Arithmetic_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()
    {
        // Arrange
        Expression<Func<TestEntity, decimal>> expr = p => p.Price * 1.1m;

        // Act
        var act = () => ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .Which.NodeType.Should().Be(ExpressionType.Multiply);
    }

    [Fact]
    public void Conditional_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()
    {
        // Arrange
        Expression<Func<TestEntity, DateTime>> expr = p => p.IsActive ? p.StartDate : p.EndDate!.Value;

        // Act
        var act = () => ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .Which.NodeType.Should().Be(ExpressionType.Conditional);
    }

    [Fact]
    public void ArrayIndex_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.Tags[0];

        // Act
        var act = () => ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .Which.NodeType.Should().Be(ExpressionType.ArrayIndex);
    }

    [Fact]
    public void StringConcatenation_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.Name + " " + p.Title;

        // Act
        var act = () => ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .Which.NodeType.Should().Be(ExpressionType.Add);
    }

    #endregion

    #region PropertyPath Metadata — SegmentProperties

    [Fact]
    public void SegmentProperties_SingleProperty_ContainsOneEntry()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.OrderId;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths[0].SegmentProperties.Should().HaveCount(1);
        paths[0].SegmentProperties[0].Name.Should().Be("OrderId");
        paths[0].SegmentProperties[0].DeclaringType.Should().Be(typeof(TestEntity));
    }

    [Fact]
    public void SegmentProperties_NestedPath_ContainsEntryPerSegment()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.Address!.City;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths[0].SegmentProperties.Should().HaveCount(2);
        paths[0].SegmentProperties[0].Name.Should().Be("Address");
        paths[0].SegmentProperties[1].Name.Should().Be("City");
    }

    [Fact]
    public void SegmentProperties_NestedPath_IntermediateDeclaringType_IsCorrect()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.Address!.City;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths[0].SegmentProperties[0].DeclaringType.Should().Be(typeof(TestEntity));
        paths[0].SegmentProperties[1].DeclaringType.Should().Be(typeof(Address));
    }

    [Fact]
    public void SegmentProperties_NestedPath_IntermediatePropertyType_MatchesNextDeclaringType()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.Address!.City;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        var addressProp = paths[0].SegmentProperties[0];
        var cityProp = paths[0].SegmentProperties[1];

        addressProp.PropertyType.Should().Be(typeof(Address));
        cityProp.DeclaringType.Should().Be(typeof(Address));
    }

    [Fact]
    public void SegmentProperties_DeeplyNested_ThreeLevels_ContainsThreeEntries()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.Address!.Country!.Code;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths[0].SegmentProperties.Should().HaveCount(3);
        paths[0].SegmentProperties[0].Name.Should().Be("Address");
        paths[0].SegmentProperties[1].Name.Should().Be("Country");
        paths[0].SegmentProperties[2].Name.Should().Be("Code");
    }

    [Fact]
    public void PropertyInfo_IsLastSegmentProperty()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.Address!.City;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr);

        // Assert
        paths[0].PropertyInfo.Should().BeSameAs(paths[0].SegmentProperties[^1]);
        paths[0].PropertyInfo.Name.Should().Be("City");
    }

    #endregion

    #region ProjectionShape Tests

    [Fact]
    public void ProjectionShape_Identity_ForWholeObject()
    {
        // Arrange
        Expression<Func<TestEntity, TestEntity>> expr = p => p;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr, out var shape);

        // Assert
        shape.Should().Be(ProjectionShape.Identity);
    }

    [Fact]
    public void ProjectionShape_SingleProperty_ForOneProperty()
    {
        // Arrange
        Expression<Func<TestEntity, string>> expr = p => p.OrderId;

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr, out var shape);

        // Assert
        shape.Should().Be(ProjectionShape.SingleProperty);
    }

    [Fact]
    public void ProjectionShape_Composite_ForAnonymousType()
    {
        // Arrange
        Expression<Func<TestEntity, object>> expr = p => new { p.OrderId, p.CustomerId };

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr, out var shape);

        // Assert
        shape.Should().Be(ProjectionShape.Composite);
    }

    [Fact]
    public void ProjectionShape_Composite_ForObjectInitialiser()
    {
        // Arrange
        Expression<Func<TestEntity, OrderSummary>> expr = p => new OrderSummary
        {
            Id = p.OrderId,
            Name = p.Title
        };

        // Act
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(expr, out var shape);

        // Assert
        shape.Should().Be(ProjectionShape.Composite);
    }

    #endregion
}
