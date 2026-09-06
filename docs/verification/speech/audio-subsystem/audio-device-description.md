### AudioDeviceDescription

#### Verification Approach

Verified indirectly through unit tests that construct instances and rely on the
compiler-generated record equality; no mocking is applicable since the type has no dependencies.
No dedicated `AudioDeviceDescriptionTests` file exists — the only current evidence is the
equal-values comparisons exercised by `AudioDeviceSelectionTests` assertions.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

Two records constructed with identical property values compare equal, and records with any
differing property compare unequal (compiler-generated record semantics); only the equal-values
case currently has test evidence.

#### Test Scenarios

##### Equality: Identical Values Compare Equal

Verifies that two `AudioDeviceDescription` instances constructed with the same `Name`,
`Direction`, `ChannelCount`, and `SampleRate` are equal, exercised indirectly through
`AudioDeviceSelectionTests` assertions that compare resolved descriptions by value.

##### Inequality: Differing Values Compare Unequal

Not currently exercised by a direct test; compiler-generated record equality guarantees this
behavior, but no `AudioDeviceDescriptionTests` case asserts it directly. Tracked as a
verification gap rather than claimed as covered.
