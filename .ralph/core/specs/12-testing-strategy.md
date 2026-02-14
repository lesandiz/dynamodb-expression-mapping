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

- **NUnit** for test framework
- **FluentAssertions** for assertions
- **NSubstitute** for mocking interfaces
- **Bogus** for test data generation
- **Testcontainers.DynamoDb** for integration tests (manages DynamoDB Local container lifecycle automatically)

## Unit Test Coverage

### ProjectionExpressionVisitor (Spec 02)

```csharp
// Property extraction tests
[Test] SingleProperty_ExtractsOnePath()
[Test] AnonymousType_ExtractsAllProperties()
[Test] ObjectInitialiser_ExtractsSourceProperties()
[Test] NestedProperty_ExtractsFullPath()
[Test] IdentityExpression_ReturnsEmptyPaths()
[Test] ValueTuple_ExtractsAllProperties()
[Test] DuplicateProperty_Deduplicated()
[Test] IntermediateNode_NotAddedAsSeparatePath()
[Test] MultipleNestedPaths_InSameAnonymousType_ExtractsAll()
[Test] DeeplyNestedPath_ThreeLevels_ExtractsFullPath()

// Unsupported expressions (Spec 14 §2)
[Test] MethodCall_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()
[Test] Arithmetic_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()
[Test] Conditional_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()
[Test] ArrayIndex_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()
[Test] StringConcatenation_ThrowsUnsupportedExpressionException_WithNodeTypeAndText()

// PropertyPath metadata — SegmentProperties (Spec 02 §2, §7)
[Test] SegmentProperties_SingleProperty_ContainsOneEntry()
[Test] SegmentProperties_NestedPath_ContainsEntryPerSegment()
[Test] SegmentProperties_NestedPath_IntermediateDeclaringType_IsCorrect()
[Test] SegmentProperties_NestedPath_IntermediatePropertyType_MatchesNextDeclaringType()
[Test] SegmentProperties_DeeplyNested_ThreeLevels_ContainsThreeEntries()
[Test] PropertyInfo_IsLastSegmentProperty()
[Test] ProjectionShape_Identity_ForWholeObject()
[Test] ProjectionShape_SingleProperty_ForOneProperty()
[Test] ProjectionShape_Composite_ForAnonymousType()
[Test] ProjectionShape_Composite_ForObjectInitialiser()
```

### ProjectionBuilder (Spec 03)

```csharp
// Expression building
[Test] SimpleProperties_CommaSeparated()
[Test] ReservedKeyword_Aliased()
[Test] NestedPath_PreservesDots()
[Test] MixedReservedAndNonReserved_CorrectAliasing()
[Test] EmptyProjection_ReturnsEmptyResult()
[Test] SpecialCharacters_Aliased()
[Test] NestedPath_ReservedSegment_OnlyReservedSegmentAliased()

// Attribute name resolution
[Test] DynamoDbAttribute_UsesRemappedName()
[Test] DynamoDbIgnore_StrictMode_ThrowsInvalidProjectionException_WithPropertyAndType()
[Test] DynamoDbIgnore_LenientMode_Excludes()
[Test] PassThroughResolver_UsesPropertyNameAsIs()
[Test] FluentOverride_TakesPrecedence()
[Test] NestedPath_CrossTypeResolution_UsesFactoryPerSegment()
[Test] NestedPath_RemappedOnBothTypes_ResolvesCorrectly()

// Result properties
[Test] ProjectionResult_IsEmpty_TrueForIdentity()
[Test] ProjectionResult_Shape_MatchesExpressionPattern()
[Test] ProjectionResult_ResolvedAttributeNames_ContainsResolvedNames()

// Alias scoping
[Test] ProjectionAliases_UseProjPrefix()
[Test] NoCollisionWithFilterAliases()

// Caching
[Test] SameExpression_ReturnsCachedProjectionResult()

// Validation
[Test] NullSelector_ThrowsArgumentNullException()
```

### AttributeNameResolver (Spec 01)

```csharp
// Resolution
[Test] PropertyWithNoAnnotation_ReturnsPropertyNameAsIs()
[Test] DynamoDbAttribute_ReturnsRemappedName()
[Test] DynamoDbIgnore_StrictMode_GetAttributeName_ThrowsInvalidProjectionException()
[Test] DynamoDbIgnore_LenientMode_IsStoredAttribute_ReturnsFalse()
[Test] IsStoredAttribute_ReturnsTrueForStoredProperty()
[Test] IsStoredAttribute_ReturnsFalseForIgnoredProperty()
[Test] GetPropertyName_ReturnsReverseMapping()
[Test] GetPropertyName_RemappedAttribute_ReturnsOriginalPropertyName()

// AWS SDK interop (Spec 01 §7)
[Test] AwsSdkDynamoDBProperty_ReturnsRemappedName()
[Test] AwsSdkDynamoDBIgnore_IsStoredAttribute_ReturnsFalse()

// Resolution order (Spec 01 §5)
[Test] FluentOverride_TakesPrecedenceOver_DynamoDbAttribute()
[Test] DynamoDbAttribute_TakesPrecedenceOver_DynamoDBProperty()
[Test] DynamoDBProperty_TakesPrecedenceOver_ConventionName()
[Test] BothAnnotationsPresent_LibraryAnnotationWins()

// Fluent builder (Spec 01 §6)
[Test] FluentMap_OverridesPropertyName()
[Test] FluentIgnore_MarksPropertyAsNotStored()

// Per-property converter annotation
[Test] DynamoDbConverterAttribute_DetectedOnProperty()

// Caching (Spec 01 §8)
[Test] SameType_ReturnsCachedMetadata()

// Validation
[Test] DynamoDbAttribute_EmptyName_ThrowsArgumentException()
[Test] DynamoDbConverterAttribute_NullType_ThrowsArgumentNullException()
```

