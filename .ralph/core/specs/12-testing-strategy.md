# Spec 12: Testing Strategy

## Motivation

A comprehensive testing strategy ensures correctness of expression tree analysis, type conversion, result mapping, and AWS SDK integration. The library handles complex runtime code generation (compiled delegates, expression visitors) that demands thorough coverage.

## Test Project Structure

```
DynamoDb.ExpressionMapping.Tests/
├── Expressions/
│   ├── ProjectionExpressionVisitorTests.cs
│   ├── ProjectionBuilderTests.cs
│   ├── FilterExpressionBuilderTests.cs
│   ├── FilterExpressionResultComposabilityTests.cs
│   ├── ConditionExpressionBuilderTests.cs
│   ├── ConditionExpressionResultComposabilityTests.cs
│   ├── KeyConditionExpressionBuilderTests.cs
│   └── UpdateExpressionBuilderTests.cs
├── Mapping/
│   ├── AttributeNameResolverTests.cs
│   ├── AttributeNameResolverFactoryTests.cs
│   ├── AttributeValueConverterTests.cs
│   ├── ConverterRegistryTests.cs
│   └── ExpressionValueEmitterTests.cs
├── ResultMapping/
│   ├── DirectResultMapperTests.cs
│   └── ResultMapperCacheTests.cs
├── ReservedKeywords/
│   ├── ReservedKeywordRegistryTests.cs
│   └── AliasGeneratorTests.cs
├── Extensions/
│   ├── ProjectionExtensionsTests.cs
│   ├── FilterExtensionsTests.cs
│   ├── ConditionExtensionsTests.cs
│   ├── KeyConditionExtensionsTests.cs
│   ├── UpdateExtensionsTests.cs
│   └── MergeHelpersTests.cs
├── Caching/
│   ├── ExpressionCacheTests.cs
│   └── ExpressionKeyGeneratorTests.cs
├── Exceptions/
│   └── ExceptionHierarchyTests.cs
├── Configuration/
│   └── ConfigurationAndDiTests.cs
├── Integration/
│   ├── DynamoDbFixture.cs
│   ├── ProjectionIntegrationTests.cs
│   ├── FilterIntegrationTests.cs
│   ├── KeyConditionIntegrationTests.cs
│   ├── UpdateIntegrationTests.cs
│   ├── ConditionIntegrationTests.cs
│   ├── DirectResultMapperIntegrationTests.cs
│   └── CombinedExpressionIntegrationTests.cs
└── Fixtures/
    ├── TestEntity.cs
    ├── TestKeyedEntity.cs
    ├── TestEntityBuilder.cs
    ├── AwsSdkAnnotatedEntity.cs
    └── AttributeValueFixtures.cs
```

## Test Framework

- **xUnit** for test framework
- **FluentAssertions** for assertions
- **NSubstitute** for mocking interfaces
- **Bogus** for test data generation
- **Testcontainers.DynamoDb** for integration tests (manages DynamoDB Local container lifecycle automatically)

## Unit Test Coverage

### ProjectionExpressionVisitor (Spec 02)

```csharp
// Property extraction tests
[Fact] SingleProperty_ExtractsOnePath()
[Fact] AnonymousType_ExtractsAllProperties()
[Fact] ObjectInitialiser_ExtractsSourceProperties()
[Fact] NestedProperty_ExtractsFullPath()
[Fact] IdentityExpression_ReturnsEmptyPaths()
[Fact] ValueTuple_ExtractsAllProperties()
[Fact] DuplicateProperty_Deduplicated()
[Fact] IntermediateNode_NotAddedAsSeparatePath()
[Fact] MultipleNestedPaths_InSameAnonymousType_ExtractsAll()
[Fact] DeeplyNestedPath_ThreeLevels_ExtractsFullPath()

// Unsupported expressions (Spec 14 §2)
[Fact] MethodCall_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()
[Fact] Arithmetic_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()
[Fact] Conditional_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()
[Fact] ArrayIndex_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()
[Fact] StringConcatenation_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()

// PropertyPath metadata — SegmentProperties (Spec 02 §2, §7)
[Fact] SegmentProperties_SingleProperty_ContainsOneEntry()
[Fact] SegmentProperties_NestedPath_ContainsEntryPerSegment()
[Fact] SegmentProperties_NestedPath_IntermediateDeclaringType_IsCorrect()
[Fact] SegmentProperties_NestedPath_IntermediatePropertyType_MatchesNextDeclaringType()
[Fact] SegmentProperties_DeeplyNested_ThreeLevels_ContainsThreeEntries()
[Fact] PropertyInfo_IsLastSegmentProperty()
[Fact] ProjectionShape_Identity_ForWholeObject()
[Fact] ProjectionShape_SingleProperty_ForOneProperty()
[Fact] ProjectionShape_Composite_ForAnonymousType()
[Fact] ProjectionShape_Composite_ForObjectInitialiser()
```

### ProjectionBuilder (Spec 03)

```csharp
// Expression building
[Fact] SimpleProperties_CommaSeparated()
[Fact] ReservedKeyword_Aliased()
[Fact] NestedPath_PreservesDots()
[Fact] MixedReservedAndNonReserved_CorrectAliasing()
[Fact] EmptyProjection_ReturnsEmptyResult()
[Fact] SpecialCharacters_Aliased()
[Fact] NestedPath_ReservedSegment_OnlyReservedSegmentAliased()

// Attribute name resolution
[Fact] DynamoDbAttribute_UsesRemappedName()
[Fact] DynamoDbIgnore_StrictMode_ThrowsInvalidProjectionException_WithPropertyAndType()
[Fact] DynamoDbIgnore_LenientMode_Excludes()
[Fact] PassThroughResolver_UsesPropertyNameAsIs()
[Fact] FluentOverride_TakesPrecedence()
[Fact] NestedPath_CrossTypeResolution_UsesFactoryPerSegment()
[Fact] NestedPath_RemappedOnBothTypes_ResolvesCorrectly()

// Result properties
[Fact] ProjectionResult_IsEmpty_TrueForIdentity()
[Fact] ProjectionResult_Shape_MatchesExpressionPattern()
[Fact] ProjectionResult_ResolvedAttributeNames_ContainsResolvedNames()

// Alias scoping
[Fact] ProjectionAliases_UseProjPrefix()
[Fact] NoCollisionWithFilterAliases()

// Caching
[Fact] SameExpression_ReturnsCachedProjectionResult()

// Validation
[Fact] NullSelector_ThrowsArgumentNullException()
```

