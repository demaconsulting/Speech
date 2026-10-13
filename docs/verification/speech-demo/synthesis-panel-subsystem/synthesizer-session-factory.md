### SynthesizerSessionFactory Verification

#### Verification Approach

`SynthesizerSessionFactory` is tested directly for argument validation, the honest "wrong role"
outcome, and the successful correct-role path. The correct-role tests use a deterministic,
publicly implementable test model double that implements the library's synthesis model contract
(`ISynthesisModel`) and is rooted in an isolated model store, so the path through the library's
`SpeechSynthesizerFactory` is exercised without a downloaded model or native runtime. The double
declares one parameter and a known installed-files layout, which lets the tests assert the inputs
the library receives, the engine returned for that model, and the library's invalid-parameter
behavior. The factory deliberately does not catch the invalid-parameter error; the
`SynthesisPanelViewModel` tests verify that its caller handles it by reporting an error state.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: The tests root the library's model store in a fresh directory under the test
  output folder
- **Test doubles**: `FakeSpeechModel` (wrong-role cases) and a deterministic synthesis-role test
  model double (correct-role cases)

#### Acceptance Criteria

The unit passes when: it validates its arguments, a model that has the synthesis role yields the
synthesizer composed from that model and the supplied parameter values, an invalid parameter value
is reported as an invalid-argument error that the caller handles, and a model that does not
implement the library's synthesis role yields the library's unavailable engine, with or without a
parameter value bag, rather than an exception.

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

##### SynthesizerSessionFactory_LoadAsync_SynthesisModel_ReturnsEngineForThatModel

**Scenario** (normal; `SessionSeamCorrectRole`): `LoadAsync` is called with the deterministic
synthesis-role test model double and a parameter value bag containing a valid value for the
parameter the double declares.

**Expected**: A synthesizer engine for that model is returned (not the unavailable engine); the
model and the supplied parameter values are the ones the engine was composed from, and the
shared model store is the one supplied at construction.

##### SynthesizerSessionFactory_LoadAsync_InvalidParameters_PropagatesArgumentException

**Scenario** (error; `SessionSeamInvalidParameter`): `LoadAsync` is called with the synthesis-role
test model double and a value of the wrong type, out of range, or not one of the offered choices
for a declared parameter.

**Expected**: `ArgumentException` propagates unchanged and is not converted into an unavailable
engine. Unrecognized keys remain ignored (boundary). The `SynthesisPanelViewModel` unit
scenario `SynthesisPanelViewModel_Play_SetupFaults_ReportsErrorStateWithMessage` verifies that the
caller handles such an error as an explained error state.
