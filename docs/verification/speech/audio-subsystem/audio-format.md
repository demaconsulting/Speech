### AudioFormat

#### Verification Approach

Verified through direct unit tests in `AudioFormatTests.cs` with no doubles or external
dependencies, since `AudioFormat` is a pure validating value type.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when valid sample-rate/channel-count pairs are exposed unchanged, non-positive values
are rejected, `Mono(sampleRate)` returns a one-channel format using the same validation rules, and
equal-valued instances compare by value.

#### Test Scenarios

##### Construction: Valid Values Are Exposed Unchanged

**Test**: `AudioFormat_Constructor_ValidValues_ExposesProperties`

##### Construction: Non-Positive Sample Rate Throws ArgumentOutOfRangeException

**Test**: `AudioFormat_Constructor_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException`

##### Construction: Non-Positive Channel Count Throws ArgumentOutOfRangeException

**Test**: `AudioFormat_Constructor_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException`

##### Convenience Factory: Mono Returns a One-Channel Format

**Test**: `AudioFormat_Mono_ValidSampleRate_ReturnsMonoFormat`

##### Convenience Factory: Mono Rejects a Non-Positive Sample Rate

**Test**: `AudioFormat_Mono_NonPositiveSampleRate_ThrowsArgumentOutOfRangeException`

##### Equality: Equivalent Values Compare by Value

**Test**: `AudioFormat_ValueEquality_EquivalentValuesCompareByValue`
