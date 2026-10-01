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
reported, null arguments and a pre-cancelled token fault the returned task with the documented
exception type, an optional `parameterValues` bag supplied by the caller reaches the recognition
model's own engine-configuration logic unchanged, an unrecognized `parameterValues` key composes
successfully with only an `Info` diagnostic reported, and an invalid value for a parameter the
model does declare faults the returned task with `ArgumentException`.

#### Test Scenarios

See the RecognitionSubsystem-level scenarios "Composition: Real Engine for an Installed Model",
"Composition: Honest Fallback for Every Unavailable State", "Composition: Null Arguments and
Cancellation Are Programming/Caller Errors", "Composition: Parameter Value Bag Forwarding", and
"Composition: Parameter Value Validation".

#### Reuse and Concurrent Pre-Warming

This factory's "load once, reuse across many sessions" and "safe to call `LoadAsync` concurrently
from a background task" guidance (see the design doc) is a documentation contract about the
factory's own statelessness, not independently testable behavior of `LoadAsync` itself: the
factory holds no state to race on.
