### ParameterBagParser Verification

#### Verification Approach

`ParameterBagParser` is verified through deterministic unit tests against hand-built
`NumericParameter`, `ChoiceParameter` and `BooleanParameter` instances. No model catalog, synthesizer
or command is involved. The tests pass `--stt-param` as the flag name, which also proves the flag
name is a parameter rather than hard-coded to `--tts-param`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: The parameter instances are immutable and the unit is stateless, so tests share no
  mutable state
- **Test doubles**: None

#### Acceptance Criteria

The unit passes when it splits well-formed tokens, resolves each parameter kind to its typed value,
rejects every malformed token and invalid value with an error naming the flag, rejects an
undeclared key, and rejects missing inputs.

#### Test Scenarios

##### ParameterBagParser_ParseToken_WellFormedToken_SplitsKeyAndValue

**Scenario**: The token `rate=1.2` is split.

**Expected**: The key is `rate` and the value is `1.2`.

##### ParameterBagParser_ParseToken_NoSeparator_ThrowsArgumentException

**Scenario**: The token `rate` has no `=`.

**Expected**: `ArgumentException` whose message contains the invoking flag name.

##### ParameterBagParser_ParseToken_EmptyKey_ThrowsArgumentException

**Scenario**: The token `=1.2` has an empty key.

**Expected**: `ArgumentException`.

##### ParameterBagParser_ParseToken_EmptyValueHalf_ReturnsEmptyValue

**Scenario**: The token `rate=` has an empty value.

**Expected**: The key is `rate` and the value is the empty string.

##### ParameterBagParser_Resolve_NumericInRange_ResolvesBoxedDouble

**Scenario**: `rate=1.5` is resolved against a numeric parameter bounded 0.5 to 2.0.

**Expected**: The bag holds a boxed `double` of 1.5.

##### ParameterBagParser_Resolve_NumericOutOfRange_ThrowsArgumentException

**Scenario**: `rate=5.0` is resolved against the same bounds.

**Expected**: `ArgumentException`.

##### ParameterBagParser_Resolve_NumericMalformed_ThrowsArgumentException

**Scenario**: `rate=not-a-number` is resolved against a numeric parameter.

**Expected**: `ArgumentException`.

##### ParameterBagParser_Resolve_IntegerParameterFractionalValue_ThrowsArgumentException

**Scenario**: `speakerIndex=1.5` is resolved against an integer-only numeric parameter.

**Expected**: `ArgumentException`.

##### ParameterBagParser_Resolve_IntegerParameterWholeValue_ResolvesBoxedDouble

**Scenario**: `speakerIndex=3` is resolved against the same parameter.

**Expected**: The bag holds a boxed `double` of 3.

##### ParameterBagParser_Resolve_ValidChoiceValue_ResolvesBoxedString

**Scenario**: `voice=bob` is resolved against a choice parameter offering `alice` and `bob`.

**Expected**: The bag holds the boxed string `bob`.

##### ParameterBagParser_Resolve_InvalidChoiceValue_ThrowsArgumentException

**Scenario**: `voice=carol` is resolved against the same parameter.

**Expected**: `ArgumentException`.

##### ParameterBagParser_Resolve_ValidBooleanValue_ResolvesBoxedBool

**Scenario**: The inputs `true`, `false` and `True` (a theory) are resolved against a boolean
parameter.

**Expected**: Each resolves to the matching boxed `bool`.

##### ParameterBagParser_Resolve_InvalidBooleanValue_ThrowsArgumentException

**Scenario**: `denoise=maybe` is resolved against a boolean parameter.

**Expected**: `ArgumentException`.

##### ParameterBagParser_Resolve_UnrecognizedKey_ThrowsArgumentException

**Scenario**: `bogus=1` is resolved against a model that does not declare `bogus`.

**Expected**: `ArgumentException` whose message contains the key and the invoking flag name.

##### ParameterBagParser_Resolve_NoRawValues_ReturnsEmptyBag

**Scenario**: An empty token list is resolved.

**Expected**: An empty bag.

##### ParameterBagParser_Resolve_NullRawValues_ThrowsArgumentNullException

**Scenario**: A null token list is resolved.

**Expected**: `ArgumentNullException`.

##### ParameterBagParser_Resolve_NullDeclaredParameters_ThrowsArgumentNullException

**Scenario**: A null declared-parameter list is supplied.

**Expected**: `ArgumentNullException`.