### AttributeNameResolver (Spec 01)

```csharp
// Resolution
[Fact] PropertyWithNoAnnotation_ReturnsPropertyNameAsIs()
[Fact] DynamoDbAttribute_ReturnsRemappedName()
[Fact] DynamoDbIgnore_StrictMode_GetAttributeName_ThrowsInvalidProjectionException()
[Fact] DynamoDbIgnore_LenientMode_IsStoredAttribute_ReturnsFalse()
[Fact] IsStoredAttribute_ReturnsTrueForStoredProperty()
[Fact] IsStoredAttribute_ReturnsFalseForIgnoredProperty()
[Fact] GetPropertyName_ReturnsReverseMapping()
[Fact] GetPropertyName_RemappedAttribute_ReturnsOriginalPropertyName()

// AWS SDK interop (Spec 01 §7)
[Fact] AwsSdkDynamoDBProperty_ReturnsRemappedName()
[Fact] AwsSdkDynamoDBIgnore_IsStoredAttribute_ReturnsFalse()

// Resolution order (Spec 01 §5)
[Fact] FluentOverride_TakesPrecedenceOver_DynamoDbAttribute()
[Fact] DynamoDbAttribute_TakesPrecedenceOver_DynamoDBProperty()
[Fact] DynamoDBProperty_TakesPrecedenceOver_ConventionName()
[Fact] BothAnnotationsPresent_LibraryAnnotationWins()

// Fluent builder (Spec 01 §6)
[Fact] FluentMap_OverridesPropertyName()
[Fact] FluentIgnore_MarksPropertyAsNotStored()

// Per-property converter annotation
[Fact] DynamoDbConverterAttribute_DetectedOnProperty()

// Caching (Spec 01 §8)
[Fact] SameType_ReturnsCachedMetadata()

// Validation
[Fact] DynamoDbAttribute_EmptyName_ThrowsArgumentException()
[Fact] DynamoDbConverterAttribute_NullType_ThrowsArgumentNullException()
```

### AttributeNameResolverFactory (Spec 01 §10–§13)

```csharp
// Factory creation
[Fact] GetResolver_CreatesResolverForArbitraryType()
[Fact] GetResolver_CachesResolverPerType()
[Fact] GetResolver_SameType_ReturnsSameInstance()
[Fact] GetResolverGeneric_ReturnsTypedResolver()

// Factory registration
[Fact] Register_OverridesAutoDiscoveredResolver()

// Cross-type nested path resolution (Spec 01 §13)
[Fact] NestedPath_ResolvesEachSegmentAgainstCorrectType()
[Fact] NestedPath_RemappedChildType_ResolvesCorrectly()

// Factory builder (Spec 01 §12)
[Fact] FactoryBuilder_ConfiguresMultipleTypes()
[Fact] FactoryBuilder_UnconfiguredType_FallsBackToAutoDiscovery()
[Fact] FactoryBuilder_WithMode_AppliesModeToAllResolvers()
```

### AttributeValueConverters (Spec 05)

```csharp
// Round-trip per built-in converter type
[Fact] String_RoundTrip_PreservesValue()
[Fact] Guid_RoundTrip_PreservesValue()
[Fact] Bool_RoundTrip_PreservesValue()
[Fact] Int_RoundTrip_PreservesValue()
[Fact] Long_RoundTrip_PreservesValue()
[Fact] Decimal_RoundTrip_PreservesValue()
[Fact] Double_RoundTrip_PreservesValue()
[Fact] DateTime_RoundTrip_PreservesValue()
[Fact] DateTimeOffset_RoundTrip_PreservesValue()
[Fact] ByteArray_RoundTrip_PreservesValue()
[Fact] ListOfString_RoundTrip_PreservesValue()
[Fact] ListOfInt_RoundTrip_PreservesValue()
[Fact] HashSetOfString_RoundTrip_PreservesValue()
[Fact] DictionaryOfStringString_RoundTrip_PreservesValue()

// Null / missing handling
[Fact] String_FromNull_ReturnsNull()
[Fact] Guid_FromNull_ReturnsGuidEmpty()
[Fact] Bool_FromMissing_ReturnsFalse()
[Fact] Int_FromNull_ReturnsZero()
[Fact] DateTime_FromNull_ReturnsMinValue()
[Fact] ByteArray_FromNull_ReturnsNull()
[Fact] ListOfString_FromMissing_ReturnsEmptyList()
[Fact] HashSetOfString_FromMissing_ReturnsEmptySet()

// Nullable wrapper (Spec 05 §4)
[Fact] NullableInt_FromNull_ReturnsNull()
[Fact] NullableInt_FromValue_ReturnsValue()
[Fact] NullableGuid_FromNull_ReturnsNull()
[Fact] NullableDateTime_FromValue_ReturnsValue()
[Fact] NullableInt_ToAttributeValue_NullWritesNULL()
[Fact] NullableInt_ToAttributeValue_ValueWritesN()

// Enum converter (Spec 05 §5)
[Fact] Enum_StringMode_RoundTrip_PreservesValue()
[Fact] Enum_NumberMode_RoundTrip_PreservesValue()
[Fact] Enum_StringMode_ParsesIgnoreCase()

// DateTime format
[Fact] DateTime_ToAttributeValue_WritesIso8601RoundtripFormat()
[Fact] DateTimeOffset_ToAttributeValue_WritesIso8601RoundtripFormat()
```

### ConverterRegistry (Spec 05 §2)

