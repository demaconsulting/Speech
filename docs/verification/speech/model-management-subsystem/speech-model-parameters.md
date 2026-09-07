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
for the recognized value without ever reaching the unrecognized-id diagnostic report.

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
the unrecognized id is ever reported.

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

##### NumericParameter rejects a null/empty Id

**Test**: `NumericParameter_Constructor_NullId_ThrowsArgumentNullException`

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
