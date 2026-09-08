## SpeechCli SynthesisCommandSubsystem Verification

### Verification Approach

The SynthesisCommandSubsystem is verified through deterministic unit tests against a
hand-written `FakeSpeechSynthesizer` (recording `SpeakAsync` calls and `Stop`/`Dispose` call
counts, with settable `IsAvailable`/`SpeakAsyncException`) and `FakeCliModelCatalog`'s two new
delegate overrides (`GetPreferredAudioFormatOverride`/`CreateSynthesizerOverride`), reused from
`ModelCommandsSubsystem`'s own tests. Real-device dispatch is verified against a `FakePlaybackDeviceSource`
(a hand-written `ICliPlaybackDeviceSource` fake resolving purely from an in-memory device list,
touching no real PortAudio state at all) and a `FakeAudioPlaybackDevice`, so every `speak`
device-dispatch scenario is deterministic on every machine, headless or not - unlike constructing
a real `AudioDeviceFactory` with an injected probe, which still resolves to the honestly
unavailable fallback whenever `PortAudioEnvironment.Shared.IsInitialized` is `false`, regardless
of the injected probe (the exact bug this pass fixed; see `AudioDeviceFactoryPlaybackDeviceSourceTests`
below for the seam's own forwarding proof). `ParameterBagParser` is verified entirely in isolation
against hand-built `NumericParameter`/`ChoiceParameter`/`BooleanParameter` instances, with no
model catalog or synthesizer involved at all. The two new `ICliModelCatalog` seam members
(`GetPreferredAudioFormat`/`CreateSynthesizer`) are additionally verified against a **real**
`SpeechModelCatalogAdapter`, proving the production `is ISynthesisModel` cast genuinely throws for
a real, compiled-in recognition-role model, in
`SpeechModelCatalogAdapterTests.cs`. `AudioDeviceFactoryPlaybackDeviceSource` - the production
`ICliPlaybackDeviceSource` implementation - is separately verified against a **real**, composed
`AudioDeviceFactory` in `AudioDeviceFactoryPlaybackDeviceSourceTests.cs`, proving it is a pure
pass-through and does not alter `AudioDeviceFactory`'s own hardware-detection behavior in any way,
without asserting on whether real playback hardware happens to be present on the machine running
the test. Out-of-process integration tests in `IntegrationTests.cs` invoke the built tool as a
child process for the model-resolution and text-source error paths that matter most from an
operator's perspective.

Automated tests do **not** exercise a real, downloaded synthesis model's actual `--output`
WAV-writing path end to end: CI has no cached TTS model available (downloading one requires
network access this environment/CI runner is not guaranteed to have, and a multi-hundred-megabyte
download is unsuitable for every test run regardless). This is manual/local verification only
(see _Manual / Build-Time Verification_ below), following this repository's established
convention for genuinely environment-dependent behavior (see the
`Speech-AudioSubsystem-UnavailableFallbacks` and `SpeechCli DeviceCommandsSubsystem Verification`
documents for the same convention applied elsewhere).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Every unit test constructs its own fake catalog/synthesizer/audio probes;
  `SpeechModelCatalogAdapterTests` uses a freshly created, uniquely named temporary directory as
  its model-store root, deleted afterward via `IDisposable`
- **Test doubles**: `FakeSpeechSynthesizer` (hand-written `ISpeechSynthesizer` fake),
  `FakeCliModelCatalog` (extended with `GetPreferredAudioFormatOverride`/
  `CreateSynthesizerOverride`), `FakePlaybackDeviceSource` (hand-written `ICliPlaybackDeviceSource`
  fake resolving purely from an in-memory device list), `FakeAudioPlaybackDevice` (hand-written
  `IAudioPlaybackDevice` fake), `FakeAudioPlaybackDeviceProbe` (reused from
  `DeviceCommandsSubsystem`'s own tests)

### Test Scenarios

#### SpeakCommand — Text Source, Model Resolution, and Argument Parsing

**Tests**: `SpeakCommand_ParseArguments_MissingModel_ThrowsArgumentException`,
`SpeakCommand_ParseArguments_TextFlag_ParsesText`,
`SpeakCommand_ParseArguments_UnsupportedArgument_ThrowsArgumentException`,
`SpeakCommand_ParseArguments_FlagMissingValue_ThrowsArgumentException`,
`SpeakCommand_RunAsync_TextAndFileBothGiven_ThrowsArgumentException`,
`SpeakCommand_RunAsync_NoTextSourceAndStdinNotRedirected_ThrowsArgumentException`,
`SpeakCommand_RunAsync_UnknownModelId_ThrowsArgumentException`,
`SpeakCommand_RunAsync_WrongRoleModel_ThrowsArgumentException`,
`SpeakCommand_RunAsync_NotDownloadedModel_ThrowsArgumentExceptionWithDownloadHint`,
`SpeakCommand_RunAsync_Success_DisposesSynthesizerOnce`,
`SpeechCli_SpeakCommandWithoutModel_Invoked_ReturnsCleanError`,
`SpeechCli_SpeakCommandWithUnknownModel_Invoked_ReturnsCleanError`,
`SpeechCli_SpeakCommandWithWrongRoleModel_Invoked_ReturnsCleanError`,
`SpeechCli_SpeakCommandWithNotDownloadedModel_Invoked_ReturnsCleanErrorWithDownloadHint`,
`SpeechCli_SpeakCommandWithConflictingTextSources_Invoked_ReturnsCleanError`,
`Program_Run_WithSpeakCommand_DoesNotThrowNotImplemented`

**Scenario/Expected**: A missing `--model`, an unsupported argument, or a value-less flag are all
rejected with `ArgumentException`; `--text` parses its value; supplying both `--text` and
`--file`, or neither with stdin not redirected, throws `ArgumentException` before the model id is
even looked up; an unknown model id, a wrong-role model id, and a not-yet-downloaded model id are
each rejected with a distinct, actionable `ArgumentException` message (suggesting `list-models`,
`list-models --role tts`, and `download <modelId>` respectively); a successful run disposes the
fake synthesizer exactly once; every error path is proven both in-process and, cleanly and
non-zero-exit, against the built tool; `speak` no longer throws `NotImplementedException` when
dispatched.

**Requirement coverage**: `SpeechCli-SynthesisCommands-Speak`.

#### ParameterBagParser and `--param` Validation

**Tests**: `ParameterBagParser_ParseToken_WellFormedToken_SplitsKeyAndValue`,
`ParameterBagParser_ParseToken_NoSeparator_ThrowsArgumentException`,
`ParameterBagParser_ParseToken_EmptyKey_ThrowsArgumentException`,
`ParameterBagParser_ParseToken_EmptyValueHalf_ReturnsEmptyValue`,
`ParameterBagParser_Resolve_NumericInRange_ResolvesBoxedDouble`,
`ParameterBagParser_Resolve_NumericOutOfRange_ThrowsArgumentException`,
`ParameterBagParser_Resolve_NumericMalformed_ThrowsArgumentException`,
`ParameterBagParser_Resolve_IntegerParameterFractionalValue_ThrowsArgumentException`,
`ParameterBagParser_Resolve_IntegerParameterWholeValue_ResolvesBoxedDouble`,
`ParameterBagParser_Resolve_ValidChoiceValue_ResolvesBoxedString`,
`ParameterBagParser_Resolve_InvalidChoiceValue_ThrowsArgumentException`,
`ParameterBagParser_Resolve_ValidBooleanValue_ResolvesBoxedBool`,
`ParameterBagParser_Resolve_InvalidBooleanValue_ThrowsArgumentException`,
`ParameterBagParser_Resolve_UnrecognizedKey_ThrowsArgumentException`,
`ParameterBagParser_Resolve_NoRawValues_ReturnsEmptyBag`,
`SpeakCommand_ParseArguments_RepeatedParamFlags_AccumulatesInOrder`,
`SpeakCommand_RunAsync_ValidParam_ForwardsToCreateSynthesizer`,
`SpeakCommand_RunAsync_InvalidParam_ThrowsArgumentException`

**Scenario/Expected**: A well-formed `key=value` token splits correctly; a token with no `=` or
an empty key throws `ArgumentException`; an empty value half is accepted as the empty string; a
`NumericParameter` value within range resolves to a boxed `double`, an out-of-range or malformed
value throws, and a fractional value for an integer-only parameter throws while a whole-number
value succeeds; a `ChoiceParameter` value matching a declared option resolves to a boxed `string`,
a non-matching value throws; a `BooleanParameter` accepts `true`/`false` (case-insensitively) as a
boxed `bool` and rejects any other value; a key not declared by the resolved model throws
`ArgumentException`; repeated `--param` flags accumulate in the order given and are forwarded,
fully resolved, to `CreateSynthesizer`'s `parameterValues` argument; an invalid value for any
declared parameter kind is rejected before synthesis is attempted.

**Requirement coverage**: `SpeechCli-SynthesisCommands-ParamValidation`.

#### `--no-tags` Stripping

**Tests**: `SpeakCommand_ParseArguments_NoTagsFlag_ParsesTrue`,
`SpeakCommand_RunAsync_NoTags_StripsRecognizedTagExactly`,
`SpeakCommand_RunAsync_WithoutNoTags_PassesOriginalTextUnchanged`

**Scenario/Expected**: `--no-tags` parses to `true`; given `--no-tags` and input text containing
a recognized Natural Language Audio Tag (e.g. `Hello [laughs] there`), the exact text passed to
`SpeakAsync` has the tag span removed while the surrounding plain-text spans (including
inter-tag whitespace, which `AudioTagParser.Parse` preserves as its own plain-text span) are kept
verbatim (`Hello  there`); without `--no-tags`, the original text is passed to `SpeakAsync`
completely unchanged, regardless of the resolved (fake) model's own declared audio-tag support
level.

**Requirement coverage**: `SpeechCli-SynthesisCommands-NoTags`.

#### `--output` Versus Real Device Dispatch

**Tests**: `SpeakCommand_RunAsync_Output_ConstructsWavFileDeviceFromPreferredFormat`,
`SpeakCommand_RunAsync_UnknownDevice_ThrowsArgumentException`,
`SpeakCommand_RunAsync_NoPlaybackDeviceAvailable_ThrowsInvalidOperationException`

**Scenario/Expected**: `--output <path>` constructs a `WavFileAudioPlaybackDevice` at the given
path, sized from the resolved model's `GetPreferredAudioFormat` result (proven via the fake
catalog's override), ignoring any `--device` given alongside it; requesting an unrecognized
`--device` name (no `--output` given) throws `ArgumentException` before any device is created;
an unavailable resolved real device throws `InvalidOperationException` suggesting `--output` as
an alternative. Every scenario here is exercised against `FakePlaybackDeviceSource`, never a real
`AudioDeviceFactory`, so results do not depend on whether real playback hardware happens to be
present on the machine running the test.

**Requirement coverage**: `SpeechCli-SynthesisCommands-OutputDispatch`.

#### AudioDeviceFactoryPlaybackDeviceSource (Real AudioDeviceFactory Forwarding)

**Tests**: `AudioDeviceFactoryPlaybackDeviceSource_PlaybackProbe_ForwardsToFactory`,
`AudioDeviceFactoryPlaybackDeviceSource_CreatePlaybackDevice_ForwardsToFactory`,
`AudioDeviceFactoryPlaybackDeviceSource_NullFactory_ThrowsArgumentNullException`

**Scenario/Expected**: Against a real, composed `AudioDeviceFactory`, `PlaybackProbe` returns the
exact same probe instance the factory itself exposes, and `CreatePlaybackDevice` (for both the
default selection and a named selection) returns a result of the same type and
`IsAvailable`-ness as calling the factory directly - proving the adapter is a pure pass-through
that does not alter `AudioDeviceFactory`'s own hardware-detection behavior in any way, without
asserting on whether real playback hardware happens to be present on the machine running the
test; a `null` factory is rejected with `ArgumentNullException`.

**Requirement coverage**: `SpeechCli-SynthesisCommands-OutputDispatch`.

#### Cancellation and Disposal Ordering

**Tests**: `SpeakCommand_RunAsync_Canceled_ReportsErrorCleanly`,
`SpeakCommand_RunAsync_Success_DisposesSynthesizerOnce`

**Scenario/Expected**: A `FakeSpeechSynthesizer` configured to throw `OperationCanceledException`
from `SpeakAsync` results in a clean, one-line cancellation message rather than an unhandled
exception propagating out of `RunAsync`; a successful run disposes the synthesizer exactly once,
proving the synthesizer-before-device disposal ordering never double-disposes or skips disposal.

**Requirement coverage**: `SpeechCli-SynthesisCommands-CancellationAndDisposal`.

#### ICliModelCatalog Seam Extension (Real Catalog)

**Tests**: `SpeechModelCatalogAdapter_GetPreferredAudioFormat_RecognitionRoleModel_ThrowsArgumentException`,
`SpeechModelCatalogAdapter_GetPreferredAudioFormat_NullDescriptor_ThrowsArgumentNullException`,
`SpeechModelCatalogAdapter_CreateSynthesizer_RecognitionRoleModel_ThrowsArgumentException`,
`SpeechModelCatalogAdapter_CreateSynthesizer_NullDescriptor_ThrowsArgumentNullException`,
`SpeechModelCatalogAdapter_CreateSynthesizer_NullPlaybackDevice_ThrowsArgumentNullException`

**Scenario/Expected**: Against a real, temp-directory-rooted `SpeechModelCatalogAdapter`, calling
either new seam member with a real, compiled-in recognition-role model's descriptor throws
`ArgumentException` naming the `descriptor` parameter, proving the adapter's `is ISynthesisModel`
cast genuinely rejects a non-synthesis model rather than only being exercised through a fake; a
`null` descriptor or playback device is rejected with `ArgumentNullException` before the cast is
even attempted. No automated test exercises a real, downloaded synthesis-role model against these
two members - see _Manual / Build-Time Verification_ below.

**Requirement coverage**: `SpeechCli-SynthesisCommands-CatalogSeamExtension`.

#### Null Guards

**Tests**: `SpeakCommand_RunAsync_NullContext_ThrowsArgumentNullException`,
`SpeakCommand_RunAsync_NullCatalog_ThrowsArgumentNullException`,
`SpeakCommand_RunAsync_NullFactory_ThrowsArgumentNullException`,
`SpeakCommand_Run_NullContext_ThrowsArgumentNullException`,
`ParameterBagParser_Resolve_NullRawValues_ThrowsArgumentNullException`,
`ParameterBagParser_Resolve_NullDeclaredParameters_ThrowsArgumentNullException`

**Scenario/Expected**: Every entry point rejects a `null` context, catalog, factory, or argument
list immediately with `ArgumentNullException`, proving no command silently proceeds with a
missing dependency.

**Requirement coverage**: `SpeechCli-SynthesisCommands-NullGuards`.

### Requirements Coverage

- **`SpeechCli-SynthesisCommands-Speak`**: see _SpeakCommand — Text Source, Model Resolution, and
  Argument Parsing_ above
- **`SpeechCli-SynthesisCommands-ParamValidation`**: see _ParameterBagParser and `--param`
  Validation_ above
- **`SpeechCli-SynthesisCommands-NoTags`**: see _`--no-tags` Stripping_ above
- **`SpeechCli-SynthesisCommands-OutputDispatch`**: see _`--output` Versus Real Device Dispatch_
  above
- **`SpeechCli-SynthesisCommands-CancellationAndDisposal`**: see _Cancellation and Disposal
  Ordering_ above
- **`SpeechCli-SynthesisCommands-CatalogSeamExtension`**: see _ICliModelCatalog Seam Extension
  (Real Catalog)_ above
- **`SpeechCli-SynthesisCommands-NullGuards`**: see _Null Guards_ above

### Acceptance Criteria

A SynthesisCommandSubsystem test run passes when: `speak` correctly enforces text-source mutual
exclusion and reports actionable model-resolution errors; `--param` values are validated against
each declared parameter kind's own constraints, rejecting an unrecognized key; `--no-tags` strips
recognized tags exactly, leaving other text byte-for-byte unchanged; `--output` and real-device
dispatch each construct the correct device kind and reject an unknown/unavailable device; a
cancellation is reported cleanly and the synthesizer is always disposed before the playback
device; the two new `ICliModelCatalog` seam members correctly reject a non-synthesis model against
a real catalog; and every entry point rejects a missing required dependency.

## Manual / Build-Time Verification

The following is verified manually, rather than by an automated test, because it requires a real,
downloaded text-to-speech model and this environment/CI has no cached TTS model available (per
this pass's planning report, Assumption 4):

- `dotnet run --project src/DemaConsulting.Speech.Cli -- speak --model <id> --text "hello world"
  --output <path>.wav`, run against a real, downloaded synthesis model on a development machine
  with network access, produces a valid, non-trivial WAV file at the given path (correct RIFF
  header, non-zero sample count, audible speech on playback)
- The same invocation without `--output`, on a machine with real audio playback hardware, plays
  audible synthesized speech through the resolved (default or `--device`-selected) device