```csharp
// Default registry
[Fact] Default_ContainsAllBuiltInConverters()
[Fact] Default_IsFrozen_RegisterThrowsInvalidOperationException()
[Fact] HasConverter_ReturnsTrueForRegisteredType()
[Fact] HasConverter_ReturnsFalseForUnregisteredType()

// Clone (Spec 05 §2)
[Fact] Clone_CreatesMutableCopy()
[Fact] Clone_MutationsDoNotAffectSource()

// Custom registration
[Fact] Register_CustomConverter_OverridesExisting()
[Fact] Register_CustomConverter_AvailableViaGetConverter()

// Resolution order (Spec 05 §8)
[Fact] GetConverter_NullableType_WrapsInnerConverter()
[Fact] GetConverter_EnumType_ReturnsEnumConverter()
[Fact] GetConverter_UnregisteredType_ThrowsMissingConverterException()
[Fact] MissingConverterException_CarriesTargetType()

// Open-generic collection resolution (Spec 05 §8a)
[Fact] GetConverter_ListOfEnum_ComposesListConverterWithEnumConverter()
[Fact] GetConverter_ListOfGuid_ComposesListConverterWithGuidConverter()
[Fact] GetConverter_HashSetOfGuid_ComposesSetConverterWithGuidConverter()
[Fact] GetConverter_HashSetOfInt_UsesNativeNS()
[Fact] GetConverter_HashSetOfEnum_UsesSS_WhenStringMode()
[Fact] GetConverter_DictionaryStringMoney_ComposesMapConverterWithCustomConverter()
[Fact] GetConverter_DictionaryIntString_ThrowsMissingConverter_NonStringKey()
[Fact] GetConverter_NestedList_ListOfListOfString_ComposesRecursively()
[Fact] GetConverter_ListOfCustomType_WithRegisteredConverter_Composes()
[Fact] GetConverter_ListOfUnregisteredType_ThrowsMissingConverterException()
[Fact] GenericCollectionConverter_CachedAfterFirstResolution()
[Fact] ExactTypeRegistration_TakesPrecedenceOverGenericResolution()

// Per-property override (Spec 05 §7)
[Fact] DynamoDbConverterAttribute_TakesPrecedenceOverRegistry()

// Null handling mode (Spec 05 §10)
[Fact] OmitNull_NullStringNotWritten()
[Fact] ExplicitNull_NullStringWritesNULLAttribute()
```

### ExpressionValueEmitter (Spec 05 §11)

```csharp
// Converter resolution — delegates to Spec 05 §8 resolution order
[Fact] Emit_WithDynamoDbConverterAttribute_UsesAttributeConverter()
[Fact] Emit_WithRegisteredConverter_UsesRegistryConverter()
[Fact] Emit_NullableValue_WrapsInNullableConverter()
[Fact] Emit_EnumValue_UsesEnumConverter()
[Fact] Emit_CollectionValue_UsesGenericCollectionConverter()
[Fact] Emit_UnregisteredType_ThrowsMissingConverterException()

// PropertyInfo null handling
[Fact] Emit_NullPropertyInfo_ResolvesViaRuntimeType()
[Fact] Emit_NullPropertyInfo_EnumValue_UsesEnumConverter()

// Integration with expression builders
[Fact] Emit_GuidValue_ProducesStringAttributeValue()
[Fact] Emit_DecimalValue_ProducesNumberAttributeValue()
[Fact] Emit_DateTimeValue_ProducesIso8601StringAttributeValue()
[Fact] Emit_BoolValue_ProducesBoolAttributeValue()
[Fact] Emit_CustomTypeWithConverter_ProducesMapAttributeValue()

// Thread safety
[Fact] Emit_ConcurrentCalls_NoRaceConditions()
```

### FilterExpressionBuilder (Spec 06)

```csharp
// Comparison operators
[Fact] Equality_GeneratesEqualsExpression()
[Fact] Inequality_GeneratesNotEqualsExpression()
[Fact] GreaterThan_GeneratesCorrectExpression()
[Fact] LessThan_GeneratesCorrectExpression()
[Fact] GreaterThanOrEqual_GeneratesCorrectExpression()
[Fact] LessThanOrEqual_GeneratesCorrectExpression()

// Logical operators
[Fact] And_CombinesWithAND()
[Fact] Or_CombinesWithOR()
[Fact] Not_WrapsWithNOT()
[Fact] ComplexPredicate_CorrectParentheses()

// Boolean properties (Spec 06 §3)
[Fact] BooleanPropertyDirect_GeneratesBoolEqualsTrue()
[Fact] NegatedBooleanProperty_GeneratesBoolEqualsFalse()

// String operations
[Fact] StartsWith_GeneratesBeginsWith()
[Fact] Contains_GeneratesContains()

// Null checks
[Fact] EqualsNull_GeneratesAttributeNotExists()
[Fact] NotEqualsNull_GeneratesAttributeExists()

// DynamoDB functions (Spec 06 §4)
[Fact] Between_GeneratesBETWEEN()
[Fact] Size_GeneratesSize()
[Fact] DynamoDbFunctions_AttributeExists_GeneratesAttributeExists()
[Fact] DynamoDbFunctions_AttributeNotExists_GeneratesAttributeNotExists()
[Fact] DynamoDbFunctions_AttributeType_GeneratesAttributeType()
[Fact] DynamoDbFunctions_CalledAtRuntime_ThrowsInvalidOperationException()

// Captured variables
[Fact] CapturedVariable_EvaluatedAtBuildTime()
[Fact] CapturedEnumValue_ConvertedToAttributeValue()

// Value conversion
[Fact] GuidValue_ConvertedToStringAttributeValue()
[Fact] BoolValue_ConvertedToBoolAttributeValue()
[Fact] DateTimeValue_ConvertedToIso8601String()
[Fact] EnumValue_ConvertedPerStorageMode()

// IN operator
[Fact] ContainsOnArray_GeneratesINExpression()

// Nested property (Spec 06 §8)
[Fact] NestedProperty_ResolvesViaFactory_GeneratesDotNotation()
[Fact] NestedProperty_RemappedAttribute_UsesResolvedName()

// Attribute name resolution
[Fact] RemappedAttribute_UsesResolvedNameInExpression()
[Fact] ReservedKeyword_AliasedWithFiltPrefix()

// Alias scoping (Spec 06 §5)
[Fact] FilterAliases_UseFiltPrefix()
[Fact] FilterValueAliases_UseFiltVPrefix()

// Validation (Spec 06 §9)
[Fact] NullPredicate_ThrowsArgumentNullException()
[Fact] DynamoDbIgnore_StrictMode_ThrowsInvalidFilterException_WithPropertyAndType()
[Fact] NonBooleanExpression_ThrowsInvalidFilterException()
```

### FilterExpressionResultComposability (Spec 06 §6)

