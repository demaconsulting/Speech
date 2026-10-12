### SynthesizerSessionFactory Verification

#### Verification Approach

`SynthesizerSessionFactory` is tested directly for argument validation and the honest "wrong role"
outcome. The "correct role composes a working engine" path delegates to the library's own
`SpeechSynthesizerFactory`, which requires an `ISynthesisModel` - an interface only the library's
own assemblies can implement. That path is therefore outside this test project's reach and remains
covered by the library's own synthesis tests; it is an accepted gap here. The library's
`ArgumentException` for an invalid parameter value comes from the same unreachable path and is
likewise covered there.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: The tests root the library's model store in a fresh directory under the test
  output folder
- **Test doubles**: `FakeSpeechModel`

#### Acceptance Criteria

The unit passes when: it validates its arguments, and a model that does not implement the
library's synthesis role yields the library's unavailable engine, with or without a parameter
value bag, rather than an exception.

#### Test Scenarios

##### SynthesizerSessionFactory_Constructor_NullStore_ThrowsArgumentNullException

**Scenario**: The seam is constructed with no model store.

**Expected**: `ArgumentNullException`.

##### SynthesizerSessionFactory_LoadAsync_NullModel_ThrowsArgumentNullException

**Scenario**: `LoadAsync` is called with a missing model.

**Expected**: `ArgumentNullException`.

##### SynthesizerSessionFactory_LoadAsync_ModelNotSynthesisRole_ReturnsUnavailableEngine

**Scenario**: `LoadAsync` is called with a model that does not implement the synthesis role.

**Expected**: The library's `UnavailableSpeechSynthesizerEngine.Instance`, exactly like a model
that is not installed, rather than an exception.

##### SynthesizerSessionFactory_LoadAsync_ModelNotSynthesisRoleWithParameterValues_ReturnsUnavailableEngine

**Scenario**: `LoadAsync` is called with a non-null parameter value bag and a wrong-role model.

**Expected**: The same unavailable engine, proving the bag does not disturb the wrong-role
fallback.
