### SpeechModelParameters Verification

#### Verification Approach

`NumericParameter`, `ChoiceParameter`, `ChoiceParameterOption`, and `BooleanParameter` are
verified by deterministic unit tests exercising both a valid descriptor's exposed values and
every documented eager-validation failure at construction (invalid range, non-positive step,
default outside range, empty/duplicate choice options, default not among the choices, missing
common fields, and - for `NumericParameter`'s `isInteger` flag - a valid whole-number
declaration, the `false` default when `isInteger` is omitted, and a fractional minimum/maximum/
step/default rejected when `isInteger` is `true`). Each invalid-input scenario is its own test
with a distinct Arrange / Act / Assert, asserting the reported `ParamName` where the design
documents one, so a failure identifies exactly one rejected input.
`SpeechModelParameterDiagnostics.ValidateAndReport`
is verified by deterministic unit tests in `SpeechModelParameterDiagnosticsTests.cs` against a
`Substitute.For<ISpeechDiagnostics>()` diagnostics sink and small in-test descriptor sets covering
a `NumericParameter`, a `ChoiceParameter`, and a `BooleanParameter`, proving both the silent,
diagnostic-only path for an unrecognized parameter id and the throwing path for every documented
invalid-value shape for a recognized parameter (wrong CLR type, out-of-range, non-integral,
invalid choice), plus that a mix of one invalid recognized value and one unrecognized id throws
for the recognized value without ever reaching the unrecognized-id diagnostic report. The tests
further cover a `null` value for each descriptor type (an `ArgumentException`, never a
`NullReferenceException`), a descriptor subtype the helper does not know (an `ArgumentException`
using a minimal in-test `ISpeechModelParameter` implementation), non-finite (NaN/infinite) values
reported as non-finite, `int`/`float` and inclusive-boundary values accepted, declared-order
determinism with several invalid values, the `ParamName` of `"parameterValues"`, each mandatory
argument (`modelId`, `declaredParameters`, `diagnostics`, `category`) being null as its own test,
and descriptor construction edge cases (non-finite maximum/step/default, boundary default, null
option element, defensive copy of the option list, and `bounds` as the reported parameter name
for an integer declaration with a fractional bound).

Every diagnostics test name carries the `SpeechModelParameterDiagnostics_` unit prefix
(`{ClassName}_{Method}_{Scenario}_{Expected}`), and the descriptor-type tests carry their own
type prefix, so each test identifies its unit.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline

#### Acceptance Criteria

Every parameter type exposes its constructed values verbatim, and every internally inconsistent
or invalid construction throws `ArgumentException`/`ArgumentNullException` rather than
succeeding. `SpeechModelParameterDiagnostics.ValidateAndReport` is a no-op for a `null` or empty
supplied-values bag; reports an `Info` diagnostic and never throws for a supplied key naming a
parameter not declared by the model; never throws (and reports nothing) for a valid value
supplied for a declared parameter; and throws `ArgumentException` naming the parameter id, the
model id, and the specific reason for a wrong-type, out-of-range, non-integral, or invalid-choice
value supplied for a declared parameter - including when an unrecognized id is also present in
the same call, in which case the recognized-value failure takes precedence and no diagnostic for
the unrecognized id is ever reported. A `null` supplied value, a non-finite numeric value, and a
value for an unsupported descriptor subtype each throw `ArgumentException`; every such exception
reports `ParamName` `"parameterValues"`. A mandatory `null` argument throws `ArgumentNullException`
naming that argument. A `ChoiceParameter` neither accepts a `null` option element nor is affected
by later mutation of the list it was constructed from.

#### Requirement Coverage