### AttributeNameResolverFactory (Spec 01 §10–§13)

```csharp
// Factory creation
[Test] GetResolver_CreatesResolverForArbitraryType()
[Test] GetResolver_CachesResolverPerType()
[Test] GetResolver_SameType_ReturnsSameInstance()
[Test] GetResolverGeneric_ReturnsTypedResolver()

// Factory registration
[Test] Register_OverridesAutoDiscoveredResolver()

// Cross-type nested path resolution (Spec 01 §13)
[Test] NestedPath_ResolvesEachSegmentAgainstCorrectType()
[Test] NestedPath_RemappedChildType_ResolvesCorrectly()

// Factory builder (Spec 01 §12)
[Test] FactoryBuilder_ConfiguresMultipleTypes()
[Test] FactoryBuilder_UnconfiguredType_FallsBackToAutoDiscovery()
[Test] FactoryBuilder_WithMode_AppliesModeToAllResolvers()
```

### AttributeValueConverters (Spec 05)

```csharp
// Round-trip per built-in converter type
[Test] String_RoundTrip_PreservesValue()
[Test] Guid_RoundTrip_PreservesValue()
[Test] Bool_RoundTrip_PreservesValue()
[Test] Int_RoundTrip_PreservesValue()
[Test] Long_RoundTrip_PreservesValue()
[Test] Decimal_RoundTrip_PreservesValue()
[Test] Double_RoundTrip_PreservesValue()
[Test] DateTime_RoundTrip_PreservesValue()
[Test] DateTimeOffset_RoundTrip_PreservesValue()
[Test] ByteArray_RoundTrip_PreservesValue()
[Test] ListOfString_RoundTrip_PreservesValue()
[Test] ListOfInt_RoundTrip_PreservesValue()
[Test] HashSetOfString_RoundTrip_PreservesValue()
[Test] DictionaryOfStringString_RoundTrip_PreservesValue()

// Null / missing handling
[Test] String_FromNull_ReturnsNull()
[Test] Guid_FromNull_ReturnsGuidEmpty()
[Test] Bool_FromMissing_ReturnsFalse()
[Test] Int_FromNull_ReturnsZero()
[Test] DateTime_FromNull_ReturnsMinValue()
[Test] ByteArray_FromNull_ReturnsNull()
[Test] ListOfString_FromMissing_ReturnsEmptyList()
[Test] HashSetOfString_FromMissing_ReturnsEmptySet()

// Nullable wrapper (Spec 05 §4)
[Test] NullableInt_FromNull_ReturnsNull()
[Test] NullableInt_FromValue_ReturnsValue()
[Test] NullableGuid_FromNull_ReturnsNull()
[Test] NullableDateTime_FromValue_ReturnsValue()
[Test] NullableInt_ToAttributeValue_NullWritesNULL()
[Test] NullableInt_ToAttributeValue_ValueWritesN()

// Enum converter (Spec 05 §5)
[Test] Enum_StringMode_RoundTrip_PreservesValue()
[Test] Enum_NumberMode_RoundTrip_PreservesValue()
[Test] Enum_StringMode_ParsesIgnoreCase()

// DateTime format
[Test] DateTime_ToAttributeValue_WritesIso8601RoundtripFormat()
[Test] DateTimeOffset_ToAttributeValue_WritesIso8601RoundtripFormat()
```

### ConverterRegistry (Spec 05 §2)