```csharp
// And composability
[Fact] And_TwoFilters_ReAliasesRightOperand()
[Fact] And_LeftEmpty_ReturnsRight()
[Fact] And_RightEmpty_ReturnsLeft()
[Fact] And_BothEmpty_ReturnsEmpty()
[Fact] And_NullOperand_ThrowsArgumentNullException()
[Fact] And_Chained_ThreeFilters_ContiguousIndices()
[Fact] And_BothFiltersUseNameAliases_IndicesDisjoint()
[Fact] And_HighIndexAliases_NoPartialMatchCorruption()

// Or composability
[Fact] Or_TwoFilters_ReAliasesRightOperand()
[Fact] Or_MergedNames_NoCollision()
[Fact] Or_MergedValues_NoCollision()
```

### ConditionExpressionBuilder (Spec 06 §10)

```csharp
// Core expression building — same patterns as filter, different result type
[Fact] Equality_GeneratesEqualsExpression()
[Fact] And_CombinesWithAND()
[Fact] NullCheck_GeneratesAttributeNotExists()
[Fact] Between_GeneratesBETWEEN()
[Fact] CapturedVariable_EvaluatedAtBuildTime()

// Alias scoping — must use #cond_ / :cond_v prefixes
[Fact] ConditionAliases_UseCondPrefix()
[Fact] ConditionValueAliases_UseCondVPrefix()

// Result type
[Fact] BuildCondition_ReturnsConditionExpressionResult_NotFilterResult()

// Validation
[Fact] NullPredicate_ThrowsArgumentNullException()
[Fact] DynamoDbIgnore_StrictMode_ThrowsInvalidFilterException()
```

### ConditionExpressionResultComposability (Spec 06 §6.7)

```csharp
// Composability with #cond_ re-aliasing
[Fact] And_TwoConditions_ReAliasesWithCondPrefix()
[Fact] Or_TwoConditions_ReAliasesWithCondPrefix()
[Fact] And_LeftEmpty_ReturnsRight()
[Fact] And_NullOperand_ThrowsArgumentNullException()
```

### KeyConditionExpressionBuilder (Spec 13)

```csharp
// Partition key only (Spec 13 §4)
[Fact] PartitionKeyOnly_GeneratesEqualityExpression()

// Partition key + sort key operators (Spec 13 §2)
[Fact] SortKeyEquals_GeneratesEquality()
[Fact] SortKeyLessThan_GeneratesLessThan()
[Fact] SortKeyLessThanOrEqual_GeneratesLessThanOrEqual()
[Fact] SortKeyGreaterThan_GeneratesGreaterThan()
[Fact] SortKeyGreaterThanOrEqual_GeneratesGreaterThanOrEqual()
[Fact] SortKeyBetween_GeneratesBETWEEN()
[Fact] SortKeyBeginsWith_GeneratesBeginsWithFunction()

// Alias scoping (Spec 13 §7)
[Fact] KeyConditionAliases_UseKeyPrefix()
[Fact] KeyConditionValueAliases_UseKeyVPrefix()

// Smart aliasing (Spec 13 §9)
[Fact] NonReservedAttribute_UsedDirectly_NotAliased()
[Fact] ReservedAttribute_Aliased()

// Property resolution (Spec 13 §5)
[Fact] RemappedAttribute_UsesResolvedName()

// Value conversion (Spec 13 §6)
[Fact] StringValue_ConvertedToStringAttributeValue()
[Fact] GuidValue_ConvertedToStringAttributeValue()

// Result type
[Fact] Build_ReturnsKeyConditionExpressionResult()

// Validation (Spec 13 §8)
[Fact] NullPropertyExpression_ThrowsArgumentNullException()
[Fact] DynamoDbIgnore_ThrowsInvalidKeyConditionException_WithPropertyAndType()
[Fact] NestedProperty_ThrowsInvalidKeyConditionException_WithPropertyName()
[Fact] NullPartitionKeyValue_ThrowsArgumentNullException()
[Fact] Between_LowGreaterThanHigh_ThrowsArgumentException()
[Fact] BeginsWith_NullPrefix_ThrowsArgumentException()
[Fact] BeginsWith_EmptyPrefix_ThrowsArgumentException()

// Thread safety (Spec 13 §11)
[Fact] ConcurrentWithPartitionKey_ProducesIndependentBuilders()
```

### UpdateExpressionBuilder (Spec 07)

```csharp
// Clause generation
[Fact] Set_GeneratesSETClause()
[Fact] Increment_GeneratesAddExpression()
[Fact] Decrement_GeneratesSubtractExpression()
[Fact] SetIfNotExists_GeneratesIfNotExistsFunction()
[Fact] AppendToList_GeneratesListAppendFunction()
[Fact] Remove_GeneratesREMOVEClause()
[Fact] Add_GeneratesADDClause()
[Fact] Delete_GeneratesDELETEClause()
[Fact] MultipleClauses_CombinedCorrectly()
[Fact] NoOperations_ReturnsEmpty()
[Fact] DuplicateProperty_LastWins()

// Alias scoping (Spec 07 §5)
[Fact] UpdateAliases_UseUpdPrefix()
[Fact] UpdateValueAliases_UseUpdVPrefix()
[Fact] ReservedKeyword_AliasedInUpdateExpression()

// Property resolution (Spec 07 §5)
[Fact] RemappedAttribute_UsesResolvedName()
[Fact] NestedProperty_ResolvesCrossType()

// Value conversion (Spec 07 §6)
[Fact] EnumValue_ConvertedViaRegistry()

// Validation (Spec 07 §7)
[Fact] ConflictingClauses_ThrowsInvalidUpdateException_WithPropertyName()
[Fact] DynamoDbIgnore_ThrowsInvalidUpdateException_WithPropertyAndType()
[Fact] NullPropertyExpression_ThrowsArgumentNullException()
```

### ReservedKeywordRegistry (Spec 08)