| Requirement | Tests |
| :---------- | :---- |
| `Speech-Models-SpeechModelParameters-NumericValidation` | minimum greater than maximum; non-positive step |
| `Speech-Models-SpeechModelParameters-NumericDefaultValidation` | default outside range; default at boundary |
| `...Parameters-NumericFiniteValidation` | non-finite minimum; non-finite maximum, step or default |
| `Speech-Models-SpeechModelParameters-NumericValuesExposed` | valid range exposes values; no unit is null |
| `Speech-Models-SpeechModelParameters-NumericCommonFieldValidation` | null id; empty or whitespace id |
| `Speech-Models-SpeechModelParameters-NumericDisplayMetadataValidation` | null display name; null description |
| `...Parameters-NumericIntegerFlag` | whole-number bounds expose true; omitted defaults to false |
| `...Parameters-NumericIntegerValidation` | fractional minimum, maximum, step, default; `bounds` name |
| `Speech-Models-SpeechModelParameters-ChoiceValidation` | empty options; null option element |
| `Speech-Models-SpeechModelParameters-ChoiceValuesExposed` | valid options expose values |
| `Speech-Models-SpeechModelParameters-ChoiceDuplicateValidation` | duplicate option values |
| `Speech-Models-SpeechModelParameters-ChoiceDefaultValidation` | default not an option |
| `Speech-Models-SpeechModelParameters-ChoiceOptionValueValidation` | empty value; null value; null label |
| `Speech-Models-SpeechModelParameters-ChoiceOptionsAndDefaultRequired` | null options; null default |
| `Speech-Models-SpeechModelParameters-ChoiceCommonFieldValidation` | whitespace id |
| `Speech-Models-SpeechModelParameters-ChoiceDisplayMetadataValidation` | null display name; null description |
| `Speech-Models-SpeechModelParameters-ChoiceOptionsDefensiveCopy` | caller list mutated afterwards |
| `...Parameters-BooleanValidation` | valid values; whitespace id; null id, display name, description |
| `...ParameterDiagnostics-*` | every scenario below, each linked from its own requirement |

#### Test Scenarios

##### NumericParameter exposes its constructed values

**Test**: `NumericParameter_Constructor_ValidRange_ExposesValues`

##### NumericParameter with no unit exposes a null Unit

**Test**: `NumericParameter_Constructor_NoUnit_UnitIsNull`

##### NumericParameter rejects Minimum greater than Maximum

**Test**: `NumericParameter_Constructor_MinimumGreaterThanMaximum_ThrowsArgumentException`

##### NumericParameter rejects a non-positive Step

**Test**: `NumericParameter_Constructor_NonPositiveStep_ThrowsArgumentException`

##### NumericParameter rejects a Default outside [Minimum, Maximum]

**Test**: `NumericParameter_Constructor_DefaultOutsideRange_ThrowsArgumentException`

##### NumericParameter rejects a non-finite Minimum

**Test**: `NumericParameter_Constructor_NonFiniteMinimum_ThrowsArgumentException`

##### NumericParameter rejects a null Id

**Test**: `NumericParameter_Constructor_NullId_ThrowsArgumentNullException`

##### NumericParameter rejects an empty or whitespace Id

**Test**: `NumericParameter_Constructor_EmptyOrWhitespaceId_ThrowsArgumentException`

##### NumericParameter rejects a null DisplayName

**Test**: `NumericParameter_Constructor_NullDisplayName_ThrowsArgumentNullException`

##### NumericParameter rejects a null Description

**Test**: `NumericParameter_Constructor_NullDescription_ThrowsArgumentNullException`

##### NumericParameter rejects a non-finite Maximum, Step or Default naming bounds

**Test**: `NumericParameter_Constructor_NonFiniteMaximumStepOrDefault_ThrowsArgumentException`

##### NumericParameter accepts a Default exactly at Minimum or Maximum

**Test**: `NumericParameter_Constructor_DefaultAtBoundary_Succeeds`

##### NumericParameter reports bounds as the offending parameter for an integer declaration with a fractional bound

**Test**: `NumericParameter_Constructor_IsIntegerWithFractionalBound_ParamNameIsBounds`

##### NumericParameter exposes IsInteger true for a valid whole-number declaration