```csharp
// Default registry
[Test] Default_ContainsAllBuiltInConverters()
[Test] Default_IsFrozen_RegisterThrowsInvalidOperationException()
[Test] HasConverter_ReturnsTrueForRegisteredType()
[Test] HasConverter_ReturnsFalseForUnregisteredType()

// Clone (Spec 05 §2)
[Test] Clone_CreatesMutableCopy()
[Test] Clone_MutationsDoNotAffectSource()

// Custom registration
[Test] Register_CustomConverter_OverridesExisting()
[Test] Register_CustomConverter_AvailableViaGetConverter()

// Resolution order (Spec 05 §8)
[Test] GetConverter_NullableType_WrapsInnerConverter()
[Test] GetConverter_EnumType_ReturnsEnumConverter()
[Test] GetConverter_UnregisteredType_ThrowsMissingConverterException()
[Test] MissingConverterException_CarriesTargetType()

// Open-generic collection resolution (Spec 05 §8a)
[Test] GetConverter_ListOfEnum_ComposesListConverterWithEnumConverter()
[Test] GetConverter_ListOfGuid_ComposesListConverterWithGuidConverter()
[Test] GetConverter_HashSetOfGuid_ComposesSetConverterWithGuidConverter()
[Test] GetConverter_HashSetOfInt_UsesNativeNS()
[Test] GetConverter_HashSetOfEnum_UsesSS_WhenStringMode()
[Test] GetConverter_DictionaryStringMoney_ComposesMapConverterWithCustomConverter()
[Test] GetConverter_DictionaryIntString_ThrowsMissingConverter_NonStringKey()
[Test] GetConverter_NestedList_ListOfListOfString_ComposesRecursively()
[Test] GetConverter_ListOfCustomType_WithRegisteredConverter_Composes()
[Test] GetConverter_ListOfUnregisteredType_ThrowsMissingConverterException()
[Test] GenericCollectionConverter_CachedAfterFirstResolution()
[Test] ExactTypeRegistration_TakesPrecedenceOverGenericResolution()

// Per-property override (Spec 05 §7)
[Test] DynamoDbConverterAttribute_TakesPrecedenceOverRegistry()

// Null handling mode (Spec 05 §10)
[Test] OmitNull_NullStringNotWritten()
[Test] ExplicitNull_NullStringWritesNULLAttribute()
```

### ExpressionValueEmitter (Spec 05 §11)

```csharp
// Converter resolution — delegates to Spec 05 §8 resolution order
[Test] Emit_WithDynamoDbConverterAttribute_UsesAttributeConverter()
[Test] Emit_WithRegisteredConverter_UsesRegistryConverter()
[Test] Emit_NullableValue_WrapsInNullableConverter()
[Test] Emit_EnumValue_UsesEnumConverter()
[Test] Emit_CollectionValue_UsesGenericCollectionConverter()
[Test] Emit_UnregisteredType_ThrowsMissingConverterException()

// PropertyInfo null handling
[Test] Emit_NullPropertyInfo_ResolvesViaRuntimeType()
[Test] Emit_NullPropertyInfo_EnumValue_UsesEnumConverter()

// Integration with expression builders
[Test] Emit_GuidValue_ProducesStringAttributeValue()
[Test] Emit_DecimalValue_ProducesNumberAttributeValue()
[Test] Emit_DateTimeValue_ProducesIso8601StringAttributeValue()
[Test] Emit_BoolValue_ProducesBoolAttributeValue()
[Test] Emit_CustomTypeWithConverter_ProducesMapAttributeValue()

// Thread safety
[Test] Emit_ConcurrentCalls_NoRaceConditions()
```

### FilterExpressionBuilder (Spec 06)

```csharp
// Comparison operators
[Test] Equality_GeneratesEqualsExpression()
[Test] Inequality_GeneratesNotEqualsExpression()
[Test] GreaterThan_GeneratesCorrectExpression()
[Test] LessThan_GeneratesCorrectExpression()
[Test] GreaterThanOrEqual_GeneratesCorrectExpression()
[Test] LessThanOrEqual_GeneratesCorrectExpression()

// Logical operators
[Test] And_CombinesWithAND()
[Test] Or_CombinesWithOR()
[Test] Not_WrapsWithNOT()
[Test] ComplexPredicate_CorrectParentheses()

// Boolean properties (Spec 06 §3)
[Test] BooleanPropertyDirect_GeneratesBoolEqualsTrue()
[Test] NegatedBooleanProperty_GeneratesBoolEqualsFalse()

// String operations
[Test] StartsWith_GeneratesBeginsWith()
[Test] Contains_GeneratesContains()

// Null checks
[Test] EqualsNull_GeneratesAttributeNotExists()
[Test] NotEqualsNull_GeneratesAttributeExists()

// DynamoDB functions (Spec 06 §4)
[Test] Between_GeneratesBETWEEN()
[Test] Size_GeneratesSize()
[Test] DynamoDbFunctions_AttributeExists_GeneratesAttributeExists()
[Test] DynamoDbFunctions_AttributeNotExists_GeneratesAttributeNotExists()
[Test] DynamoDbFunctions_AttributeType_GeneratesAttributeType()
[Test] DynamoDbFunctions_CalledAtRuntime_ThrowsInvalidOperationException()

// Captured variables
[Test] CapturedVariable_EvaluatedAtBuildTime()
[Test] CapturedEnumValue_ConvertedToAttributeValue()

// Value conversion
[Test] GuidValue_ConvertedToStringAttributeValue()
[Test] BoolValue_ConvertedToBoolAttributeValue()
[Test] DateTimeValue_ConvertedToIso8601String()
[Test] EnumValue_ConvertedPerStorageMode()

// IN operator
[Test] ContainsOnArray_GeneratesINExpression()

// Nested property (Spec 06 §8)
[Test] NestedProperty_ResolvesViaFactory_GeneratesDotNotation()
[Test] NestedProperty_RemappedAttribute_UsesResolvedName()

// Attribute name resolution
[Test] RemappedAttribute_UsesResolvedNameInExpression()
[Test] ReservedKeyword_AliasedWithFiltPrefix()

// Alias scoping (Spec 06 §5)
[Test] FilterAliases_UseFiltPrefix()
[Test] FilterValueAliases_UseFiltVPrefix()

// Validation (Spec 06 §9)
[Test] NullPredicate_ThrowsArgumentNullException()
[Test] DynamoDbIgnore_StrictMode_ThrowsInvalidFilterException_WithPropertyAndType()
[Test] NonBooleanExpression_ThrowsInvalidFilterException()
```

