### AudioDeviceDescription

#### Verification Approach

Verified directly in `AudioDeviceDescriptionTests.cs` by constructing instances and asserting the
compiler-generated record equality; no mocking is applicable since the type has no dependencies.
`AudioDeviceSelectionTests` additionally compares resolved descriptions by value.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

Two records constructed with identical property values compare equal, and records with any
differing property compare unequal (compiler-generated record semantics); both cases have direct test evidence.

#### Test Scenarios

##### Equality: Identical Values Compare Equal

Verifies that two `AudioDeviceDescription` instances constructed with the same `Name`,
`Direction`, `ChannelCount`, and `SampleRate` are equal and share a hash code. Also exercised
indirectly through `AudioDeviceSelectionTests` assertions that compare resolved descriptions by
value.

**Test**: `AudioDeviceDescription_Equality_IdenticalValues_AreEqual`

##### Inequality: Differing Values Compare Unequal

Verifies that changing any one of `Name`, `Direction`, `ChannelCount`, or `SampleRate` yields a
description that is not equal to the original.

**Test**: `AudioDeviceDescription_Equality_AnyDifferingValue_AreNotEqual`