**Test**: `NumericParameter_Constructor_IsIntegerWithWholeNumberBounds_ExposesIsIntegerTrue`

##### NumericParameter defaults IsInteger to false when omitted

**Test**: `NumericParameter_Constructor_IsIntegerOmitted_DefaultsToFalse`

##### NumericParameter rejects a fractional Minimum when IsInteger is true

**Test**: `NumericParameter_Constructor_IsIntegerWithFractionalMinimum_ThrowsArgumentException`

##### NumericParameter rejects a fractional Maximum when IsInteger is true

**Test**: `NumericParameter_Constructor_IsIntegerWithFractionalMaximum_ThrowsArgumentException`

##### NumericParameter rejects a fractional Step when IsInteger is true

**Test**: `NumericParameter_Constructor_IsIntegerWithFractionalStep_ThrowsArgumentException`

##### NumericParameter rejects a fractional Default when IsInteger is true

**Test**: `NumericParameter_Constructor_IsIntegerWithFractionalDefault_ThrowsArgumentException`

##### ChoiceParameter exposes its constructed options and default

**Test**: `ChoiceParameter_Constructor_ValidOptions_ExposesValues`

##### ChoiceParameter rejects an empty option list

**Test**: `ChoiceParameter_Constructor_EmptyOptions_ThrowsArgumentException`

##### ChoiceParameter rejects duplicate option values

**Test**: `ChoiceParameter_Constructor_DuplicateOptionValues_ThrowsArgumentException`

##### ChoiceParameter rejects a default that matches no option

**Test**: `ChoiceParameter_Constructor_DefaultNotAnOption_ThrowsArgumentException`

##### ChoiceParameterOption rejects an empty value

**Test**: `ChoiceParameterOption_Constructor_EmptyValue_ThrowsArgumentException`

##### ChoiceParameter rejects a null option element

**Test**: `ChoiceParameter_Constructor_NullOptionElement_ThrowsArgumentException`

##### ChoiceParameter rejects a null option list

**Test**: `ChoiceParameter_Constructor_NullOptions_ThrowsArgumentNullException`

##### ChoiceParameter rejects a null default

**Test**: `ChoiceParameter_Constructor_NullDefault_ThrowsArgumentNullException`

##### ChoiceParameter rejects a whitespace Id

**Test**: `ChoiceParameter_Constructor_WhitespaceId_ThrowsArgumentException`

##### ChoiceParameter rejects a null DisplayName

**Test**: `ChoiceParameter_Constructor_NullDisplayName_ThrowsArgumentNullException`

##### ChoiceParameter rejects a null Description

**Test**: `ChoiceParameter_Constructor_NullDescription_ThrowsArgumentNullException`

##### ChoiceParameter keeps its options when the caller's list is mutated afterwards

**Test**: `ChoiceParameter_Constructor_CallerListMutatedAfterwards_OptionsUnchanged`

##### ChoiceParameterOption rejects a null value

**Test**: `ChoiceParameterOption_Constructor_NullValue_ThrowsArgumentNullException`

##### ChoiceParameterOption rejects a null label

**Test**: `ChoiceParameterOption_Constructor_NullLabel_ThrowsArgumentNullException`

##### BooleanParameter exposes its constructed default for both true and false

**Test**: `BooleanParameter_Constructor_ValidValues_ExposesValues`

##### BooleanParameter rejects a whitespace Id

**Test**: `BooleanParameter_Constructor_WhitespaceId_ThrowsArgumentException`

##### BooleanParameter rejects a null DisplayName

**Test**: `BooleanParameter_Constructor_NullDisplayName_ThrowsArgumentNullException`

##### BooleanParameter rejects a null Id

**Test**: `BooleanParameter_Constructor_NullId_ThrowsArgumentNullException`

##### BooleanParameter rejects a null Description

**Test**: `BooleanParameter_Constructor_NullDescription_ThrowsArgumentNullException`

