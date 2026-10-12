### RecognizerSessionFactory Verification

#### Verification Approach

`RecognizerSessionFactory` is tested directly for argument validation and the honest "wrong role"
outcome. The "correct role composes a working engine" path delegates to the library's own
`SpeechRecognizerFactory`, which requires an `IRecognitionModel` - an interface the demo's test
doubles deliberately do not implement. That path remains covered by the library's own recognition
tests and by the system-level integration test, and is an accepted gap here.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: The tests root the library's model store in a fresh directory under the test
  output folder
- **Test doubles**: `FakeSpeechModel`

#### Acceptance Criteria

The unit passes when: it validates its arguments, and a model that does not implement the
library's recognition role yields the library's unavailable engine rather than an exception.

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
