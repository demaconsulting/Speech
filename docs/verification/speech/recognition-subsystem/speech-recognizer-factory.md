### SpeechRecognizerFactory

#### Verification Approach

`SpeechRecognizerFactory` is verified through unit tests that inject a fake
`IRecognitionBackendFactory`, so every composition branch can be exercised without a downloaded
model or a native speech-inference runtime. Model installation is simulated with a real scratch
directory created and removed per test instance, so the "is it installed?" check is exercised
against the file system rather than a mock. The store-based overload is verified against a real
`SpeechModelStore` rooted at a second scratch directory (via `SpeechModelStoreOptions
.RootPathOverride`), not a mock, proving the installed-model directory is genuinely resolved
through the store rather than hard-coded, and the catalog-based overload is verified against a
`SpeechModelCatalog` wrapping that same real store (via the internal test constructor with an
empty known-model list), proving `catalog.Store` genuinely resolves through to the same store
rather than a second, disconnected one. Diagnostics are verified with an NSubstitute sink where
the reported reason matters. Because `LoadAsync` is now `async`, every outcome is observed by
awaiting the returned task and, for the fault cases, asserting the specific exception type the
task faults with, rather than a synchronous throw.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Setup**: A scratch installed-model directory under the system temporary directory, created in
  the test class constructor and deleted on disposal
- **Dependencies**: No external services, no model files, and no native speech-inference runtime

#### Acceptance Criteria

Composition is considered verified when a real engine is returned only for the fully available
case, every unavailable state returns the shared fallback without faulting the returned task, no
backend is loaded once an earlier check has failed, a backend load failure is caught and
reported, null arguments and a pre-canceled token fault the returned task with the documented
exception type, an optional `parameterValues` bag supplied by the caller reaches the recognition
model's own engine-configuration logic unchanged, an unrecognized `parameterValues` key composes
successfully with only an `Info` diagnostic reported, and an invalid value for a parameter the
model does declare faults the returned task with `ArgumentException`.

#### Test Scenarios

##### Composition: Real Engine for an Installed Model

**Tests**: `SpeechRecognizerFactory_LoadAsync_ModelInstalled_ReturnsRealEngine`,
`SpeechRecognizerFactory_LoadAsync_WithStoreModelInstalled_ReturnsRealEngine`,
`SpeechRecognizerFactory_LoadAsync_WithCatalogModelInstalled_ReturnsRealEngine`

An installed recognition model composes a real engine from a directory, store, or catalog.

##### Composition: Honest Fallback for Every Unavailable State

**Tests**: `SpeechRecognizerFactory_LoadAsync_ModelNotInstalled_ReturnsUnavailableEngine`,
`SpeechRecognizerFactory_LoadAsync_ModelRoleIsNotRecognition_ReturnsUnavailableEngine`,
`SpeechRecognizerFactory_LoadAsync_EngineLoadFails_ReturnsUnavailableEngineAndDoesNotFaultTask`,
`SpeechRecognizerFactory_LoadAsync_WithStoreModelNotInstalled_ReturnsUnavailableEngine`,
`SpeechRecognizerFactory_LoadAsync_WithCatalogModelNotInstalled_ReturnsUnavailableEngine`

Every unavailable state returns the shared unavailable engine without faulting the task.

##### Composition: Null Arguments and Cancellation Are Programming/Caller Errors

**Tests**: `SpeechRecognizerFactory_LoadAsync_NullModel_FaultsWithArgumentNullException`,
`SpeechRecognizerFactory_LoadAsync_WithStoreNullModel_FaultsWithArgumentNullException`,
`SpeechRecognizerFactory_LoadAsync_WithStoreNullStore_FaultsWithArgumentNullException`,
`SpeechRecognizerFactory_LoadAsync_WithCatalogNullModel_FaultsWithArgumentNullException`,
`SpeechRecognizerFactory_LoadAsync_WithCatalogNullCatalog_FaultsWithArgumentNullException`,
`SpeechRecognizerFactory_LoadAsync_CanceledToken_FaultsWithOperationCanceledException`,
`SpeechRecognizerFactory_LoadAsync_CanceledDuringGraceWindowCreateSucceeds_ThrowsAndDisposesBackend`

Null arguments and cancellation fault the task; a cancellation arriving after the backend was
created disposes that backend instead of leaking it.

##### Composition: Parameter Value Bag Forwarding

**Tests**: `SpeechRecognizerFactory_LoadAsync_ParameterValuesSupplied_ReachesModelCreateBackend`,
`SpeechRecognizerFactory_LoadAsync_WithStoreParameterValuesSupplied_ReachesModelCreateBackend`,
`SpeechRecognizerFactory_LoadAsync_WithCatalogParameterValuesSupplied_ReachesModelCreateBackend`

Caller-supplied values reach the model's own `CreateBackend` override.

##### Composition: Parameter Value Validation

**Tests**: `SpeechRecognizerFactory_LoadAsync_UnrecognizedParameterId_ComposesAndReportsInfo`,
`SpeechRecognizerFactory_LoadAsync_RecognizedNumericParameterOutOfRange_FaultsWithArgumentException`,
`SpeechRecognizerFactory_LoadAsync_RecognizedNumericParameterWrongType_FaultsWithArgumentException`,
`SpeechRecognizerFactory_LoadAsync_RecognizedChoiceParameterInvalidOption_FaultsWithArgumentException`,
`SpeechRecognizerFactory_LoadAsync_RecognizedBooleanParameterWrongType_FaultsWithArgumentException`

An unrecognized key only reports `Info`; an invalid value for a declared parameter faults the task
with `ArgumentException`.

See also the RecognitionSubsystem-level scenarios of the same names for the fuller descriptions.

#### Reuse and Concurrent Pre-Warming

This factory's "load once, reuse across many sessions" and "safe to call `LoadAsync` concurrently
from a background task" guidance (see the design doc) is a documentation contract about the
factory's own statelessness, not independently testable behavior of `LoadAsync` itself: the
factory holds no state to race on.