```csharp
// Detection (Spec 08 §1)
[Fact] KnownReservedWord_IsReserved_ReturnsTrue()
[Fact] NonReservedWord_IsReserved_ReturnsFalse()
[Fact] IsReserved_CaseInsensitive()
[Fact] EmptyString_IsReserved_ReturnsFalse()
[Fact] NullString_IsReserved_ReturnsFalse()

// Common reserved words that overlap with typical attribute names
[Fact] Status_IsReserved()
[Fact] Name_IsReserved()
[Fact] Date_IsReserved()
[Fact] Comment_IsReserved()
[Fact] Value_IsReserved()

// NeedsEscaping (Spec 08 §1)
[Fact] ReservedWord_NeedsEscaping_ReturnsTrue()
[Fact] SpecialCharacters_NeedsEscaping_ReturnsTrue()
[Fact] UnderscoreOnly_NeedsEscaping_ReturnsFalse()
[Fact] AlphanumericOnly_NeedsEscaping_ReturnsFalse()
```

### AliasGenerator (Spec 08 §2)

```csharp
// Sequential generation
[Fact] NextName_GeneratesSequentialAliases()
[Fact] NextValue_GeneratesSequentialAliases()
[Fact] Reset_ResetsCountersToZero()

// Scoped prefixes (Spec 08 §3)
[Fact] ProjScope_GeneratesHashProjPrefix()
[Fact] FiltScope_GeneratesHashFiltPrefix()
[Fact] CondScope_GeneratesHashCondPrefix()
[Fact] UpdScope_GeneratesHashUpdPrefix()
[Fact] KeyScope_GeneratesHashKeyPrefix()
[Fact] FiltScope_ValuePrefix_GeneratesColonFiltV()
[Fact] CondScope_ValuePrefix_GeneratesColonCondV()
[Fact] UpdScope_ValuePrefix_GeneratesColonUpdV()
[Fact] KeyScope_ValuePrefix_GeneratesColonKeyV()

// No collision across scopes (Spec 08 §5)
[Fact] DifferentScopes_ProduceDifferentPrefixes()
```

### ExpressionCache (Spec 09)

```csharp
[Fact] SameExpression_ReturnsCachedResult()
[Fact] DifferentExpression_ReturnsDifferentResult()
[Fact] StructurallyIdentical_SameKey()
[Fact] NoCache_AlwaysBuilds()
[Fact] ThreadSafety_ConcurrentAccess()

// Statistics (Spec 09 §6)
[Fact] GetStatistics_ReportsHitsAndMisses()
[Fact] GetStatistics_ReportsTotalEntries()

// Default instance (Spec 09 §1)
[Fact] Default_IsSingletonInstance()
```

### ExpressionKeyGenerator (Spec 09 §2, §9)

```csharp
// Key correctness (Spec 09 §9)
[Fact] SingleProperty_DifferentFromWrappedInAnonymousType()
[Fact] AnonymousType_DifferentMemberName_DifferentKey()
[Fact] AnonymousType_VsNamedType_DifferentKey()
[Fact] StructurallyIdentical_DifferentCallSites_SameKey()

// Key structure
[Fact] Key_IncludesSourceTypeName()
[Fact] Key_IncludesResultTypeName()

// Filter caching behaviour (Spec 09 §3)
[Fact] FilterExpressions_NotCachedByDefault()
```

### DirectResultMapper\<TSource\> (Spec 04)

```csharp
// Mapping strategies (Spec 04 §2)
[Fact] SingleProperty_MapsDirectly()
[Fact] AnonymousType_ConstructsViaConstructor()
[Fact] NamedType_ConstructsViaPropertySetters()
[Fact] Record_ConstructsViaConstructor()
[Fact] ParameterisedConstructor_ConstructsViaConstructorArgs()
[Fact] Identity_DelegatesToFallback()

// Type conversion — all built-in types
[Fact] StringAttribute_MapsToString()
[Fact] GuidAttribute_MapsToGuid()
[Fact] BoolAttribute_MapsToBool()
[Fact] NumberAttribute_MapsToInt()
[Fact] NumberAttribute_MapsToLong()
[Fact] NumberAttribute_MapsToDecimal()
[Fact] NumberAttribute_MapsToDouble()
[Fact] DateTimeAttribute_MapsToDateTime()
[Fact] DateTimeOffsetAttribute_MapsToDateTimeOffset()
[Fact] ByteArrayAttribute_MapsToBytesArray()
[Fact] ListAttribute_MapsToListOfString()
[Fact] HashSetAttribute_MapsToHashSetOfString()
[Fact] DictionaryAttribute_MapsToDictionaryOfStringString()
[Fact] NullableAttribute_MapsToNullable()
[Fact] EnumAttribute_MapsToEnum()

// Missing / null attributes (Spec 04 §10)
[Fact] MissingAttribute_ReturnsDefault()
[Fact] NullAttribute_ReturnsDefault()
[Fact] WrongDynamoDbType_ReturnsDefault()

// Nested attributes (Spec 04 §6)
[Fact] NestedMapAttribute_MapsCorrectly()
[Fact] NavigateToLeaf_MissingIntermediate_ReturnsDefault()
[Fact] NavigateToLeaf_IntermediateNotMap_ReturnsDefault()
[Fact] NestedPath_CustomConverterOnLeaf_UsesConverter()

// One-shot Map method (Spec 04 §1)
[Fact] Map_OneShot_ReturnsMappedResult()
[Fact] Map_OneShot_UsesCachedMapperInternally()

// Performance
[Fact] CreateMapper_ReturnsReusableDelegate()
[Fact] CachedMapper_ReturnsSameDelegate()

// Custom converters (Spec 04 §4)
[Fact] DynamoDbConverterAttribute_UsesCustomConverter()
[Fact] RegisteredConverter_UsedForType()

// Validation (Spec 04 §10)
[Fact] NoConverterForType_ThrowsMissingConverterException_AtCreationTime()
[Fact] UnsupportedExpressionShape_ThrowsUnsupportedExpressionException_AtCreationTime()
```

### ProjectionExtensions (Spec 10 §1)

```csharp
// GetItemRequest
[Fact] GetItemRequest_WithProjection_SetsProjectionExpression()
[Fact] GetItemRequest_WithProjection_MergesAttributeNames()

// QueryRequest
[Fact] QueryRequest_WithProjection_SetsProjectionExpression()
[Fact] QueryRequest_WithProjection_NullSelector_NoOp()

// ScanRequest
[Fact] ScanRequest_WithProjection_SetsProjectionExpression()
[Fact] ScanRequest_WithProjection_NullSelector_NoOp()

// BatchGetItemRequest (Spec 10 §1)
[Fact] BatchGetItemRequest_WithProjection_SetsProjectionOnTable()
[Fact] BatchGetItemRequest_WithProjection_TableNotFound_ThrowsArgumentException()
[Fact] BatchGetItemRequest_WithProjection_NullRequestItems_ThrowsArgumentNullException()

// Null builder
[Fact] WithProjection_NullBuilder_ThrowsArgumentNullException()
```

