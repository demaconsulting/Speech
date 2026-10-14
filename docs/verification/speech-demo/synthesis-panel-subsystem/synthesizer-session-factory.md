### SynthesizerSessionFactory Verification

#### Verification Approach

`SynthesizerSessionFactory` is tested directly for argument validation, the honest "wrong role"
outcome, and the successful correct-role path. The correct-role tests use a deterministic,
publicly implementable test model double that implements the library's synthesis model contract
(`ISynthesisModel`) and is rooted in an isolated model store, so the path through the library's
`SpeechSynthesizerFactory` is exercised without a downloaded model or native runtime. The double
declares one numeric and one choice parameter, records the values it is given and the speaker its
backend is asked for, and uses a known installed-files layout, which lets the tests assert the inputs
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
of the wrong type, out of range or not offered is reported as an invalid-argument error that the
caller handles, and a model that does not
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
synthesis-role test model double and no parameter values.

**Expected**: A synthesizer engine for that model is returned (not the unavailable engine) and the
model's backend is created for the installed-files directory of the store supplied at
construction.

##### SynthesizerSessionFactory_LoadAsync_SynthesisModelWithValues_ForwardsAppliedValues

**Scenario** (normal; `SessionSeamCorrectRole`): `LoadAsync` is called with the test model double
and non-default values (`gain` 0.8, `voice` "b"; the declared defaults are 0.5 and "a"), then text
is synthesized with the returned engine.

**Expected**: The model receives the values unchanged (`gain` is 0.8 and `voice` is "b"), and the
backend is asked to generate with the speaker the model resolved from the voice (speaker 1), which
proves the values are applied rather than the defaults.

##### SynthesizerSessionFactory_LoadAsync_OutOfRangeParameter_PropagatesArgumentException

**Scenario** (error; `SessionSeamInvalidParameter`): `LoadAsync` is called with the test model
double and a numeric value (99.0) outside the declared range (0.0 to 1.0).

**Expected**: `ArgumentException` propagates unchanged and is not converted into an unavailable
engine. Unrecognized keys remain ignored (boundary). The `SynthesisPanelViewModel` unit
scenario `SynthesisPanelViewModel_Play_SetupFaults_ReportsErrorStateWithMessage` verifies that the
caller handles such an error as an explained error state.

##### SynthesizerSessionFactory_LoadAsync_WrongTypeParameter_PropagatesArgumentException

**Scenario** (error; `SessionSeamWrongTypeParameter`): `LoadAsync` is called with the test model
double and a text value for the numeric parameter.

**Expected**: `ArgumentException` propagates unchanged.

##### SynthesizerSessionFactory_LoadAsync_UnofferedChoice_PropagatesArgumentException

**Scenario** (error; `SessionSeamUnofferedChoice`): `LoadAsync` is called with the test model
double and a choice value that the choice parameter does not offer.

**Expected**: `ArgumentException` propagates unchanged.