### FilterExpressionResultComposability (Spec 06 §6)

```csharp
// And composability
[Test] And_TwoFilters_ReAliasesRightOperand()
[Test] And_LeftEmpty_ReturnsRight()
[Test] And_RightEmpty_ReturnsLeft()
[Test] And_BothEmpty_ReturnsEmpty()
[Test] And_NullOperand_ThrowsArgumentNullException()
[Test] And_Chained_ThreeFilters_ContiguousIndices()
[Test] And_BothFiltersUseNameAliases_IndicesDisjoint()
[Test] And_HighIndexAliases_NoPartialMatchCorruption()

// Or composability
[Test] Or_TwoFilters_ReAliasesRightOperand()
[Test] Or_MergedNames_NoCollision()
[Test] Or_MergedValues_NoCollision()
```

### ConditionExpressionBuilder (Spec 06 §10)

```csharp
// Core expression building — same patterns as filter, different result type
[Test] Equality_GeneratesEqualsExpression()
[Test] And_CombinesWithAND()
[Test] NullCheck_GeneratesAttributeNotExists()
[Test] Between_GeneratesBETWEEN()
[Test] CapturedVariable_EvaluatedAtBuildTime()

// Alias scoping — must use #cond_ / :cond_v prefixes
[Test] ConditionAliases_UseCondPrefix()
[Test] ConditionValueAliases_UseCondVPrefix()

// Result type
[Test] BuildCondition_ReturnsConditionExpressionResult_NotFilterResult()

// Validation
[Test] NullPredicate_ThrowsArgumentNullException()
[Test] DynamoDbIgnore_StrictMode_ThrowsInvalidFilterException()
```

### ConditionExpressionResultComposability (Spec 06 §6.7)

```csharp
// Composability with #cond_ re-aliasing
[Test] And_TwoConditions_ReAliasesWithCondPrefix()
[Test] Or_TwoConditions_ReAliasesWithCondPrefix()
[Test] And_LeftEmpty_ReturnsRight()
[Test] And_NullOperand_ThrowsArgumentNullException()
```

### KeyConditionExpressionBuilder (Spec 13)

```csharp
// Partition key only (Spec 13 §4)
[Test] PartitionKeyOnly_GeneratesEqualityExpression()

// Partition key + sort key operators (Spec 13 §2)
[Test] SortKeyEquals_GeneratesEquality()
[Test] SortKeyLessThan_GeneratesLessThan()
[Test] SortKeyLessThanOrEqual_GeneratesLessThanOrEqual()
[Test] SortKeyGreaterThan_GeneratesGreaterThan()
[Test] SortKeyGreaterThanOrEqual_GeneratesGreaterThanOrEqual()
[Test] SortKeyBetween_GeneratesBETWEEN()
[Test] SortKeyBeginsWith_GeneratesBeginsWithFunction()

// Alias scoping (Spec 13 §7)
[Test] KeyConditionAliases_UseKeyPrefix()
[Test] KeyConditionValueAliases_UseKeyVPrefix()

// Smart aliasing (Spec 13 §9)
[Test] NonReservedAttribute_UsedDirectly_NotAliased()
[Test] ReservedAttribute_Aliased()

// Property resolution (Spec 13 §5)
[Test] RemappedAttribute_UsesResolvedName()

// Value conversion (Spec 13 §6)
[Test] StringValue_ConvertedToStringAttributeValue()
[Test] GuidValue_ConvertedToStringAttributeValue()

// Result type
[Test] Build_ReturnsKeyConditionExpressionResult()

// Validation (Spec 13 §8)
[Test] NullPropertyExpression_ThrowsArgumentNullException()
[Test] DynamoDbIgnore_ThrowsInvalidKeyConditionException_WithPropertyAndType()
[Test] NestedProperty_ThrowsInvalidKeyConditionException_WithPropertyName()
[Test] NullPartitionKeyValue_ThrowsArgumentNullException()
[Test] Between_LowGreaterThanHigh_ThrowsArgumentException()
[Test] BeginsWith_NullPrefix_ThrowsArgumentException()
[Test] BeginsWith_EmptyPrefix_ThrowsArgumentException()

// Thread safety (Spec 13 §11)
[Test] ConcurrentWithPartitionKey_ProducesIndependentBuilders()
```

### UpdateExpressionBuilder (Spec 07)