### FilterExtensions (Spec 10 §2)

```csharp
[Fact] QueryRequest_WithFilter_SetsFilterExpression()
[Fact] QueryRequest_WithFilter_MergesAttributeNamesAndValues()
[Fact] ScanRequest_WithFilter_SetsFilterExpression()
[Fact] FluentChaining_ProjectionAndFilter()
```

### ConditionExtensions (Spec 10 §3)

```csharp
[Fact] PutItemRequest_WithCondition_SetsConditionExpression()
[Fact] PutItemRequest_WithCondition_MergesAttributeNamesAndValues()
[Fact] DeleteItemRequest_WithCondition_SetsConditionExpression()
[Fact] UpdateItemRequest_WithCondition_SetsConditionExpression()
[Fact] WithCondition_NullBuilder_ThrowsArgumentNullException()
```

### KeyConditionExtensions (Spec 10 §4)

```csharp
[Fact] QueryRequest_WithKeyCondition_SetsKeyConditionExpression()
[Fact] QueryRequest_WithKeyCondition_MergesAttributeNamesAndValues()
[Fact] WithKeyCondition_NullBuilder_ThrowsArgumentNullException()
[Fact] WithKeyCondition_NullConfigure_ThrowsArgumentNullException()
```

### UpdateExtensions (Spec 10 §5)

```csharp
[Fact] UpdateItemRequest_WithUpdate_SetsUpdateExpression()
[Fact] UpdateItemRequest_WithUpdate_MergesAttributeNamesAndValues()
[Fact] WithUpdate_EmptyResult_NoOp()
```

### MergeHelpers (Spec 10 §6)

```csharp
[Fact] MergeAttributeNames_DisjointKeys_MergesAll()
[Fact] MergeAttributeNames_SameKeyAndValue_NoConflict()
[Fact] MergeAttributeNames_SameKeyDifferentValue_ThrowsConflictException()
[Fact] MergeAttributeValues_DisjointKeys_MergesAll()
[Fact] MergeAttributeValues_DuplicateKey_ThrowsConflictException()
[Fact] ConflictException_CarriesAliasKeyAndValues()
```

### CombinedExtensions (Spec 10 §7, §9)

```csharp
[Fact] QueryRequest_WithExpressions_AppliesProjectionAndFilter()
[Fact] FluentChaining_KeyCondition_Projection_Filter_AllApplied()
[Fact] FluentChaining_AllScopes_AliasesDoNotCollide()
```

### Exception Hierarchy (Spec 14)

```csharp
// Hierarchy structure
[Fact] AllExceptions_DeriveFromExpressionMappingException()
[Fact] InvalidProjectionException_DeriveFromInvalidExpressionException()
[Fact] InvalidFilterException_DeriveFromInvalidExpressionException()
[Fact] InvalidUpdateException_DeriveFromInvalidExpressionException()
[Fact] InvalidKeyConditionException_DeriveFromInvalidExpressionException()

// Structured properties
[Fact] UnsupportedExpressionException_CarriesNodeTypeAndText()
[Fact] MissingConverterException_CarriesTargetTypeAndPropertyName()
[Fact] MissingConverterException_NullPropertyName_WhenDirectRegistryCall()
[Fact] ExpressionAttributeConflictException_CarriesAliasKeyAndValues()
[Fact] ExpressionAttributeConflictException_NullConflictingValue_WhenValuePlaceholder()
[Fact] InvalidProjectionException_CarriesPropertyNameAndEntityType()
[Fact] InvalidFilterException_CarriesPropertyNameAndEntityType()
[Fact] InvalidUpdateException_CarriesPropertyNameAndEntityType()
[Fact] InvalidKeyConditionException_CarriesPropertyNameAndEntityType()

// Message formatting
[Fact] UnsupportedExpressionException_MessageContainsNodeTypeAndText()
[Fact] MissingConverterException_MessageContainsTypeName()
[Fact] InvalidProjectionException_MessageContainsPropertyAndEntityName()

// Inner exception support
[Fact] ExpressionMappingException_PreservesInnerException()

// Catch patterns
[Fact] CatchExpressionMappingException_CatchesAllLibraryExceptions()
[Fact] CatchInvalidExpressionException_CatchesAllBuilderValidationErrors()
[Fact] CatchInvalidExpressionException_DoesNotCatchUnsupportedOrMissing()
```

### Configuration and DI (Spec 11)

```csharp
// Default config (Spec 11 §1)
[Fact] DefaultConfig_HasExpectedDefaults()
[Fact] DefaultConfig_NameResolutionMode_IsStrict()
[Fact] DefaultConfig_NullHandlingMode_IsOmitNull()
[Fact] DefaultConfig_LoggerFactory_IsNullLoggerFactory()

// Builder (Spec 11 §2)
[Fact] Builder_OverridesIndividualSettings()
[Fact] Builder_WithNameResolutionMode_AppliesMode()
[Fact] Builder_WithNullHandling_AppliesMode()
[Fact] Builder_WithCache_AppliesCustomCache()
[Fact] Builder_WithLoggerFactory_AppliesFactory()
[Fact] Builder_WithConverter_ClonesDefaultRegistryBeforeMutating()
[Fact] Builder_WithConverter_DoesNotMutateDefaultRegistry()

// DI registration (Spec 11 §3)
[Fact] AddDynamoDbExpressionMapping_RegistersOpenGenericBuilders()
[Fact] AddDynamoDbExpressionMapping_RegistersResolverFactory()
[Fact] AddDynamoDbExpressionMapping_WithConfigure_AppliesSettings()

// Per-entity configuration (Spec 11 §5)
[Fact] AddDynamoDbEntity_OverridesOpenGenericResolver()
[Fact] AddDynamoDbEntity_RegistersIntoFactory_ForNestedResolution()
[Fact] AddDynamoDbEntity_MultipleEntities_EachConfiguredIndependently()
[Fact] OpenGenericResolver_FallsBackToReflection()

// Manual instantiation (Spec 11 §4)
[Fact] ManualInstantiation_WorksWithoutDI()
[Fact] ManualInstantiation_WithFluentFactoryBuilder()
```

