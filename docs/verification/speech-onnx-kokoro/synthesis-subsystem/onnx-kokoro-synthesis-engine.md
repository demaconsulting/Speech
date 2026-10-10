### OnnxKokoroSynthesisEngine Verification

#### Verification Approach

`OnnxKokoroSynthesisEngine` is verified by automated unit tests in
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests/SynthesisSubsystem/OnnxKokoroSynthesisEngineTests.cs`,
proving its constructor argument validation, `ISynthesisBackend.Generate`'s zero-token
short-circuit and disposed-engine guard, and `RunProbeInference`'s successful-probe path - all
against a tiny, hand-built ONNX test fixture (`TestData/fake-kokoro-model.onnx`) whose single node
forwards its `style` input straight through as the output, never the real ~163 MiB production
model. `OnnxKokoroEnglishSynthesisModelTests` already proves `Generate`'s voice-style-vector
selection and tensor-wiring end-to-end (via `CreateBackend`), so this class focuses on behavior
specific to this engine class itself, reachable only through its own internal constructor.

One test, `Generate_RealInstalledModel_ProducesNonEmptyAudio`, additionally exercises the real
production model end-to-end - through `OnnxKokoroEnglishSynthesisModel.CreateBackend` and
`Generate` together - synthesizing non-empty, real 24000 Hz audio for known English text. It is
guarded by `Assert.Skip` and skips (rather than fails) when the real model is not installed in
this environment: it checks that every one of the model's 30 declared download files exists under
the default installed-model directory, not merely that the directory itself is non-empty, so a
partial/interrupted real install still skips rather than failing with a misleading error. This
mirrors this repository's established Sherpa real-model-skip convention (see, for example,
_SpeechSherpa SynthesisSubsystem Verification_ and _SherpaOnnxRecognitionEngine Verification_,
whose own real-model tests self-skip the same way). Following that same Sherpa precedent, this
test is intentionally **not** linked in
`docs/reqstream/speech-onnx-kokoro/synthesis-subsystem/onnx-kokoro-synthesis-engine.yaml`: the CI
model-download bootstrap (`tools/ModelDownloader`) does not provision this package's model, so the
test would always skip in CI and could never serve as enforceable `--enforce` evidence there. It
still runs and self-reports whenever a developer happens to have the real model installed
locally, per `requirements-principles.md`'s "Tests MAY exist without a requirement" rule.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.Kokoro.Tests`
- **Isolation**: every fixture-based test uses the same small, read-only `.onnx` fixture file
  checked into the test project; no network access or physical audio hardware is required
- **Real-model dependency**: `Generate_RealInstalledModel_ProducesNonEmptyAudio` additionally
  depends on the real Kokoro model being installed under this user's default installed-model
  directory (`%LOCALAPPDATA%\DemaConsulting.Speech\Models\kokoro-onnx-v1_0-en\current` on
  Windows), self-skipping when it is not

#### Acceptance Criteria

The constructor throws `ArgumentNullException` for each individually-null required parameter
(session, vocabulary, phonemizer, voice-styles dictionary) and `ArgumentException` for an empty
voice-styles dictionary; `SampleRate` always reports 24000 Hz; `Generate` returns an empty
`EngineAudio` at 24000 Hz without running the ONNX graph for text that phonemizes to zero tokens
(empty text, or text composed entirely of out-of-vocabulary words); `Generate` throws
`ArgumentNullException` for null text and `ObjectDisposedException` once the engine has been
disposed; a second `Dispose` call is a harmless no-op; `RunProbeInference` runs its representative
inference successfully, without throwing, against a session that can actually run the graph; and,
when the real production model is installed in this environment, `CreateBackend` plus `Generate`
together synthesize non-empty audio at 24000 Hz for a real English sentence.

#### Test Scenarios

##### The constructor throws ArgumentNullException for a null session

**Test**: `Constructor_NullSession_ThrowsArgumentNullException`

##### The constructor throws ArgumentNullException for a null vocabulary

**Test**: `Constructor_NullVocabulary_ThrowsArgumentNullException`

##### The constructor throws ArgumentNullException for a null phonemizer

**Test**: `Constructor_NullPhonemizer_ThrowsArgumentNullException`

##### The constructor throws ArgumentNullException for a null voice-styles dictionary

**Test**: `Constructor_NullVoiceStyles_ThrowsArgumentNullException`

##### The constructor throws ArgumentException for an empty voice-styles dictionary

**Test**: `Constructor_EmptyVoiceStyles_ThrowsArgumentException`

##### SampleRate is always 24000 Hz

**Test**: `SampleRate_IsAlways24000`

##### Generate short-circuits to empty audio for zero-token text

**Test**: `Generate_ZeroTokenText_ReturnsEmptyAudio` (a `[Theory]` covering empty text and
text composed entirely of out-of-vocabulary words)

##### Generate throws ArgumentNullException for null text

**Test**: `Generate_NullText_ThrowsArgumentNullException`

##### Generate throws ObjectDisposedException after disposal

**Test**: `Generate_AfterDispose_ThrowsObjectDisposedException`

##### Disposing twice does not throw

**Test**: `Dispose_CalledTwice_DoesNotThrow`

##### RunProbeInference runs successfully against a runnable session

**Test**: `RunProbeInference_RunnableSession_DoesNotThrow`

##### The real, installed production model synthesizes non-empty audio (skipped if not installed)

**Test**: `Generate_RealInstalledModel_ProducesNonEmptyAudio`

Skipped via `Assert.Skip` unless every one of the real model's 30 declared download files is
found installed under this environment's default installed-model directory. When installed,
constructs the real `OnnxKokoroEnglishSynthesisModel`'s backend and calls `Generate` with "The
quick brown fox jumps over the lazy dog.", asserting the returned audio is non-empty at 24000 Hz.