```csharp
// Clause generation
[Test] Set_GeneratesSETClause()
[Test] Increment_GeneratesAddExpression()
[Test] Decrement_GeneratesSubtractExpression()
[Test] SetIfNotExists_GeneratesIfNotExistsFunction()
[Test] AppendToList_GeneratesListAppendFunction()
[Test] Remove_GeneratesREMOVEClause()
[Test] Add_GeneratesADDClause()
[Test] Delete_GeneratesDELETEClause()
[Test] MultipleClauses_CombinedCorrectly()
[Test] NoOperations_ReturnsEmpty()
[Test] DuplicateProperty_LastWins()

// Alias scoping (Spec 07 §5)
[Test] UpdateAliases_UseUpdPrefix()
[Test] UpdateValueAliases_UseUpdVPrefix()
[Test] ReservedKeyword_AliasedInUpdateExpression()

// Property resolution (Spec 07 §5)
[Test] RemappedAttribute_UsesResolvedName()
[Test] NestedProperty_ResolvesCrossType()

// Value conversion (Spec 07 §6)
[Test] EnumValue_ConvertedViaRegistry()

// Validation (Spec 07 §7)
[Test] ConflictingClauses_ThrowsInvalidUpdateException_WithPropertyName()
[Test] DynamoDbIgnore_ThrowsInvalidUpdateException_WithPropertyAndType()
[Test] NullPropertyExpression_ThrowsArgumentNullException()
```

### ReservedKeywordRegistry (Spec 08)

```csharp
// Detection (Spec 08 §1)
[Test] KnownReservedWord_IsReserved_ReturnsTrue()
[Test] NonReservedWord_IsReserved_ReturnsFalse()
[Test] IsReserved_CaseInsensitive()
[Test] EmptyString_IsReserved_ReturnsFalse()
[Test] NullString_IsReserved_ReturnsFalse()

// Common reserved words that overlap with typical attribute names
[Test] Status_IsReserved()
[Test] Name_IsReserved()
[Test] Date_IsReserved()
[Test] Comment_IsReserved()
[Test] Value_IsReserved()

// NeedsEscaping (Spec 08 §1)
[Test] ReservedWord_NeedsEscaping_ReturnsTrue()
[Test] SpecialCharacters_NeedsEscaping_ReturnsTrue()
[Test] UnderscoreOnly_NeedsEscaping_ReturnsFalse()
[Test] AlphanumericOnly_NeedsEscaping_ReturnsFalse()
```

### AliasGenerator (Spec 08 §2)

```csharp
// Sequential generation
[Test] NextName_GeneratesSequentialAliases()
[Test] NextValue_GeneratesSequentialAliases()
[Test] Reset_ResetsCountersToZero()

// Scoped prefixes (Spec 08 §3)
[Test] ProjScope_GeneratesHashProjPrefix()
[Test] FiltScope_GeneratesHashFiltPrefix()
[Test] CondScope_GeneratesHashCondPrefix()
[Test] UpdScope_GeneratesHashUpdPrefix()
[Test] KeyScope_GeneratesHashKeyPrefix()
[Test] FiltScope_ValuePrefix_GeneratesColonFiltV()
[Test] CondScope_ValuePrefix_GeneratesColonCondV()
[Test] UpdScope_ValuePrefix_GeneratesColonUpdV()
[Test] KeyScope_ValuePrefix_GeneratesColonKeyV()

// No collision across scopes (Spec 08 §5)
[Test] DifferentScopes_ProduceDifferentPrefixes()
```

### ExpressionCache (Spec 09)

```csharp
[Test] SameExpression_ReturnsCachedResult()
[Test] DifferentExpression_ReturnsDifferentResult()
[Test] StructurallyIdentical_SameKey()
[Test] NoCache_AlwaysBuilds()
[Test] ThreadSafety_ConcurrentAccess()

// Statistics (Spec 09 §6)
[Test] GetStatistics_ReportsHitsAndMisses()
[Test] GetStatistics_ReportsTotalEntries()

// Default instance (Spec 09 §1)
[Test] Default_IsSingletonInstance()
```

### ExpressionKeyGenerator (Spec 09 §2, §9)

```csharp
// Key correctness (Spec 09 §9)
[Test] SingleProperty_DifferentFromWrappedInAnonymousType()
[Test] AnonymousType_DifferentMemberName_DifferentKey()
[Test] AnonymousType_VsNamedType_DifferentKey()
[Test] StructurallyIdentical_DifferentCallSites_SameKey()

// Key structure
[Test] Key_IncludesSourceTypeName()
[Test] Key_IncludesResultTypeName()

// Filter caching behaviour (Spec 09 §3)
[Test] FilterExpressions_NotCachedByDefault()
```

### DirectResultMapper\<TSource\> (Spec 04)

