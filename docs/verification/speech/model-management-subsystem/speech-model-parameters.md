### SpeechModelParameters Verification

#### Verification Approach

`NumericParameter`, `ChoiceParameter`, `ChoiceParameterOption`, and `BooleanParameter` are
verified by deterministic unit tests exercising both a valid descriptor's exposed values and
every documented eager-validation failure at construction (invalid range, non-positive step,
default outside range, empty/duplicate choice options, default not among the choices, missing
common fields, and - for `NumericParameter`'s `isInteger` flag - a valid whole-number
declaration, the `false` default when `isInteger` is omitted, and a fractional minimum/maximum/
step/default rejected when `isInteger` is `true`). `SpeechModelParameterDiagnostics.ValidateAndReport`
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
determinism with several invalid values, the `ParamName` of `"parameterValues"`, null mandatory
arguments, and descriptor construction edge cases (non-finite maximum/step/default, boundary
default, null option element, defensive copy of the option list, and `bounds` as the reported
parameter name for an integer declaration with a fractional bound).

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
reports `ParamName` `"parameterValues"`. A mandatory `null` argument throws `ArgumentNullException`.
A `ChoiceParameter` neither accepts a `null` option element nor is affected by later mutation of
the list it was constructed from.

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

##### NumericParameter rejects an empty/whitespace Id and a null DisplayName or Description

**Test**: `NumericParameter_Constructor_InvalidCommonFields_Throw`

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

##### ChoiceParameter rejects a null option list or null default

**Test**: `ChoiceParameter_Constructor_NullOptionsOrDefault_ThrowsArgumentNullException`

##### ChoiceParameter rejects an invalid Id, null DisplayName or null Description

**Test**: `ChoiceParameter_Constructor_InvalidCommonFields_Throw`

##### ChoiceParameter keeps its options when the caller's list is mutated afterwards

**Test**: `ChoiceParameter_Constructor_CallerListMutatedAfterwards_OptionsUnchanged`

##### BooleanParameter exposes its constructed default for both true and false

**Test**: `BooleanParameter_Constructor_ValidValues_ExposesValues`

##### BooleanParameter rejects a whitespace Id

**Test**: `BooleanParameter_Constructor_WhitespaceId_ThrowsArgumentException`

##### BooleanParameter rejects a null DisplayName

**Test**: `BooleanParameter_Constructor_NullDisplayName_ThrowsArgumentNullException`

##### ValidateAndReport is a no-op for a null supplied-values bag

**Test**: `ValidateAndReport_NullSuppliedValues_DoesNothing`

##### ValidateAndReport is a no-op for an empty supplied-values bag

**Test**: `ValidateAndReport_EmptySuppliedValues_DoesNothing`

##### ValidateAndReport reports Info and does not throw for an unrecognized parameter id

**Test**: `ValidateAndReport_UnrecognizedParameterId_ReportsInfoAndDoesNotThrow`

##### ValidateAndReport does nothing for a valid recognized numeric value

**Test**: `ValidateAndReport_ValidRecognizedNumericValue_DoesNothing`

##### ValidateAndReport throws for a wrong-CLR-type numeric value

**Test**: `ValidateAndReport_NumericParameterWrongType_Throws`

##### ValidateAndReport throws for a numeric value outside [Minimum, Maximum]

**Test**: `ValidateAndReport_NumericParameterOutOfRange_Throws`

##### ValidateAndReport throws for a fractional value against an IsInteger parameter

**Test**: `ValidateAndReport_IntegerParameterFractionalValue_Throws`

##### ValidateAndReport does not throw for a whole-number value against an IsInteger parameter

**Test**: `ValidateAndReport_IntegerParameterWholeNumberValue_DoesNotThrow`

##### ValidateAndReport throws for a choice value matching no declared option

**Test**: `ValidateAndReport_ChoiceParameterInvalidOption_Throws`

##### ValidateAndReport throws for a wrong-CLR-type choice value

**Test**: `ValidateAndReport_ChoiceParameterWrongType_Throws`

##### ValidateAndReport does not throw for a valid choice value

**Test**: `ValidateAndReport_ChoiceParameterValidOption_DoesNotThrow`

##### ValidateAndReport throws for a wrong-CLR-type boolean value

**Test**: `ValidateAndReport_BooleanParameterWrongType_Throws`

##### ValidateAndReport does not throw for a valid boolean value

**Test**: `ValidateAndReport_BooleanParameterValidValue_DoesNotThrow`

##### ValidateAndReport throws for the invalid recognized value even when an unrecognized id is also supplied

**Test**: `ValidateAndReport_InvalidRecognizedValueAndUnrecognizedId_ThrowsForRecognizedValue`

##### ValidateAndReport throws ArgumentException for a null numeric value

**Test**: `ValidateAndReport_NumericParameterNullValue_ThrowsArgumentException`

##### ValidateAndReport throws ArgumentException for a null choice value

**Test**: `ValidateAndReport_ChoiceParameterNullValue_ThrowsArgumentException`

##### ValidateAndReport throws ArgumentException for a null boolean value

**Test**: `ValidateAndReport_BooleanParameterNullValue_ThrowsArgumentException`

##### ValidateAndReport throws for an unsupported descriptor subtype

**Test**: `ValidateAndReport_UnsupportedParameterDescriptorType_Throws`

##### ValidateAndReport reports a non-finite numeric value as non-finite

**Test**: `ValidateAndReport_NumericParameterNonFiniteValue_ThrowsFiniteMessage`

##### ValidateAndReport accepts int and float numeric values

**Test**: `ValidateAndReport_NumericParameterIntAndFloatValues_DoNotThrow`

##### ValidateAndReport accepts values exactly at Minimum and Maximum

**Test**: `ValidateAndReport_NumericParameterBoundaryValue_DoesNotThrow`

##### ValidateAndReport throws for the first invalid value in declared-parameter order

**Test**: `ValidateAndReport_MultipleInvalidValues_ThrowsForFirstDeclaredParameter`

##### ValidateAndReport names the received type for a wrong-type choice value

**Test**: `ValidateAndReport_ChoiceParameterWrongType_MessageNamesReceivedType`

##### ValidateAndReport throws ArgumentNullException for a null mandatory argument

**Test**: `ValidateAndReport_NullArguments_ThrowArgumentNullException`
