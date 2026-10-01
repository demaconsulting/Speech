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
throws, a cancelled token throws `OperationCanceledException`, an optional `parameterValues` bag
supplied by the caller is forwarded unchanged to the constructed engine, an unrecognized
`parameterValues` key composes successfully with only an `Info` diagnostic reported, and an
invalid value for a parameter the model does declare throws `ArgumentException` synchronously
from `LoadAsync()`.

#### Test Scenarios

See the SynthesisSubsystem-level scenarios "Composition: Real Engine for an Installed Model",
"Composition: Honest Fallback for Every Unavailable State", "Composition: Null Arguments and
Cancellation Are Programming Errors", and "Composition: Parameter Value Forwarding and
Validation".
