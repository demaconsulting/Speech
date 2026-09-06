### SpeechModelParameters Verification

#### Verification Approach

`NumericParameter`, `ChoiceParameter`, `ChoiceParameterOption`, and `BooleanParameter` are
verified by deterministic unit tests exercising both a valid descriptor's exposed values and
every documented eager-validation failure at construction (invalid range, non-positive step,
default outside range, empty/duplicate choice options, default not among the choices, missing
common fields, and - for `NumericParameter`'s `isInteger` flag - a valid whole-number
declaration, the `false` default when `isInteger` is omitted, and a fractional minimum/maximum/
step/default rejected when `isInteger` is `true`).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline

#### Acceptance Criteria

Every parameter type exposes its constructed values verbatim, and every internally inconsistent
or invalid construction throws `ArgumentException`/`ArgumentNullException` rather than
succeeding.

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