##### ValidateAndReport is a no-op for a null supplied-values bag

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NullSuppliedValues_DoesNothing`

##### ValidateAndReport is a no-op for an empty supplied-values bag

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_EmptySuppliedValues_DoesNothing`

##### ValidateAndReport reports Info and does not throw for an unrecognized parameter id

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_UnrecognizedParameterId_ReportsInfoAndDoesNotThrow`

##### ValidateAndReport does nothing for a valid recognized numeric value

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_ValidRecognizedNumericValue_DoesNothing`

##### ValidateAndReport throws for a wrong-CLR-type numeric value

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterWrongType_Throws`

##### ValidateAndReport throws for a numeric value outside [Minimum, Maximum]

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterOutOfRange_Throws`

##### ValidateAndReport throws for a fractional value against an IsInteger parameter

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_IntegerParameterFractionalValue_Throws`

##### ValidateAndReport does not throw for a whole-number value against an IsInteger parameter

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_IntegerParameterWholeNumberValue_DoesNotThrow`

##### ValidateAndReport throws for a choice value matching no declared option

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_ChoiceParameterInvalidOption_Throws`

##### ValidateAndReport throws for a wrong-CLR-type choice value

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_ChoiceParameterWrongType_Throws`

##### ValidateAndReport does not throw for a valid choice value

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_ChoiceParameterValidOption_DoesNotThrow`

##### ValidateAndReport throws for a wrong-CLR-type boolean value

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_BooleanParameterWrongType_Throws`

##### ValidateAndReport does not throw for a valid boolean value

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_BooleanParameterValidValue_DoesNotThrow`

##### ValidateAndReport throws for the invalid recognized value even when an unrecognized id is also supplied

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_InvalidRecognizedValueAndUnrecognizedId_ThrowsForRecognizedValue`

##### ValidateAndReport throws ArgumentException for a null numeric value

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterNullValue_ThrowsArgumentException`

##### ValidateAndReport throws ArgumentException for a null choice value

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_ChoiceParameterNullValue_ThrowsArgumentException`

##### ValidateAndReport throws ArgumentException for a null boolean value

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_BooleanParameterNullValue_ThrowsArgumentException`

##### ValidateAndReport throws for an unsupported descriptor subtype

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_UnsupportedParameterDescriptorType_Throws`

##### ValidateAndReport reports a non-finite numeric value as non-finite

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterNonFiniteValue_ThrowsFiniteMessage`

##### ValidateAndReport accepts int and float numeric values

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterIntAndFloatValues_DoNotThrow`

##### ValidateAndReport accepts values exactly at Minimum and Maximum

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterBoundaryValue_DoesNotThrow`

##### ValidateAndReport throws for the first invalid value in declared-parameter order

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_MultipleInvalidValues_ThrowsForFirstDeclaredParameter`

##### ValidateAndReport names the received type for a wrong-type choice value

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_ChoiceParameterWrongType_MessageNamesReceivedType`

##### ValidateAndReport throws ArgumentNullException for a null model id

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NullModelId_ThrowsArgumentNullException`

##### ValidateAndReport throws ArgumentNullException for null declared parameters

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NullDeclaredParameters_ThrowsArgumentNullException`

##### ValidateAndReport throws ArgumentNullException for a null diagnostics sink

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NullDiagnostics_ThrowsArgumentNullException`

##### ValidateAndReport throws ArgumentNullException for a null category

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NullCategory_ThrowsArgumentNullException`

##### ValidateAndReport throws for a numeric value below the Minimum

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NumericParameterBelowMinimum_Throws`

##### ValidateAndReport reports each unrecognized id

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_MultipleUnrecognizedParameterIds_ReportsInfoForEach`

##### ValidateAndReport rejects a null declared-parameter element

**Test**: `SpeechModelParameterDiagnostics_ValidateAndReport_NullDeclaredParameterElement_ThrowsArgumentException`
