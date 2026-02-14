using System.Linq.Expressions;
using DynamoDb.ExpressionMapping.Exceptions;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Exceptions;

public class ExceptionHierarchyTests
{
    #region Hierarchy Structure

    [Fact]
    public void AllExceptions_DeriveFromExpressionMappingException()
    {
        // Arrange & Act & Assert
        typeof(UnsupportedExpressionException).Should().BeDerivedFrom<ExpressionMappingException>();
        typeof(MissingConverterException).Should().BeDerivedFrom<ExpressionMappingException>();
        typeof(ExpressionAttributeConflictException).Should().BeDerivedFrom<ExpressionMappingException>();
        typeof(InvalidExpressionException).Should().BeDerivedFrom<ExpressionMappingException>();
        typeof(InvalidProjectionException).Should().BeDerivedFrom<ExpressionMappingException>();
        typeof(InvalidFilterException).Should().BeDerivedFrom<ExpressionMappingException>();
        typeof(InvalidUpdateException).Should().BeDerivedFrom<ExpressionMappingException>();
        typeof(InvalidKeyConditionException).Should().BeDerivedFrom<ExpressionMappingException>();
    }

    [Fact]
    public void InvalidProjectionException_DeriveFromInvalidExpressionException()
    {
        typeof(InvalidProjectionException).Should().BeDerivedFrom<InvalidExpressionException>();
    }

    [Fact]
    public void InvalidFilterException_DeriveFromInvalidExpressionException()
    {
        typeof(InvalidFilterException).Should().BeDerivedFrom<InvalidExpressionException>();
    }

    [Fact]
    public void InvalidUpdateException_DeriveFromInvalidExpressionException()
    {
        typeof(InvalidUpdateException).Should().BeDerivedFrom<InvalidExpressionException>();
    }

    [Fact]
    public void InvalidKeyConditionException_DeriveFromInvalidExpressionException()
    {
        typeof(InvalidKeyConditionException).Should().BeDerivedFrom<InvalidExpressionException>();
    }

    #endregion

    #region Structured Properties - UnsupportedExpressionException

    [Fact]
    public void UnsupportedExpressionException_CarriesNodeTypeAndText()
    {
        // Arrange
        var nodeType = ExpressionType.Call;
        var expressionText = "obj.ToString()";

        // Act
        var exception = new UnsupportedExpressionException(nodeType, expressionText);

        // Assert
        exception.NodeType.Should().Be(nodeType);
        exception.ExpressionText.Should().Be(expressionText);
    }

    [Fact]
    public void UnsupportedExpressionException_MessageContainsNodeTypeAndText()
    {
        // Arrange
        var nodeType = ExpressionType.Call;
        var expressionText = "obj.ToString()";

        // Act
        var exception = new UnsupportedExpressionException(nodeType, expressionText);

        // Assert
        exception.Message.Should().Contain(nodeType.ToString());
        exception.Message.Should().Contain(expressionText);
        exception.Message.Should().Be($"Expression node type '{nodeType}' is not supported: {expressionText}");
    }

    #endregion

    #region Structured Properties - MissingConverterException

    [Fact]
    public void MissingConverterException_CarriesTargetTypeAndPropertyName()
    {
        // Arrange
        var targetType = typeof(CustomType);
        var propertyName = "CustomProperty";

        // Act
        var exception = new MissingConverterException(targetType, propertyName);

        // Assert
        exception.TargetType.Should().Be(targetType);
        exception.PropertyName.Should().Be(propertyName);
    }

    [Fact]
    public void MissingConverterException_NullPropertyName_WhenDirectRegistryCall()
    {
        // Arrange
        var targetType = typeof(CustomType);

        // Act
        var exception = new MissingConverterException(targetType);

        // Assert
        exception.TargetType.Should().Be(targetType);
        exception.PropertyName.Should().BeNull();
    }

    [Fact]
    public void MissingConverterException_MessageContainsTypeName()
    {
        // Arrange
        var targetType = typeof(CustomType);

        // Act
        var exception = new MissingConverterException(targetType);

        // Assert
        exception.Message.Should().Contain(targetType.ToString());
        exception.Message.Should().Be($"No converter registered for type '{targetType}'.");
    }

    [Fact]
    public void MissingConverterException_MessageContainsTypeNameAndProperty_WhenPropertyProvided()
    {
        // Arrange
        var targetType = typeof(CustomType);
        var propertyName = "CustomProperty";

        // Act
        var exception = new MissingConverterException(targetType, propertyName);

        // Assert
        exception.Message.Should().Contain(targetType.ToString());
        exception.Message.Should().Contain(propertyName);
        exception.Message.Should().Be($"No converter registered for type '{targetType}' (property '{propertyName}').");
    }