```csharp
// Mapping strategies (Spec 04 §2)
[Test] SingleProperty_MapsDirectly()
[Test] AnonymousType_ConstructsViaConstructor()
[Test] NamedType_ConstructsViaPropertySetters()
[Test] Record_ConstructsViaConstructor()
[Test] ParameterisedConstructor_ConstructsViaConstructorArgs()
[Test] Identity_DelegatesToFallback()

// Type conversion — all built-in types
[Test] StringAttribute_MapsToString()
[Test] GuidAttribute_MapsToGuid()
[Test] BoolAttribute_MapsToBool()
[Test] NumberAttribute_MapsToInt()
[Test] NumberAttribute_MapsToLong()
[Test] NumberAttribute_MapsToDecimal()
[Test] NumberAttribute_MapsToDouble()
[Test] DateTimeAttribute_MapsToDateTime()
[Test] DateTimeOffsetAttribute_MapsToDateTimeOffset()
[Test] ByteArrayAttribute_MapsToBytesArray()
[Test] ListAttribute_MapsToListOfString()
[Test] HashSetAttribute_MapsToHashSetOfString()
[Test] DictionaryAttribute_MapsToDictionaryOfStringString()
[Test] NullableAttribute_MapsToNullable()
[Test] EnumAttribute_MapsToEnum()

// Missing / null attributes (Spec 04 §10)
[Test] MissingAttribute_ReturnsDefault()
[Test] NullAttribute_ReturnsDefault()
[Test] WrongDynamoDbType_ReturnsDefault()

// Nested attributes (Spec 04 §6)
[Test] NestedMapAttribute_MapsCorrectly()
[Test] NavigateToLeaf_MissingIntermediate_ReturnsDefault()
[Test] NavigateToLeaf_IntermediateNotMap_ReturnsDefault()
[Test] NestedPath_CustomConverterOnLeaf_UsesConverter()

// One-shot Map method (Spec 04 §1)
[Test] Map_OneShot_ReturnsMappedResult()
[Test] Map_OneShot_UsesCachedMapperInternally()

// Performance
[Test] CreateMapper_ReturnsReusableDelegate()
[Test] CachedMapper_ReturnsSameDelegate()

// Custom converters (Spec 04 §4)
[Test] DynamoDbConverterAttribute_UsesCustomConverter()
[Test] RegisteredConverter_UsedForType()

// Validation (Spec 04 §10)
[Test] NoConverterForType_ThrowsMissingConverterException_AtCreationTime()
[Test] UnsupportedExpressionShape_ThrowsUnsupportedExpressionException_AtCreationTime()
```

### ProjectionExtensions (Spec 10 §1)

```csharp
// GetItemRequest
[Test] GetItemRequest_WithProjection_SetsProjectionExpression()
[Test] GetItemRequest_WithProjection_MergesAttributeNames()

// QueryRequest
[Test] QueryRequest_WithProjection_SetsProjectionExpression()
[Test] QueryRequest_WithProjection_NullSelector_NoOp()

// ScanRequest
[Test] ScanRequest_WithProjection_SetsProjectionExpression()
[Test] ScanRequest_WithProjection_NullSelector_NoOp()

// BatchGetItemRequest (Spec 10 §1)
[Test] BatchGetItemRequest_WithProjection_SetsProjectionOnTable()
[Test] BatchGetItemRequest_WithProjection_TableNotFound_ThrowsArgumentException()
[Test] BatchGetItemRequest_WithProjection_NullRequestItems_ThrowsArgumentNullException()

// Null builder
[Test] WithProjection_NullBuilder_ThrowsArgumentNullException()
```

### FilterExtensions (Spec 10 §2)

```csharp
[Test] QueryRequest_WithFilter_SetsFilterExpression()
[Test] QueryRequest_WithFilter_MergesAttributeNamesAndValues()
[Test] ScanRequest_WithFilter_SetsFilterExpression()
[Test] FluentChaining_ProjectionAndFilter()
```

### ConditionExtensions (Spec 10 §3)

```csharp
[Test] PutItemRequest_WithCondition_SetsConditionExpression()
[Test] PutItemRequest_WithCondition_MergesAttributeNamesAndValues()
[Test] DeleteItemRequest_WithCondition_SetsConditionExpression()
[Test] UpdateItemRequest_WithCondition_SetsConditionExpression()
[Test] WithCondition_NullBuilder_ThrowsArgumentNullException()
```

### KeyConditionExtensions (Spec 10 §4)

```csharp
[Test] QueryRequest_WithKeyCondition_SetsKeyConditionExpression()
[Test] QueryRequest_WithKeyCondition_MergesAttributeNamesAndValues()
[Test] WithKeyCondition_NullBuilder_ThrowsArgumentNullException()
[Test] WithKeyCondition_NullConfigure_ThrowsArgumentNullException()
```

### UpdateExtensions (Spec 10 §5)

```csharp
[Test] UpdateItemRequest_WithUpdate_SetsUpdateExpression()
[Test] UpdateItemRequest_WithUpdate_MergesAttributeNamesAndValues()
[Test] WithUpdate_EmptyResult_NoOp()
```

### MergeHelpers (Spec 10 §6)

```csharp
[Test] MergeAttributeNames_DisjointKeys_MergesAll()
[Test] MergeAttributeNames_SameKeyAndValue_NoConflict()
[Test] MergeAttributeNames_SameKeyDifferentValue_ThrowsConflictException()
[Test] MergeAttributeValues_DisjointKeys_MergesAll()
[Test] MergeAttributeValues_DuplicateKey_ThrowsConflictException()
[Test] ConflictException_CarriesAliasKeyAndValues()
```

### CombinedExtensions (Spec 10 §7, §9)

```csharp
[Test] QueryRequest_WithExpressions_AppliesProjectionAndFilter()
[Test] FluentChaining_KeyCondition_Projection_Filter_AllApplied()
[Test] FluentChaining_AllScopes_AliasesDoNotCollide()
```

### Exception Hierarchy (Spec 14)