## Integration Tests

Integration tests verify that the expressions produced by the library are accepted by DynamoDB and return the expected results. They are **not** for testing expression tree analysis, type conversion, or mapping logic — those are covered exhaustively by unit tests.

**Scope**: write data → execute operation with generated expression → assert DynamoDB returns what we expect.

### Infrastructure: Testcontainers

```csharp
/// <summary>
/// Shared fixture that starts a DynamoDB Local container once per test collection.
/// Implements IAsyncLifetime for xUnit's collection fixture pattern.
/// </summary>
public class DynamoDbFixture : IAsyncLifetime
{
    private DynamoDbContainer _container;
    public IAmazonDynamoDB Client { get; private set; }

    public async Task InitializeAsync()
    {
        _container = new DynamoDbBuilder()
            .WithImage("amazon/dynamodb-local:latest")
            .Build();

        await _container.StartAsync();

        Client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonDynamoDBConfig { ServiceURL = _container.GetConnectionString() });
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("DynamoDb")]
public class DynamoDbCollection : ICollectionFixture<DynamoDbFixture> { }
```

Each test class creates its own table in the constructor (unique name per test) and deletes it in `IAsyncLifetime.DisposeAsync()` for isolation. Integration test classes use `[Collection("DynamoDb")]` to share the container fixture.

### Test Cases

Focus: expressions that could be syntactically wrong in ways unit tests can't catch — reserved keyword aliasing, nested path dot notation, complex boolean logic, multi-clause updates, alias scope collisions across expression types.

#### ProjectionIntegrationTests

```csharp
[Trait("Category", "Integration")]
public class ProjectionIntegrationTests
{
    // Reserved keyword attributes are aliased and DynamoDB returns only projected fields
    [Fact] ReservedKeywordProjection_ReturnsOnlyProjectedAttributes()

    // Nested path "Address.City" is accepted and returns the nested value
    [Fact] NestedPropertyProjection_ReturnsDottedPath()

    // Projection + filter combined on same request with merged alias maps
    [Fact] ProjectionWithFilter_MergedAliases_Accepted()

    // GetItemRequest with projection returns only specified attributes
    [Fact] GetItemRequest_WithProjection_ReturnsProjectedAttributes()

    // BatchGetItemRequest with per-table projections
    [Fact] BatchGetItemRequest_WithProjection_ReturnsProjectedAttributes()
}
```

#### FilterIntegrationTests

```csharp
[Trait("Category", "Integration")]
public class FilterIntegrationTests
{
    // Complex boolean: (A AND B) OR (NOT C) — verifies parenthesisation
    [Fact] ComplexBooleanFilter_CorrectParentheses_ReturnsMatchingItems()

    // begins_with, contains, BETWEEN — DynamoDB function syntax
    [Fact] StringFunctions_AcceptedByDynamoDB()

    // attribute_not_exists for null checks
    [Fact] NullCheck_GeneratesAttributeNotExists_FiltersCorrectly()

    // IN operator with multiple values
    [Fact] InOperator_FiltersToMatchingValues()

    // Composed filters — verifies re-aliased expressions are accepted by DynamoDB
    [Fact] ComposedAndFilter_ReAliasedExpression_ReturnsMatchingItems()

    // Enum value filter round-trips correctly
    [Fact] EnumFilter_MatchesStoredEnumValue()

    // Nullable attribute filter
    [Fact] NullableAttribute_FilterOnExistence_FiltersCorrectly()
}
```

#### KeyConditionIntegrationTests (Spec 13)

```csharp
[Trait("Category", "Integration")]
public class KeyConditionIntegrationTests
{
    // Partition key only query returns matching items
    [Fact] PartitionKeyOnly_ReturnsMatchingItems()

    // Partition key + sort key equality returns single item
    [Fact] SortKeyEquals_ReturnsSingleItem()

    // Partition key + sort key begins_with returns prefix-matched items
    [Fact] SortKeyBeginsWith_ReturnsMatchingItems()

    // Partition key + sort key between returns range of items
    [Fact] SortKeyBetween_ReturnsItemsInRange()

    // Partition key + sort key comparison (>, <) returns ordered subset
    [Fact] SortKeyGreaterThan_ReturnsItemsAfterValue()

    // Reserved keyword key attributes are aliased and accepted
    [Fact] ReservedKeywordKeyAttribute_AliasedAndAccepted()
}
```

#### UpdateIntegrationTests

```csharp
[Trait("Category", "Integration")]
public class UpdateIntegrationTests
{
    // SET + REMOVE + ADD combined in one UpdateExpression
    [Fact] MultiClauseUpdate_AllClausesApplied()

    // SET with if_not_exists function
    [Fact] SetIfNotExists_PreservesExistingValue()

    // list_append function
    [Fact] AppendToList_AppendsElements()

    // ADD clause on numeric attribute
    [Fact] AddToNumber_IncrementsValue()

    // DELETE clause removes elements from string set
    [Fact] DeleteFromSet_RemovesElements()
}
```

#### ConditionIntegrationTests

```csharp
[Trait("Category", "Integration")]
public class ConditionIntegrationTests
{
    // ConditionExpression prevents write when condition fails
    [Fact] ConditionFails_ThrowsConditionalCheckFailedException()

    // ConditionExpression allows write when condition passes
    [Fact] ConditionPasses_WriteSucceeds()

    // Condition on DeleteItemRequest prevents deletion
    [Fact] DeleteWithCondition_ConditionFails_ItemNotDeleted()
}
```

#### DirectResultMapperIntegrationTests (Spec 04)

```csharp
[Trait("Category", "Integration")]
public class DirectResultMapperIntegrationTests
{
    // Projection + direct mapping pipeline: write item → query with projection → map to DTO
    [Fact] ProjectAndMap_AnonymousType_MapsFromRealResponse()

    // Projection + direct mapping for named DTO type
    [Fact] ProjectAndMap_NamedType_MapsFromRealResponse()

    // Nested attribute projected and mapped
    [Fact] ProjectAndMap_NestedAttribute_MapsFromRealResponse()

    // Enum attribute round-trips through projection and mapping
    [Fact] ProjectAndMap_EnumAttribute_RoundTripsCorrectly()

    // Nullable attribute projected when present and absent
    [Fact] ProjectAndMap_NullableAttribute_HandlesPresenceAndAbsence()
}
```