    #endregion

    #region Structured Properties - ExpressionAttributeConflictException

    [Fact]
    public void ExpressionAttributeConflictException_CarriesAliasKeyAndValues()
    {
        // Arrange
        var aliasKey = "#filt_0";
        var existingValue = "Name";
        var conflictingValue = "Title";

        // Act
        var exception = new ExpressionAttributeConflictException(aliasKey, existingValue, conflictingValue);

        // Assert
        exception.AliasKey.Should().Be(aliasKey);
        exception.ExistingValue.Should().Be(existingValue);
        exception.ConflictingValue.Should().Be(conflictingValue);
    }

    [Fact]
    public void ExpressionAttributeConflictException_NullConflictingValue_WhenValuePlaceholder()
    {
        // Arrange
        var aliasKey = ":filt_v0";
        var existingValue = "S: 'test'";

        // Act
        var exception = new ExpressionAttributeConflictException(aliasKey, existingValue);

        // Assert
        exception.AliasKey.Should().Be(aliasKey);
        exception.ExistingValue.Should().Be(existingValue);
        exception.ConflictingValue.Should().BeNull();
    }

    [Fact]
    public void ExpressionAttributeConflictException_MessageContainsAllValues_WhenConflictingProvided()
    {
        // Arrange
        var aliasKey = "#filt_0";
        var existingValue = "Name";
        var conflictingValue = "Title";

        // Act
        var exception = new ExpressionAttributeConflictException(aliasKey, existingValue, conflictingValue);

        // Assert
        exception.Message.Should().Contain(aliasKey);
        exception.Message.Should().Contain(existingValue);
        exception.Message.Should().Contain(conflictingValue);
        exception.Message.Should().Be($"Attribute alias '{aliasKey}' conflicts: existing '{existingValue}', incoming '{conflictingValue}'.");
    }

    [Fact]
    public void ExpressionAttributeConflictException_MessageExcludesConflictingValue_WhenNull()
    {
        // Arrange
        var aliasKey = ":filt_v0";
        var existingValue = "S: 'test'";

        // Act
        var exception = new ExpressionAttributeConflictException(aliasKey, existingValue);

        // Assert
        exception.Message.Should().Contain(aliasKey);
        exception.Message.Should().Contain(existingValue);
        exception.Message.Should().Be($"Attribute alias '{aliasKey}' already exists with value '{existingValue}'.");
    }

    #endregion

    #region Structured Properties - InvalidProjectionException

    [Fact]
    public void InvalidProjectionException_CarriesPropertyNameAndEntityType()
    {
        // Arrange
        var propertyName = "IgnoredProperty";
        var entityType = typeof(TestEntity);

        // Act
        var exception = new InvalidProjectionException(propertyName, entityType);

        // Assert
        exception.PropertyName.Should().Be(propertyName);
        exception.EntityType.Should().Be(entityType);
        exception.AttributeName.Should().BeNull();
    }

    [Fact]
    public void InvalidProjectionException_MessageContainsPropertyAndEntityName()
    {
        // Arrange
        var propertyName = "IgnoredProperty";
        var entityType = typeof(TestEntity);

        // Act
        var exception = new InvalidProjectionException(propertyName, entityType);

        // Assert
        exception.Message.Should().Contain(propertyName);
        exception.Message.Should().Contain(entityType.Name);
        exception.Message.Should().Be(
            $"Cannot project property '{propertyName}' on '{entityType.Name}': " +
            "property is marked [DynamoDbIgnore] or is not a stored attribute.");
    }

    #endregion

    #region Structured Properties - InvalidFilterException

