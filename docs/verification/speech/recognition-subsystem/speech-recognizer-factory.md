### SpeechRecognizerFactory

#### Verification Approach

`SpeechRecognizerFactory` is verified through unit tests that inject a fake
`IRecognitionEngineFactory`, so every composition branch can be exercised without a downloaded
model or a native speech-inference runtime. Model installation is simulated with a real scratch
directory created and removed per test instance, so the "is it installed?" check is exercised
against the file system rather than a mock. The store-based overload is verified against a real
`SpeechModelStore` rooted at a second scratch directory (via `SpeechModelStoreOptions
.RootPathOverride`), not a mock, proving the installed-model directory is genuinely resolved
through the store rather than hard-coded, and the catalog-based overload is verified against a
`SpeechModelCatalog` wrapping that same real store (via the internal test constructor with an
empty known-model list), proving `catalog.Store` genuinely resolves through to the same store
rather than a second, disconnected one. Capture-device availability is supplied by an
NSubstitute `IAudioCaptureDevice` and by the shared unavailable capture device. Diagnostics are
verified with an NSubstitute sink where the reported reason matters.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Setup**: A scratch installed-model directory under the system temporary directory, created in
  the test class constructor and deleted on disposal
- **Dependencies**: No external services, no model files, and no native speech-inference runtime

#### Acceptance Criteria

Composition is considered verified when a real recognizer is returned only for the fully
available case, every unavailable state returns the shared fallback without throwing, no engine
is loaded once an earlier check has failed, an engine load failure is caught and reported, null
arguments throw, an optional `parameterValues` bag supplied by the caller reaches the recognition
model's own engine-configuration logic unchanged, an unrecognized `parameterValues` key composes
successfully with only an `Info` diagnostic reported, and an invalid value for a parameter the
model does declare throws `ArgumentException` synchronously from `Create()`.

#### Test Scenarios

See the RecognitionSubsystem-level scenarios "Composition: Real Recognizer for an Installed Model
and Available Device", "Composition: Honest Fallback for Every Unavailable State", "Composition:
Null Arguments Are Programming Errors", "Composition: Parameter Value Bag Forwarding", and
"Composition: Parameter Value Validation".

#### Reuse and Concurrent Pre-Warming

This factory's "construct once, reuse across turns" and "safe to call `Create` concurrently from
a background task" guidance (see the design doc) is a documentation contract about the factory's
own statelessness, not independently testable behavior of `Create` itself: the factory holds no
state to race on. It is exercised end-to-end by
`DemaConsulting.Speech.Cli.Tests.Commands.ConversationCommandSubsystem.AskCommandTests
.AskCommand_Run_PrewarmsRecognizerConcurrentlyWithPlayback_CreatesRecognizerBeforePlaybackCompletes`,
which proves a host (the CLI's `ask` command) genuinely calls `Create` from a background task
while other work (Phase 1's synthesis/playback) proceeds concurrently, and that the resulting
recognizer is adopted correctly once both complete.
