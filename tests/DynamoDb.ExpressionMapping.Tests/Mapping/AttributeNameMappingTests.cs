using DynamoDb.ExpressionMapping.Attributes;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Mapping;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Mapping;

/// <summary>
/// Comprehensive test suite for Spec 01: Attribute Name Mapping.
/// Covers attributes, resolvers, builders, and factories.
/// </summary>
public class AttributeNameMappingTests
{
    #region Attribute Tests

    public class DynamoDbAttributeAttributeTests
    {
        [Fact]
        public void Constructor_ValidName_StoresAttributeName()
        {
            var attr = new DynamoDbAttributeAttribute("customer_id");

            attr.AttributeName.Should().Be("customer_id");
        }

        [Fact]
        public void Constructor_NullName_ThrowsArgumentException()
        {
            var act = () => new DynamoDbAttributeAttribute(null!);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Constructor_EmptyName_ThrowsArgumentException()
        {
            var act = () => new DynamoDbAttributeAttribute("");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Constructor_WhitespaceName_DoesNotThrow()
        {
            // ArgumentException.ThrowIfNullOrEmpty doesn't check whitespace
            // This is by design - only null or empty strings are rejected
            var attr = new DynamoDbAttributeAttribute("   ");

            attr.AttributeName.Should().Be("   ");
        }
    }

    public class DynamoDbIgnoreAttributeTests
    {
        [Fact]
        public void Attribute_CanBeAppliedToProperty()
        {
            var propertyInfo = typeof(TestEntityWithIgnore).GetProperty(nameof(TestEntityWithIgnore.ComputedValue));
            var attr = propertyInfo!.GetCustomAttributes(typeof(DynamoDbIgnoreAttribute), false).FirstOrDefault();

            attr.Should().NotBeNull();
            attr.Should().BeOfType<DynamoDbIgnoreAttribute>();
        }
    }

    public class DynamoDbConverterAttributeTests
    {
        [Fact]
        public void Constructor_ValidType_StoresConverterType()
        {
            var attr = new DynamoDbConverterAttribute(typeof(string));

            attr.ConverterType.Should().Be(typeof(string));
        }

        [Fact]
        public void Constructor_NullType_ThrowsArgumentNullException()
        {
            var act = () => new DynamoDbConverterAttribute(null!);

            act.Should().Throw<ArgumentNullException>();
        }
    }

    #endregion

    #region AttributeNameResolver Tests

    public class AttributeNameResolverTests
    {
        [Fact]
        public void GetAttributeName_PropertyWithNoAnnotation_ReturnsPropertyNameAsIs()
        {
            var resolver = new AttributeNameResolver<TestEntityPlain>();

            var result = resolver.GetAttributeName("Name");

            result.Should().Be("Name");
        }

        [Fact]
        public void GetAttributeName_DynamoDbAttribute_ReturnsRemappedName()
        {
            var resolver = new AttributeNameResolver<TestEntityWithAttributes>();

            var result = resolver.GetAttributeName("CustomerId");

            result.Should().Be("customer_id");
        }

        [Fact]
        public void GetAttributeName_DynamoDbIgnore_StrictMode_ThrowsInvalidProjectionException()
        {
            var resolver = new AttributeNameResolver<TestEntityWithIgnore>(NameResolutionMode.Strict);

            var act = () => resolver.GetAttributeName("ComputedValue");

            act.Should().Throw<InvalidProjectionException>()
                .Which.PropertyName.Should().Be("ComputedValue");
        }

        [Fact]
        public void GetAttributeName_DynamoDbIgnore_StrictMode_ExceptionContainsEntityType()
        {
            var resolver = new AttributeNameResolver<TestEntityWithIgnore>(NameResolutionMode.Strict);

            var act = () => resolver.GetAttributeName("ComputedValue");

            act.Should().Throw<InvalidProjectionException>()
                .Which.EntityType.Should().Be(typeof(TestEntityWithIgnore));
        }

        [Fact]
        public void GetAttributeName_DynamoDbIgnore_LenientMode_ReturnsPropertyName()
        {
            var resolver = new AttributeNameResolver<TestEntityWithIgnore>(NameResolutionMode.Lenient);

            var result = resolver.GetAttributeName("ComputedValue");

            result.Should().Be("ComputedValue");
        }

        [Fact]
        public void IsStoredAttribute_StoredProperty_ReturnsTrue()
        {
            var resolver = new AttributeNameResolver<TestEntityWithIgnore>();

            var result = resolver.IsStoredAttribute("Name");

            result.Should().BeTrue();
        }

        [Fact]
        public void IsStoredAttribute_IgnoredProperty_ReturnsFalse()
        {
            var resolver = new AttributeNameResolver<TestEntityWithIgnore>();

            var result = resolver.IsStoredAttribute("ComputedValue");

            result.Should().BeFalse();
        }

        [Fact]
        public void GetPropertyName_ReturnsReverseMapping()
        {
            var resolver = new AttributeNameResolver<TestEntityPlain>();

            var result = resolver.GetPropertyName("Name");

            result.Should().Be("Name");
        }

        [Fact]
        public void GetPropertyName_RemappedAttribute_ReturnsOriginalPropertyName()
        {
            var resolver = new AttributeNameResolver<TestEntityWithAttributes>();

            var result = resolver.GetPropertyName("customer_id");

            result.Should().Be("CustomerId");
        }

        [Fact]
        public void GetPropertyName_UnmappedAttribute_ReturnsAttributeNameAsIs()
        {
            var resolver = new AttributeNameResolver<TestEntityWithAttributes>();

            var result = resolver.GetPropertyName("unknown_attribute");

            result.Should().Be("unknown_attribute");
        }

        [Fact]
        public void AwsSdkDynamoDBProperty_ReturnsRemappedName()
        {
            var resolver = new AttributeNameResolver<AwsSdkAnnotatedEntity>();

            var result = resolver.GetAttributeName("DisplayName");

            result.Should().Be("display_name");
        }

        [Fact]
        public void AwsSdkDynamoDBIgnore_IsStoredAttribute_ReturnsFalse()
        {
            var resolver = new AttributeNameResolver<AwsSdkAnnotatedEntity>();

            var result = resolver.IsStoredAttribute("Computed");

            result.Should().BeFalse();
        }

        [Fact]
        public void ResolutionOrder_DynamoDbAttribute_TakesPrecedenceOver_DynamoDBProperty()
        {
            var resolver = new AttributeNameResolver<DualAnnotatedEntity>();

            var result = resolver.GetAttributeName("Name");

            result.Should().Be("lib_name");
        }

        [Fact]
        public void ResolutionOrder_DynamoDBProperty_TakesPrecedenceOver_ConventionName()
        {
            var resolver = new AttributeNameResolver<AwsSdkAnnotatedEntity>();

            var result = resolver.GetAttributeName("DisplayName");

            result.Should().Be("display_name");
        }

        [Fact]
        public void MultipleProperties_WithDifferentAnnotations_ResolveCorrectly()
        {
            var resolver = new AttributeNameResolver<TestEntityWithAttributes>();

            resolver.GetAttributeName("CustomerId").Should().Be("customer_id");
            resolver.GetAttributeName("Name").Should().Be("Name");
            resolver.IsStoredAttribute("CustomerId").Should().BeTrue();
            resolver.IsStoredAttribute("Name").Should().BeTrue();
        }
    }

    #endregion

    #region AttributeNameResolverBuilder Tests

    public class AttributeNameResolverBuilderTests
    {
        [Fact]
        public void Map_ValidProperty_OverridesPropertyName()
        {
            var resolver = new AttributeNameResolverBuilder<TestEntityPlain>()
                .Map(e => e.Id, "entity_id")
                .Build();

            var result = resolver.GetAttributeName("Id");

            result.Should().Be("entity_id");
        }

        [Fact]
        public void Map_MultipleProperties_AllMapped()
        {
            var resolver = new AttributeNameResolverBuilder<TestEntityPlain>()
                .Map(e => e.Id, "entity_id")
                .Map(e => e.Name, "entity_name")
                .Build();

            resolver.GetAttributeName("Id").Should().Be("entity_id");
            resolver.GetAttributeName("Name").Should().Be("entity_name");
        }

        [Fact]
        public void Map_NullSelector_ThrowsArgumentNullException()
        {
            var builder = new AttributeNameResolverBuilder<TestEntityPlain>();

            var act = () => builder.Map<string>(null!, "test");

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Map_NullAttributeName_ThrowsArgumentException()
        {
            var builder = new AttributeNameResolverBuilder<TestEntityPlain>();

            var act = () => builder.Map(e => e.Name, null!);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Map_EmptyAttributeName_ThrowsArgumentException()
        {
            var builder = new AttributeNameResolverBuilder<TestEntityPlain>();

            var act = () => builder.Map(e => e.Name, "");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Map_WhitespaceAttributeName_ThrowsArgumentException()
        {
            var builder = new AttributeNameResolverBuilder<TestEntityPlain>();

            var act = () => builder.Map(e => e.Name, "   ");

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Ignore_ValidProperty_MarksPropertyAsNotStored()
        {
            var resolver = new AttributeNameResolverBuilder<TestEntityPlain>()
                .Ignore(e => e.Name)
                .Build();

            resolver.IsStoredAttribute("Name").Should().BeFalse();
        }

        [Fact]
        public void Ignore_MultipleProperties_AllIgnored()
        {
            var resolver = new AttributeNameResolverBuilder<TestEntityPlain>()
                .Ignore(e => e.Name)
                .Ignore(e => e.Count)
                .Build();

            resolver.IsStoredAttribute("Name").Should().BeFalse();
            resolver.IsStoredAttribute("Count").Should().BeFalse();
        }

        [Fact]
        public void Ignore_NullSelector_ThrowsArgumentNullException()
        {
            var builder = new AttributeNameResolverBuilder<TestEntityPlain>();

            var act = () => builder.Ignore<string>(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Ignore_StrictMode_GetAttributeName_ThrowsInvalidProjectionException()
        {
            var resolver = new AttributeNameResolverBuilder<TestEntityPlain>()
                .WithMode(NameResolutionMode.Strict)
                .Ignore(e => e.Name)
                .Build();

            var act = () => resolver.GetAttributeName("Name");

            act.Should().Throw<InvalidProjectionException>();
        }

        [Fact]
        public void FluentOverride_TakesPrecedenceOver_DynamoDbAttribute()
        {
            var resolver = new AttributeNameResolverBuilder<TestEntityWithAttributes>()
                .Map(e => e.CustomerId, "override_id")
                .Build();

            var result = resolver.GetAttributeName("CustomerId");

            result.Should().Be("override_id");
        }

        [Fact]
        public void FluentIgnore_TakesPrecedenceOver_DynamoDbAttribute()
        {
            var resolver = new AttributeNameResolverBuilder<TestEntityWithAttributes>()
                .Ignore(e => e.CustomerId)
                .Build();

            resolver.IsStoredAttribute("CustomerId").Should().BeFalse();
        }

        [Fact]
        public void Map_AfterIgnore_RemovesIgnoreFlag()
        {
            var resolver = new AttributeNameResolverBuilder<TestEntityPlain>()
                .Ignore(e => e.Name)
                .Map(e => e.Name, "entity_name")
                .Build();

            resolver.IsStoredAttribute("Name").Should().BeTrue();
            resolver.GetAttributeName("Name").Should().Be("entity_name");
        }

        [Fact]
        public void Ignore_AfterMap_RemovesMapping()
        {
            var resolver = new AttributeNameResolverBuilder<TestEntityPlain>()
                .Map(e => e.Name, "entity_name")
                .Ignore(e => e.Name)
                .Build();

            resolver.IsStoredAttribute("Name").Should().BeFalse();
        }

        [Fact]
        public void WithMode_SetsResolutionMode()
        {
            var resolver = new AttributeNameResolverBuilder<TestEntityPlain>()
                .WithMode(NameResolutionMode.Lenient)
                .Ignore(e => e.Name)
                .Build();

            // In lenient mode, GetAttributeName on ignored property should not throw
            var result = resolver.GetAttributeName("Name");

            result.Should().Be("Name");
        }

        [Fact]
        public void Build_ProducesWorkingResolver()
        {
            var resolver = new AttributeNameResolverBuilder<TestEntityPlain>()
                .Map(e => e.Id, "entity_id")
                .Build();

            resolver.Should().NotBeNull();
            resolver.Should().BeAssignableTo<IAttributeNameResolver<TestEntityPlain>>();
            resolver.GetAttributeName("Id").Should().Be("entity_id");
        }

        [Fact]
        public void GetPropertyName_ReverseMapping_WorksWithFluentOverrides()
        {
            var resolver = new AttributeNameResolverBuilder<TestEntityPlain>()
                .Map(e => e.Id, "entity_id")
                .Build();

            var result = resolver.GetPropertyName("entity_id");

            result.Should().Be("Id");
        }
    }

    #endregion

    #region AttributeNameResolverFactory Tests

    public class AttributeNameResolverFactoryTests
    {
        [Fact]
        public void GetResolver_CreatesResolverForArbitraryType()
        {
            var factory = new AttributeNameResolverFactory();

            var resolver = factory.GetResolver(typeof(TestEntityPlain));

            resolver.Should().NotBeNull();
            resolver.Should().BeAssignableTo<IAttributeNameResolver>();
        }

        [Fact]
        public void GetResolver_SameType_ReturnsSameInstance()
        {
            var factory = new AttributeNameResolverFactory();

            var resolver1 = factory.GetResolver(typeof(TestEntityPlain));
            var resolver2 = factory.GetResolver(typeof(TestEntityPlain));

            resolver1.Should().BeSameAs(resolver2);
        }

        [Fact]
        public void GetResolver_DifferentTypes_ReturnsDifferentInstances()
        {
            var factory = new AttributeNameResolverFactory();

            var resolver1 = factory.GetResolver(typeof(TestEntityPlain));
            var resolver2 = factory.GetResolver(typeof(TestEntityWithAttributes));

            resolver1.Should().NotBeSameAs(resolver2);
        }

        [Fact]
        public void GetResolverGeneric_ReturnsTypedResolver()
        {
            var factory = new AttributeNameResolverFactory();

            var resolver = factory.GetResolver<TestEntityPlain>();

            resolver.Should().NotBeNull();
            resolver.Should().BeAssignableTo<IAttributeNameResolver<TestEntityPlain>>();
        }

        [Fact]
        public void GetResolverGeneric_SameType_ReturnsSameInstance()
        {
            var factory = new AttributeNameResolverFactory();

            var resolver1 = factory.GetResolver<TestEntityPlain>();
            var resolver2 = factory.GetResolver<TestEntityPlain>();

            resolver1.Should().BeSameAs(resolver2);
        }

        [Fact]
        public void GetResolverGeneric_DelegatesToNonGenericMethod()
        {
            var factory = new AttributeNameResolverFactory();

            var resolver1 = factory.GetResolver<TestEntityPlain>();
            var resolver2 = factory.GetResolver(typeof(TestEntityPlain));

            resolver1.Should().BeSameAs(resolver2);
        }

        [Fact]
        public void AutoDiscovery_NoRegistrationNeeded_ForAnnotatedTypes()
        {
            var factory = new AttributeNameResolverFactory();

            var resolver = factory.GetResolver<TestEntityWithAttributes>();

            resolver.GetAttributeName("CustomerId").Should().Be("customer_id");
        }

        [Fact]
        public void Factory_WithStrictMode_AppliesModeToResolvers()
        {
            var factory = new AttributeNameResolverFactory(NameResolutionMode.Strict);

            var resolver = factory.GetResolver<TestEntityWithIgnore>();

            var act = () => resolver.GetAttributeName("ComputedValue");
            act.Should().Throw<InvalidProjectionException>();
        }

        [Fact]
        public void Factory_WithLenientMode_AppliesModeToResolvers()
        {
            var factory = new AttributeNameResolverFactory(NameResolutionMode.Lenient);

            var resolver = factory.GetResolver<TestEntityWithIgnore>();

            var result = resolver.GetAttributeName("ComputedValue");
            result.Should().Be("ComputedValue");
        }

        [Fact]
        public void Register_OverridesAutoDiscoveredResolver()
        {
            var factory = new AttributeNameResolverFactory();
            var customResolver = new AttributeNameResolverBuilder<TestEntityPlain>()
                .Map(e => e.Id, "custom_id")
                .Build();

            // Use reflection to call internal Register method
            var registerMethod = typeof(AttributeNameResolverFactory).GetMethod(
                "Register",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            registerMethod!.Invoke(factory, new object[] { typeof(TestEntityPlain), customResolver });

            var resolver = factory.GetResolver<TestEntityPlain>();

            resolver.GetAttributeName("Id").Should().Be("custom_id");
        }

        [Fact]
        public void NestedPath_ResolvesEachSegmentAgainstCorrectType()
        {
            var factory = new AttributeNameResolverFactory();

            var orderResolver = factory.GetResolver<TestOrder>();
            var addressResolver = factory.GetResolver<TestAddress>();

            orderResolver.GetAttributeName("ShippingAddress").Should().Be("ShippingAddress");
            addressResolver.GetAttributeName("City").Should().Be("City");
        }

        [Fact]
        public void NestedPath_RemappedChildType_ResolvesCorrectly()
        {
            var factory = new AttributeNameResolverFactory();
            var addressResolverBuilder = new AttributeNameResolverBuilder<TestAddress>()
                .Map(a => a.City, "city_name")
                .Build();

            // Use reflection to call internal Register method
            var registerMethod = typeof(AttributeNameResolverFactory).GetMethod(
                "Register",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            registerMethod!.Invoke(factory, new object[] { typeof(TestAddress), addressResolverBuilder });

            var orderResolver = factory.GetResolver<TestOrder>();
            var addressResolver = factory.GetResolver<TestAddress>();

            orderResolver.GetAttributeName("ShippingAddress").Should().Be("ShippingAddress");
            addressResolver.GetAttributeName("City").Should().Be("city_name");
        }
    }

    #endregion

    #region AttributeNameResolverFactoryBuilder Tests

    public class AttributeNameResolverFactoryBuilderTests
    {
        [Fact]
        public void Configure_RegistersPerTypeConfiguration()
        {
            var factory = new AttributeNameResolverFactoryBuilder()
                .Configure<TestEntityPlain>(b => b.Map(e => e.Id, "entity_id"))
                .Build();

            var resolver = factory.GetResolver<TestEntityPlain>();

            resolver.GetAttributeName("Id").Should().Be("entity_id");
        }

        [Fact]
        public void Configure_MultipleTypes_EachConfiguredIndependently()
        {
            var factory = new AttributeNameResolverFactoryBuilder()
                .Configure<TestEntityPlain>(b => b.Map(e => e.Id, "entity_id"))
                .Configure<TestOrder>(b => b.Map(o => o.OrderId, "order_id"))
                .Build();

            var plainResolver = factory.GetResolver<TestEntityPlain>();
            var orderResolver = factory.GetResolver<TestOrder>();

            plainResolver.GetAttributeName("Id").Should().Be("entity_id");
            orderResolver.GetAttributeName("OrderId").Should().Be("order_id");
        }

        [Fact]
        public void Configure_NullAction_ThrowsArgumentNullException()
        {
            var builder = new AttributeNameResolverFactoryBuilder();

            var act = () => builder.Configure<TestEntityPlain>(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WithMode_AppliesModeToAllResolvers()
        {
            var factory = new AttributeNameResolverFactoryBuilder()
                .WithMode(NameResolutionMode.Lenient)
                .Configure<TestEntityPlain>(b => b.Ignore(e => e.Name))
                .Build();

            var resolver = factory.GetResolver<TestEntityPlain>();

            // In lenient mode, should not throw
            var result = resolver.GetAttributeName("Name");
            result.Should().Be("Name");
        }

        [Fact]
        public void WithMode_AppliesTo_AutoDiscoveredTypes()
        {
            var factory = new AttributeNameResolverFactoryBuilder()
                .WithMode(NameResolutionMode.Lenient)
                .Build();

            var resolver = factory.GetResolver<TestEntityWithIgnore>();

            // In lenient mode, should not throw
            var result = resolver.GetAttributeName("ComputedValue");
            result.Should().Be("ComputedValue");
        }

        [Fact]
        public void Build_CreatesFactoryWithRegisteredResolvers()
        {
            var factory = new AttributeNameResolverFactoryBuilder()
                .Configure<TestEntityPlain>(b => b.Map(e => e.Id, "entity_id"))
                .Build();

            factory.Should().NotBeNull();
            factory.Should().BeAssignableTo<IAttributeNameResolverFactory>();
        }

        [Fact]
        public void UnconfiguredType_FallsBackToAutoDiscovery()
        {
            var factory = new AttributeNameResolverFactoryBuilder()
                .Configure<TestEntityPlain>(b => b.Map(e => e.Id, "entity_id"))
                .Build();

            var unconfiguredResolver = factory.GetResolver<TestEntityWithAttributes>();

            // Should use attribute-based resolution
            unconfiguredResolver.GetAttributeName("CustomerId").Should().Be("customer_id");
        }

        [Fact]
        public void FactoryBuilder_SupportsFluentChaining()
        {
            var factory = new AttributeNameResolverFactoryBuilder()
                .WithMode(NameResolutionMode.Strict)
                .Configure<TestEntityPlain>(b => b.Map(e => e.Id, "id"))
                .Configure<TestOrder>(b => b.Map(o => o.OrderId, "order_id"))
                .Build();

            factory.GetResolver<TestEntityPlain>().GetAttributeName("Id").Should().Be("id");
            factory.GetResolver<TestOrder>().GetAttributeName("OrderId").Should().Be("order_id");
        }

        [Fact]
        public void Configure_WithComplexMapping_AllMappingsApplied()
        {
            var factory = new AttributeNameResolverFactoryBuilder()
                .Configure<TestOrder>(b => b
                    .Map(o => o.OrderId, "order_id")
                    .Map(o => o.CustomerId, "customer_id")
                    .Ignore(o => o.IsExpedited))
                .Build();

            var resolver = factory.GetResolver<TestOrder>();

            resolver.GetAttributeName("OrderId").Should().Be("order_id");
            resolver.GetAttributeName("CustomerId").Should().Be("customer_id");
            resolver.IsStoredAttribute("IsExpedited").Should().BeFalse();
        }
    }

    #endregion

    #region Test Entity Classes

    public class TestEntityPlain
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    public class TestEntityWithAttributes
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";

        [DynamoDbAttribute("customer_id")]
        public Guid CustomerId { get; set; }
    }

    public class TestEntityWithIgnore
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";

        [DynamoDbIgnore]
        public string ComputedValue => Name.ToUpper();
    }

    public class AwsSdkAnnotatedEntity
    {
        public Guid Id { get; set; }

        [Amazon.DynamoDBv2.DataModel.DynamoDBProperty("display_name")]
        public string DisplayName { get; set; } = "";

        [Amazon.DynamoDBv2.DataModel.DynamoDBIgnore]
        public string Computed { get; set; } = "";
    }

    public class DualAnnotatedEntity
    {
        public Guid Id { get; set; }

        [DynamoDbAttribute("lib_name")]
        [Amazon.DynamoDBv2.DataModel.DynamoDBProperty("sdk_name")]
        public string Name { get; set; } = "";
    }

    public class TestOrder
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public TestAddress? ShippingAddress { get; set; }
        public bool IsExpedited { get; set; }
    }

    public class TestAddress
    {
        public string City { get; set; } = "";
        public string PostCode { get; set; } = "";
        public int Floor { get; set; }
    }

    #endregion
}