    [Fact]
    public void InvalidFilterException_CarriesPropertyNameAndEntityType()
    {
        // Arrange
        var message = "Cannot filter on ignored property.";
        var propertyName = "IgnoredProperty";
        var entityType = typeof(TestEntity);

        // Act
        var exception = new InvalidFilterException(message, propertyName, entityType);

        // Assert
        exception.PropertyName.Should().Be(propertyName);
        exception.EntityType.Should().Be(entityType);
        exception.AttributeName.Should().BeNull();
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void InvalidFilterException_SupportsNullProperties()
    {
        // Arrange
        var message = "Filter expression is not boolean.";

        // Act
        var exception = new InvalidFilterException(message);

        // Assert
        exception.PropertyName.Should().BeNull();
        exception.EntityType.Should().BeNull();
        exception.AttributeName.Should().BeNull();
        exception.Message.Should().Be(message);
    }

    #endregion

    #region Structured Properties - InvalidUpdateException

    [Fact]
    public void InvalidUpdateException_CarriesPropertyNameAndEntityType()
    {
        // Arrange
        var message = "Cannot update ignored property.";
        var propertyName = "IgnoredProperty";
        var entityType = typeof(TestEntity);

        // Act
        var exception = new InvalidUpdateException(message, propertyName, entityType);

        // Assert
        exception.PropertyName.Should().Be(propertyName);
        exception.EntityType.Should().Be(entityType);
        exception.AttributeName.Should().BeNull();
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void InvalidUpdateException_SupportsNullProperties()
    {
        // Arrange
        var message = "Conflicting update operations.";

        // Act
        var exception = new InvalidUpdateException(message);

        // Assert
        exception.PropertyName.Should().BeNull();
        exception.EntityType.Should().BeNull();
        exception.AttributeName.Should().BeNull();
        exception.Message.Should().Be(message);
    }

    #endregion

    #region Structured Properties - InvalidKeyConditionException

    [Fact]
    public void InvalidKeyConditionException_CarriesPropertyNameAndEntityType()
    {
        // Arrange
        var message = "Key condition references nested property.";
        var propertyName = "Address.City";
        var entityType = typeof(TestEntity);

        // Act
        var exception = new InvalidKeyConditionException(message, propertyName, entityType);

        // Assert
        exception.PropertyName.Should().Be(propertyName);
        exception.EntityType.Should().Be(entityType);
        exception.AttributeName.Should().BeNull();
        exception.Message.Should().Be(message);
    }

    [Fact]
    public void InvalidKeyConditionException_SupportsNullProperties()
    {
        // Arrange
        var message = "Invalid key condition operation.";

        // Act
        var exception = new InvalidKeyConditionException(message);

        // Assert
        exception.PropertyName.Should().BeNull();
        exception.EntityType.Should().BeNull();
        exception.AttributeName.Should().BeNull();
        exception.Message.Should().Be(message);
    }

    #endregion

    #region Inner Exception Support

    [Fact]
    public void ExpressionMappingException_PreservesInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");
        var message = "Cannot filter on ignored property.";

        // Act
        var exception = new InvalidFilterException(message);
        var exceptionWithInner = new TestExpressionMappingException(message, innerException);

        // Assert
        exceptionWithInner.InnerException.Should().Be(innerException);
    }

    #endregion

    #region Catch Patterns

    [Fact]
    public void CatchExpressionMappingException_CatchesAllLibraryExceptions()
    {
        // Arrange
        var exceptions = new Exception[]
        {
            new UnsupportedExpressionException(ExpressionType.Call, "test"),
            new MissingConverterException(typeof(string)),
            new ExpressionAttributeConflictException("key", "value"),
            new InvalidProjectionException("prop", typeof(TestEntity)),
            new InvalidFilterException("message"),
            new InvalidUpdateException("message"),
            new InvalidKeyConditionException("message")
        };

        // Act & Assert
        foreach (var ex in exceptions)
        {
            ex.Should().BeAssignableTo<ExpressionMappingException>(
                $"{ex.GetType().Name} should be catchable as ExpressionMappingException");
        }
    }

    [Fact]
    public void CatchInvalidExpressionException_CatchesAllBuilderValidationErrors()
    {
        // Arrange
        var builderExceptions = new Exception[]
        {
            new InvalidProjectionException("prop", typeof(TestEntity)),
            new InvalidFilterException("message"),
            new InvalidUpdateException("message"),
            new InvalidKeyConditionException("message")
        };

        // Act & Assert
        foreach (var ex in builderExceptions)
        {
            ex.Should().BeAssignableTo<InvalidExpressionException>(
                $"{ex.GetType().Name} should be catchable as InvalidExpressionException");
        }
    }

    [Fact]
    public void CatchInvalidExpressionException_DoesNotCatchUnsupportedOrMissing()
    {
        // Arrange
        var nonBuilderExceptions = new Exception[]
        {
            new UnsupportedExpressionException(ExpressionType.Call, "test"),
            new MissingConverterException(typeof(string)),
            new ExpressionAttributeConflictException("key", "value")
        };

        // Act & Assert
        foreach (var ex in nonBuilderExceptions)
        {
            ex.Should().NotBeAssignableTo<InvalidExpressionException>(
                $"{ex.GetType().Name} should NOT be catchable as InvalidExpressionException");
        }
    }

    #endregion

    #region Test Helper Types

    private class CustomType { }

    private class TestEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    // Concrete test implementation of abstract ExpressionMappingException
    private class TestExpressionMappingException : ExpressionMappingException
    {
        public TestExpressionMappingException(string message) : base(message) { }
        public TestExpressionMappingException(string message, Exception innerException) : base(message, innerException) { }
    }

    #endregion
}
