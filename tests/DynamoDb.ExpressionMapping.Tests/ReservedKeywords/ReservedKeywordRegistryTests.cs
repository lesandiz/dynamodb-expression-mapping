using DynamoDb.ExpressionMapping.ReservedKeywords;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.ReservedKeywords;

public class ReservedKeywordRegistryTests
{
    private readonly ReservedKeywordRegistry registry = ReservedKeywordRegistry.Default;

    [Theory]
    [InlineData("STATUS")]
    [InlineData("NAME")]
    [InlineData("DATE")]
    [InlineData("DATA")]
    [InlineData("COMMENT")]
    [InlineData("HIDDEN")]
    [InlineData("LIST")]
    [InlineData("MAP")]
    [InlineData("NUMBER")]
    [InlineData("TABLE")]
    [InlineData("USER")]
    [InlineData("VALUE")]
    [InlineData("ZONE")]
    public void IsReserved_ShouldReturnTrue_ForOfficialReservedWords(string keyword)
    {
        // Act
        var result = registry.IsReserved(keyword);

        // Assert
        result.Should().BeTrue($"'{keyword}' is an official DynamoDB reserved word");
    }

    [Theory]
    [InlineData("status")]  // lowercase
    [InlineData("Status")]  // mixed case
    [InlineData("StAtUs")]  // mixed case
    [InlineData("name")]
    [InlineData("Name")]
    [InlineData("dAtE")]
    public void IsReserved_ShouldBeCaseInsensitive(string keyword)
    {
        // Act
        var result = registry.IsReserved(keyword);

        // Assert
        result.Should().BeTrue($"reserved word check should be case-insensitive for '{keyword}'");
    }

    [Theory]
    [InlineData("OrderId")]
    [InlineData("CustomerId")]
    [InlineData("TotalAmount")]
    [InlineData("CreatedAt")]
    [InlineData("IsActive")]
    [InlineData("customer_id")]
    [InlineData("order_total")]
    public void IsReserved_ShouldReturnFalse_ForNonReservedWords(string name)
    {
        // Act
        var result = registry.IsReserved(name);

        // Assert
        result.Should().BeFalse($"'{name}' is not a reserved word");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsReserved_ShouldReturnFalse_ForNullOrEmpty(string? name)
    {
        // Act
        var result = registry.IsReserved(name!);

        // Assert
        result.Should().BeFalse("null or empty strings are not reserved words");
    }

    [Theory]
    [InlineData("STATUS")]  // reserved word
    [InlineData("Name")]    // reserved word (case insensitive)
    [InlineData("customer-id")]  // contains hyphen
    [InlineData("order.total")]  // contains dot
    [InlineData("user@email")]   // contains @
    [InlineData("price$")]       // contains $
    [InlineData("items[0]")]     // contains brackets
    public void NeedsEscaping_ShouldReturnTrue_ForReservedWordsOrSpecialCharacters(string name)
    {
        // Act
        var result = registry.NeedsEscaping(name);

        // Assert
        result.Should().BeTrue($"'{name}' needs escaping");
    }

    [Theory]
    [InlineData("OrderId")]
    [InlineData("CustomerId")]
    [InlineData("TotalAmount")]
    [InlineData("customer_id")]  // underscore is allowed
    [InlineData("order_total")]
    [InlineData("_privateField")]
    [InlineData("field123")]
    [InlineData("ABC123xyz")]
    public void NeedsEscaping_ShouldReturnFalse_ForNonReservedWordsWithoutSpecialCharacters(string name)
    {
        // Act
        var result = registry.NeedsEscaping(name);

        // Assert
        result.Should().BeFalse($"'{name}' does not need escaping");
    }

    [Fact]
    public void Default_ShouldBeSingleton()
    {
        // Act
        var instance1 = ReservedKeywordRegistry.Default;
        var instance2 = ReservedKeywordRegistry.Default;

        // Assert
        instance1.Should().BeSameAs(instance2, "Default should return the same singleton instance");
    }

    [Fact]
    public void Registry_ShouldContain_CommonDynamoDbReservedWords()
    {
        // Arrange - sample of commonly used reserved words that often overlap with attribute names
        var commonReservedWords = new[]
        {
            "ABORT", "ADD", "ALL", "AND", "AS", "BETWEEN", "BY",
            "COMMENT", "DATA", "DATE", "DELETE", "DOMAIN", "ENABLE",
            "HIDDEN", "IN", "INSERT", "IS", "LIKE", "LIST", "MAP",
            "NAME", "NOT", "NULL", "NUMBER", "OF", "ONLINE", "OR",
            "OWNER", "PATH", "RANK", "REGION", "SELECT", "SET", "SIZE",
            "SOURCE", "STATE", "STATUS", "STRING", "TABLE", "TEXT", "TIME",
            "TOKEN", "TYPE", "UPDATE", "URL", "USER", "VALUE", "VALUES",
            "VIEW", "WHERE", "WITH", "ZONE"
        };

        // Act & Assert
        foreach (var word in commonReservedWords)
        {
            registry.IsReserved(word).Should().BeTrue($"'{word}' should be in the reserved words list");
        }
    }

    [Theory]
    [InlineData("customer-name", '-')]
    [InlineData("order.total", '.')]
    [InlineData("price$", '$')]
    [InlineData("user@email", '@')]
    [InlineData("items[0]", '[')]
    [InlineData("data]", ']')]
    [InlineData("func()", '(')]
    [InlineData("expr)", ')')]
    [InlineData("a+b", '+')]
    [InlineData("x=y", '=')]
    [InlineData("a b", ' ')]
    public void NeedsEscaping_ShouldDetect_SpecificSpecialCharacters(string name, char specialChar)
    {
        // Act
        var result = registry.NeedsEscaping(name);

        // Assert
        result.Should().BeTrue($"'{name}' contains special character '{specialChar}' and needs escaping");
    }

    [Fact]
    public void NeedsEscaping_ShouldAllowUnderscore()
    {
        // Arrange
        var namesWithUnderscores = new[]
        {
            "customer_id",
            "_privateField",
            "field_name_with_many_underscores",
            "___multiple",
            "end_"
        };

        // Act & Assert
        foreach (var name in namesWithUnderscores)
        {
            registry.NeedsEscaping(name).Should().BeFalse(
                $"'{name}' contains only valid characters (underscore is allowed in DynamoDB)");
        }
    }
}
