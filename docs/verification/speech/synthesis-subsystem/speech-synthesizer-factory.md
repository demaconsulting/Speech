### SpeechSynthesizerFactory

#### Verification Approach

`SpeechSynthesizerFactory` is verified through unit tests that inject a fake
`ISynthesisBackendFactory`, so every composition branch can be exercised without a downloaded
model or a native speech-inference runtime. Model installation is simulated with a real scratch
directory created and removed per test instance, so the "is it installed?" check is exercised
against the file system rather than a mock. The store-based overload is verified against a real
`SpeechModelStore` rooted at a second scratch directory (via `SpeechModelStoreOptions
.RootPathOverride`), not a mock, proving the installed-model directory is genuinely resolved
through the store rather than hard-coded, and the catalog-based overload is verified against a
`SpeechModelCatalog` wrapping that same real store (via the internal test constructor with an
empty known-model list), proving `catalog.Store` genuinely resolves through to the same store
rather than a second, disconnected one. Diagnostics are verified with an NSubstitute sink where
the reported reason matters. Unlike the former synchronous factory, no playback device is
involved in composition at all - this type no longer takes one.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Setup**: A scratch installed-model directory under the system temporary directory, created in
  the test class constructor and deleted on disposal
- **Dependencies**: No external services, no model files, and no native speech-inference runtime

#### Acceptance Criteria

Composition is considered verified when a real engine is returned only for the fully available
case, every unavailable state returns the shared fallback without throwing, no backend is loaded
once an earlier check has failed, a backend load failure is caught and reported, a null model
throws, a canceled token throws `OperationCanceledException`, an optional `parameterValues` bag
supplied by the caller is forwarded unchanged to the constructed engine, an unrecognized
`parameterValues` key composes successfully with only an `Info` diagnostic reported, and an
invalid value for a parameter the model does declare throws `ArgumentException` synchronously
from `LoadAsync()`.

#### Test Scenarios

##### Composition: Real Engine for an Installed Model

**Tests**: `SpeechSynthesizerFactory_LoadAsync_ModelInstalled_ReturnsRealEngine`,
`SpeechSynthesizerFactory_LoadAsync_WithStoreModelInstalled_ReturnsRealEngine`,
`SpeechSynthesizerFactory_LoadAsync_WithCatalogModelInstalled_ReturnsRealEngine`

An installed synthesis model composes a real engine, resolved from a directory, a store, or a
catalog.

##### Composition: Honest Fallback for Every Unavailable State

**Tests**: `SpeechSynthesizerFactory_LoadAsync_ModelNotInstalled_ReturnsUnavailableEngine`,
`SpeechSynthesizerFactory_LoadAsync_ModelRoleIsNotSynthesis_ReturnsUnavailableEngine`,
`SpeechSynthesizerFactory_LoadAsync_EngineLoadFails_ReturnsUnavailableEngineAndDoesNotThrow`,
`SpeechSynthesizerFactory_LoadAsync_WithStoreModelNotInstalled_ReturnsUnavailableEngine`,
`SpeechSynthesizerFactory_LoadAsync_WithCatalogModelNotInstalled_ReturnsUnavailableEngine`

A missing model, wrong-role model, or failed backend load returns the shared unavailable engine.

##### Composition: Null Arguments and Cancellation Are Programming Errors

**Tests**: `SpeechSynthesizerFactory_LoadAsync_NullModel_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_WithStoreNullModel_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_WithStoreNullStore_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_WithCatalogNullModel_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_WithCatalogNullCatalog_ThrowsArgumentNullException`,
`SpeechSynthesizerFactory_LoadAsync_CanceledToken_ThrowsOperationCanceledException`,
`SpeechSynthesizerFactory_LoadAsync_CanceledDuringGraceWindowCreateSucceeds_ThrowsAndDisposesBackend`

Null arguments throw `ArgumentNullException`; a canceled token throws
`OperationCanceledException`, including cancellation during the grace window, where the
already-created backend is disposed.

##### Composition: Parameter Value Forwarding and Validation

**Tests**: `SpeechSynthesizerFactory_LoadAsync_ParameterValuesSupplied_ForwardedToEngine`,
`SpeechSynthesizerFactory_LoadAsync_UnrecognizedParameterId_ComposesAndReportsInfo`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedNumericParameterOutOfRange_Throws`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedNumericParameterWrongType_Throws`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedIntegerParameterFractionalValue_Throws`,
`SpeechSynthesizerFactory_LoadAsync_RecognizedChoiceParameterInvalidOption_Throws`

Supplied values are forwarded unchanged, an unrecognized key only reports `Info`, and an invalid
value for a declared parameter throws `ArgumentException`.
