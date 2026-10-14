### RecognizerSessionFactory Verification

#### Verification Approach

`RecognizerSessionFactory` is tested directly for argument validation, the honest "wrong role"
outcome, and the successful correct-role path. `IRecognitionModel` is a public interface, so the
tests use a deterministic public test model double that implements it, rooted in a fresh model
store, to assert that the model and store inputs are forwarded to the library, that the returned
engine is the one the library composes for that model. No downloaded model or native runtime is needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: The tests root the library's model store in a fresh directory under the test
  output folder
- **Test doubles**: `FakeSpeechModel` (wrong role); a public deterministic recognition-model test
  double implementing `IRecognitionModel` (correct role)

#### Acceptance Criteria

The unit passes when: it validates its arguments; a model that does not implement the library's
recognition role yields the library's unavailable engine rather than an exception; and a
recognition-role model yields the engine the library composes for that model.

#### Test Scenarios

##### RecognizerSessionFactory_Constructor_NullStore_ThrowsArgumentNullException

**Scenario**: The seam is constructed with no model store.

**Expected**: `ArgumentNullException`.

##### RecognizerSessionFactory_LoadAsync_NullModel_ThrowsArgumentNullException

**Scenario**: `LoadAsync` is called with a missing model.

**Expected**: `ArgumentNullException`.

##### RecognizerSessionFactory_LoadAsync_ModelNotRecognitionRole_ReturnsUnavailableEngine

**Scenario**: `LoadAsync` is called with a model that does not implement the library's
recognition role.

**Expected**: The library's own `UnavailableSpeechRecognizerEngine.Instance`, exactly like a
model that is not installed, rather than an exception.

##### RecognizerSessionFactory_LoadAsync_RecognitionModel_ReturnsEngineForThatModel

**Scenario** (normal): `LoadAsync` is called with the deterministic recognition-model test double,
which implements the library's recognition role, against a store rooted in a fresh directory.

**Expected**: A recognizer engine for that model is returned (the model and store were forwarded
to the library factory and the engine reflects that model), not the unavailable engine and not an
exception.