```csharp
// Hierarchy structure
[Test] AllExceptions_DeriveFromExpressionMappingException()
[Test] InvalidProjectionException_DeriveFromInvalidExpressionException()
[Test] InvalidFilterException_DeriveFromInvalidExpressionException()
[Test] InvalidUpdateException_DeriveFromInvalidExpressionException()
[Test] InvalidKeyConditionException_DeriveFromInvalidExpressionException()

// Structured properties
[Test] UnsupportedExpressionException_CarriesNodeTypeAndText()
[Test] MissingConverterException_CarriesTargetTypeAndPropertyName()
[Test] MissingConverterException_NullPropertyName_WhenDirectRegistryCall()
[Test] ExpressionAttributeConflictException_CarriesAliasKeyAndValues()
[Test] ExpressionAttributeConflictException_NullConflictingValue_WhenValuePlaceholder()
[Test] InvalidProjectionException_CarriesPropertyNameAndEntityType()
[Test] InvalidFilterException_CarriesPropertyNameAndEntityType()
[Test] InvalidUpdateException_CarriesPropertyNameAndEntityType()
[Test] InvalidKeyConditionException_CarriesPropertyNameAndEntityType()

// Message formatting
[Test] UnsupportedExpressionException_MessageContainsNodeTypeAndText()
[Test] MissingConverterException_MessageContainsTypeName()
[Test] InvalidProjectionException_MessageContainsPropertyAndEntityName()

// Inner exception support
[Test] ExpressionMappingException_PreservesInnerException()

// Catch patterns
[Test] CatchExpressionMappingException_CatchesAllLibraryExceptions()
[Test] CatchInvalidExpressionException_CatchesAllBuilderValidationErrors()
[Test] CatchInvalidExpressionException_DoesNotCatchUnsupportedOrMissing()
```

### Configuration and DI (Spec 11)

```csharp
// Default config (Spec 11 §1)
[Test] DefaultConfig_HasExpectedDefaults()
[Test] DefaultConfig_NameResolutionMode_IsStrict()
[Test] DefaultConfig_NullHandlingMode_IsOmitNull()
[Test] DefaultConfig_LoggerFactory_IsNullLoggerFactory()

// Builder (Spec 11 §2)
[Test] Builder_OverridesIndividualSettings()
[Test] Builder_WithNameResolutionMode_AppliesMode()
[Test] Builder_WithNullHandling_AppliesMode()
[Test] Builder_WithCache_AppliesCustomCache()
[Test] Builder_WithLoggerFactory_AppliesFactory()
[Test] Builder_WithConverter_ClonesDefaultRegistryBeforeMutating()
[Test] Builder_WithConverter_DoesNotMutateDefaultRegistry()

// DI registration (Spec 11 §3)
[Test] AddDynamoDbExpressionMapping_RegistersOpenGenericBuilders()
[Test] AddDynamoDbExpressionMapping_RegistersResolverFactory()
[Test] AddDynamoDbExpressionMapping_WithConfigure_AppliesSettings()

// Per-entity configuration (Spec 11 §5)
[Test] AddDynamoDbEntity_OverridesOpenGenericResolver()
[Test] AddDynamoDbEntity_RegistersIntoFactory_ForNestedResolution()
[Test] AddDynamoDbEntity_MultipleEntities_EachConfiguredIndependently()
[Test] OpenGenericResolver_FallsBackToReflection()

// Manual instantiation (Spec 11 §4)
[Test] ManualInstantiation_WorksWithoutDI()
[Test] ManualInstantiation_WithFluentFactoryBuilder()
```

## Integration Tests

Integration tests verify that the expressions produced by the library are accepted by DynamoDB and return the expected results. They are **not** for testing expression tree analysis, type conversion, or mapping logic — those are covered exhaustively by unit tests.

**Scope**: write data → execute operation with generated expression → assert DynamoDB returns what we expect.

### Infrastructure: Testcontainers

```csharp
/// <summary>
/// Shared fixture that starts a DynamoDB Local container once per test run.
/// Implements IAsyncLifetime for NUnit's SetUpFixture pattern.
/// </summary>
[SetUpFixture]
public class DynamoDbFixture
{
    private static DynamoDbContainer _container;
    public static IAmazonDynamoDB Client { get; private set; }

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        _container = new DynamoDbBuilder()
            .WithImage("amazon/dynamodb-local:latest")
            .Build();

        await _container.StartAsync();

        Client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonDynamoDBConfig { ServiceURL = _container.GetConnectionString() });
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        Client?.Dispose();
        await _container.DisposeAsync();
    }
}
```

Each test class creates its own table in `[SetUp]` (unique name per test) and deletes it in `[TearDown]` for isolation.

### Test Cases

Focus: expressions that could be syntactically wrong in ways unit tests can't catch — reserved keyword aliasing, nested path dot notation, complex boolean logic, multi-clause updates, alias scope collisions across expression types.

#### ProjectionIntegrationTests