#### CombinedExpressionIntegrationTests (Spec 10)

```csharp
[Trait("Category", "Integration")]
public class CombinedExpressionIntegrationTests
{
    // KeyCondition + Projection + Filter on same QueryRequest — all alias scopes coexist
    [Fact] KeyCondition_Projection_Filter_AllScopesCoexist()

    // Update + Condition on same UpdateItemRequest — #upd_ and #cond_ coexist
    [Fact] Update_WithCondition_BothApplied()

    // Full fluent chain: key condition → projection → filter → executes correctly
    [Fact] FullFluentChain_QueryRequest_ReturnsExpectedResults()
}
```

## Test Fixtures

```csharp
/// <summary>
/// Reusable test entity covering all built-in attribute types and edge cases.
/// </summary>
public class TestEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }                    // Reserved keyword
    public int Count { get; set; }
    public long LargeCount { get; set; }
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public decimal Total { get; set; }
    public double Ratio { get; set; }
    public byte[] Payload { get; set; }
    public int? OptionalScore { get; set; }             // Nullable value type
    public DateTime? ExpiresOn { get; set; }            // Nullable DateTime
    public List<string> Tags { get; set; }
    public List<int> Scores { get; set; }
    public HashSet<string> Categories { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public TestStatus Status { get; set; }              // Reserved keyword + enum

    [DynamoDbIgnore]
    public bool IsActive => Enabled && Status == TestStatus.Active;

    [DynamoDbAttribute("cust_id")]
    public Guid CustomerId { get; set; }

    [DynamoDbConverter(typeof(MoneyConverter))]
    public Money Price { get; set; }                    // Per-property custom converter

    public TestAddress Address { get; set; }            // Nested object
    public TestContact Contact { get; set; }            // Nested object (3-level depth)
}

public class TestAddress
{
    public string City { get; set; }
    public string PostCode { get; set; }
    public int Floor { get; set; }                      // Nested non-string leaf
}

public class TestContact
{
    public string Phone { get; set; }
    public TestAddress MailingAddress { get; set; }      // 3-level nesting
}

public enum TestStatus { Active, Inactive, Suspended }

public record Money(decimal Amount, string Currency);
```

### TestKeyedEntity (for KeyCondition tests)

```csharp
/// <summary>
/// Entity with explicit partition key and sort key for KeyConditionExpressionBuilder tests.
/// </summary>
public class TestKeyedEntity
{
    public string PK { get; set; }                      // Partition key
    public string SK { get; set; }                      // Sort key
    public string Data { get; set; }
    public TestStatus Status { get; set; }              // Reserved keyword

    [DynamoDbIgnore]
    public bool IsRecent => SK?.StartsWith("2024") == true;
}
```

### AwsSdkAnnotatedEntity (for Spec 01 §7 interop tests)

```csharp
using Amazon.DynamoDBv2.DataModel;

/// <summary>
/// Entity annotated with AWS SDK attributes for interop testing.
/// </summary>
public class AwsSdkAnnotatedEntity
{
    public Guid Id { get; set; }

    [DynamoDBProperty("display_name")]
    public string DisplayName { get; set; }

    [DynamoDBIgnore]
    public string Computed { get; set; }
}

/// <summary>
/// Entity with both library and AWS SDK annotations for priority testing.
/// </summary>
public class DualAnnotatedEntity
{
    public Guid Id { get; set; }

    [DynamoDbAttribute("lib_name")]
    [DynamoDBProperty("sdk_name")]
    public string Name { get; set; }                    // Library annotation should win
}
```

### AttributeValue Fixtures

Helper methods for building `Dictionary<string, AttributeValue>` in tests:

```csharp
public static class AttributeValueFixtures
{
    public static Dictionary<string, AttributeValue> CreateTestEntityItem(
        Guid? id = null,
        string name = "Test",
        int count = 0,
        long largeCount = 0,
        bool enabled = true,
        decimal total = 0m,
        double ratio = 0.0,
        TestStatus status = TestStatus.Active,
        int? optionalScore = null,
        DateTime? expiresOn = null,
        byte[] payload = null)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["Id"] = new() { S = (id ?? Guid.NewGuid()).ToString() },
            ["Name"] = new() { S = name },
            ["Count"] = new() { N = count.ToString() },
            ["LargeCount"] = new() { N = largeCount.ToString() },
            ["Enabled"] = new() { BOOL = enabled },
            ["Total"] = new() { N = total.ToString() },
            ["Ratio"] = new() { N = ratio.ToString() },
            ["Status"] = new() { S = status.ToString() },
        };

        if (optionalScore.HasValue)
            item["OptionalScore"] = new() { N = optionalScore.Value.ToString() };

        if (expiresOn.HasValue)
            item["ExpiresOn"] = new() { S = expiresOn.Value.ToString("O") };

        if (payload != null)
            item["Payload"] = new() { B = new MemoryStream(payload) };

        return item;
    }

    public static Dictionary<string, AttributeValue> CreateNestedEntityItem(
        Guid? id = null,
        string city = "London",
        string postCode = "SW1A 1AA",
        int floor = 0)
    {
        var item = CreateTestEntityItem(id: id);
        item["Address"] = new()
        {
            M = new Dictionary<string, AttributeValue>
            {
                ["City"] = new() { S = city },
                ["PostCode"] = new() { S = postCode },
                ["Floor"] = new() { N = floor.ToString() }
            }
        };
        return item;
    }

    public static Dictionary<string, AttributeValue> CreateKeyedEntityItem(
        string pk,
        string sk,
        string data = "test",
        TestStatus status = TestStatus.Active)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = pk },
            ["SK"] = new() { S = sk },
            ["Data"] = new() { S = data },
            ["Status"] = new() { S = status.ToString() }
        };
    }
}
```

## Coverage Targets

- **Line coverage**: 90%+
- **Branch coverage**: 85%+
- **Critical paths**: 100% (expression visitor, all expression builders, direct mapping, type conversion, request extensions)
- **Edge cases**: Null handling, missing attributes, type mismatches, reserved keywords, alias scope collisions, cross-type nested path resolution