```csharp
[TestFixture, TestCategory("Integration")]
public class ProjectionIntegrationTests
{
    // Reserved keyword attributes are aliased and DynamoDB returns only projected fields
    [Test] ReservedKeywordProjection_ReturnsOnlyProjectedAttributes()

    // Nested path "Address.City" is accepted and returns the nested value
    [Test] NestedPropertyProjection_ReturnsDottedPath()

    // Projection + filter combined on same request with merged alias maps
    [Test] ProjectionWithFilter_MergedAliases_Accepted()

    // GetItemRequest with projection returns only specified attributes
    [Test] GetItemRequest_WithProjection_ReturnsProjectedAttributes()

    // BatchGetItemRequest with per-table projections
    [Test] BatchGetItemRequest_WithProjection_ReturnsProjectedAttributes()
}
```

#### FilterIntegrationTests

```csharp
[TestFixture, TestCategory("Integration")]
public class FilterIntegrationTests
{
    // Complex boolean: (A AND B) OR (NOT C) — verifies parenthesisation
    [Test] ComplexBooleanFilter_CorrectParentheses_ReturnsMatchingItems()

    // begins_with, contains, BETWEEN — DynamoDB function syntax
    [Test] StringFunctions_AcceptedByDynamoDB()

    // attribute_not_exists for null checks
    [Test] NullCheck_GeneratesAttributeNotExists_FiltersCorrectly()

    // IN operator with multiple values
    [Test] InOperator_FiltersToMatchingValues()

    // Composed filters — verifies re-aliased expressions are accepted by DynamoDB
    [Test] ComposedAndFilter_ReAliasedExpression_ReturnsMatchingItems()

    // Enum value filter round-trips correctly
    [Test] EnumFilter_MatchesStoredEnumValue()

    // Nullable attribute filter
    [Test] NullableAttribute_FilterOnExistence_FiltersCorrectly()
}
```

#### KeyConditionIntegrationTests (Spec 13)

```csharp
[TestFixture, TestCategory("Integration")]
public class KeyConditionIntegrationTests
{
    // Partition key only query returns matching items
    [Test] PartitionKeyOnly_ReturnsMatchingItems()

    // Partition key + sort key equality returns single item
    [Test] SortKeyEquals_ReturnsSingleItem()

    // Partition key + sort key begins_with returns prefix-matched items
    [Test] SortKeyBeginsWith_ReturnsMatchingItems()

    // Partition key + sort key between returns range of items
    [Test] SortKeyBetween_ReturnsItemsInRange()

    // Partition key + sort key comparison (>, <) returns ordered subset
    [Test] SortKeyGreaterThan_ReturnsItemsAfterValue()

    // Reserved keyword key attributes are aliased and accepted
    [Test] ReservedKeywordKeyAttribute_AliasedAndAccepted()
}
```

#### UpdateIntegrationTests

```csharp
[TestFixture, TestCategory("Integration")]
public class UpdateIntegrationTests
{
    // SET + REMOVE + ADD combined in one UpdateExpression
    [Test] MultiClauseUpdate_AllClausesApplied()

    // SET with if_not_exists function
    [Test] SetIfNotExists_PreservesExistingValue()

    // list_append function
    [Test] AppendToList_AppendsElements()

    // ADD clause on numeric attribute
    [Test] AddToNumber_IncrementsValue()

    // DELETE clause removes elements from string set
    [Test] DeleteFromSet_RemovesElements()
}
```

#### ConditionIntegrationTests

```csharp
[TestFixture, TestCategory("Integration")]
public class ConditionIntegrationTests
{
    // ConditionExpression prevents write when condition fails
    [Test] ConditionFails_ThrowsConditionalCheckFailedException()

    // ConditionExpression allows write when condition passes
    [Test] ConditionPasses_WriteSucceeds()

    // Condition on DeleteItemRequest prevents deletion
    [Test] DeleteWithCondition_ConditionFails_ItemNotDeleted()
}
```

#### DirectResultMapperIntegrationTests (Spec 04)

```csharp
[TestFixture, TestCategory("Integration")]
public class DirectResultMapperIntegrationTests
{
    // Projection + direct mapping pipeline: write item → query with projection → map to DTO
    [Test] ProjectAndMap_AnonymousType_MapsFromRealResponse()

    // Projection + direct mapping for named DTO type
    [Test] ProjectAndMap_NamedType_MapsFromRealResponse()

    // Nested attribute projected and mapped
    [Test] ProjectAndMap_NestedAttribute_MapsFromRealResponse()

    // Enum attribute round-trips through projection and mapping
    [Test] ProjectAndMap_EnumAttribute_RoundTripsCorrectly()

    // Nullable attribute projected when present and absent
    [Test] ProjectAndMap_NullableAttribute_HandlesPresenceAndAbsence()
}
```

#### CombinedExpressionIntegrationTests (Spec 10)

```csharp
[TestFixture, TestCategory("Integration")]
public class CombinedExpressionIntegrationTests
{
    // KeyCondition + Projection + Filter on same QueryRequest — all alias scopes coexist
    [Test] KeyCondition_Projection_Filter_AllScopesCoexist()

    // Update + Condition on same UpdateItemRequest — #upd_ and #cond_ coexist
    [Test] Update_WithCondition_BothApplied()

    // Full fluent chain: key condition → projection → filter → executes correctly
    [Test] FullFluentChain_QueryRequest_ReturnsExpectedResults()
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
