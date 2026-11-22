### OnnxKokoroSynthesisEngine Verification

#### Verification Approach

`OnnxKokoroSynthesisEngine` is verified by automated unit tests in
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests/SynthesisSubsystem/OnnxKokoroSynthesisEngineTests.cs`,
proving its constructor argument validation, the zero-token short-circuit of `ISynthesisBackend.Generate` and the
disposed-engine guard, the annotated generation, the generation in parts, the style-row selection, and
`RunProbeInference`'s successful-probe path - all against a tiny, hand-built ONNX test fixture
(`TestData/fake-kokoro-model.onnx`) whose single node forwards its `style` input straight through as the output,
never the real ~88 MiB production model. The engine takes its token ids from the real `KokoroPhonemePipeline`
(verified by its own tests), and the fixture returns the style row the engine selected - the row is indexed by the
number of phoneme tokens - so a test can read from the audio samples how many sounds were spoken and which row (and
so which voice) was used. Tests whose subject is this behavior of the engine together with the pipeline carry the
prefix `SynthesisSubsystem_` (they are also the subsystem-level tests). `OnnxKokoroEnglishSynthesisModelTests` proves
`Generate`'s voice-style-vector selection and tensor-wiring end-to-end (via `CreateBackend`), so this class focuses on
behavior specific to this engine class itself, reachable only through its own internal constructor.

One test, `Generate_RealInstalledModel_ProducesNonEmptyAudio`, additionally exercises the real
production model end-to-end - through `OnnxKokoroEnglishSynthesisModel.CreateBackend` and
`Generate` together - synthesizing non-empty, real 24000 Hz audio for known English text. It is
guarded by `Assert.Skip` and skips (rather than fails) when the real model is not installed in
this environment: it checks that every one of the 30 vendor files of the model's bundle exists under
the default installed-model directory, not merely that the directory itself is non-empty, so a
partial/interrupted real install still skips rather than failing with a misleading error. This
mirrors this repository's established Sherpa real-model-skip convention (see, for example,
*SpeechSherpa SynthesisSubsystem Verification* and *SherpaOnnxRecognitionEngine Verification*,
whose own real-model tests self-skip the same way). Following that same Sherpa precedent, this
test is intentionally **not** linked in
`docs/reqstream/speech-onnx-kokoro/synthesis-subsystem/onnx-kokoro-synthesis-engine.yaml`, per
`requirements-principles.md`'s "Tests MAY exist without a requirement" rule. The CI model-download bootstrap
(`tools/ModelDownloader`) provisions this package's model from its bundle on the project's `models` release, so
the test runs in CI on every operating system, and it runs whenever a developer has the real model installed locally.

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
(session, phoneme pipeline, voice-styles dictionary) and `ArgumentException` for an empty
voice-styles dictionary; `SampleRate` always reports 24000 Hz; `Generate` returns an empty
`BackendAudio` at 24000 Hz, without failing, for text with no sound in this voice (empty text, an emoji);
a word absent from the dictionary is spoken; `GenerateAnnotated` speaks a replacement annotation instead of
the written text and the reading the annotations of the model's analysis chose; a text longer than the model
accepts is spoken in several runs of the graph; `Generate` throws
`ArgumentNullException` for null text and `ObjectDisposedException` once the engine has been
disposed; a second `Dispose` call is a harmless no-op, concurrent calls release the session once, and a disposal that
overlaps a running synthesis waits for it; `RunProbeInference` runs its representative
inference successfully, without throwing, against a session that can actually run the graph; and,
when the real production model is installed in this environment, `CreateBackend` plus `Generate`
together synthesize non-empty audio at 24000 Hz for a real English sentence.

#### Test Scenarios

##### The constructor throws ArgumentNullException for a null session

**Test**: `Constructor_NullSession_ThrowsArgumentNullException`

##### The constructor throws ArgumentNullException for a null phoneme pipeline

**Test**: `Constructor_NullPipeline_ThrowsArgumentNullException`

##### The constructor throws ArgumentNullException for a null voice-styles dictionary

**Test**: `Constructor_NullVoiceStyles_ThrowsArgumentNullException`

##### The constructor throws ArgumentException for an empty voice-styles dictionary

**Test**: `Constructor_EmptyVoiceStyles_ThrowsArgumentException`

##### SampleRate is always 24000 Hz

**Test**: `SampleRate_IsAlways24000`

##### Generate returns empty audio for text without sounds

**Test**: `SynthesisSubsystem_TextWithoutSounds_ReturnsEmptyAudio` (a `[Theory]` covering empty text and
an emoji)

##### Generate speaks a word that is absent from the dictionary

**Test**: `SynthesisSubsystem_UnknownWord_IsStillSpoken`

A made-up 22-letter word through the plain `Generate` produces audio.

##### GenerateAnnotated speaks the replacement and the chosen reading

**Tests**: `SynthesisSubsystem_AnnotatedReplacement_IsSpokenAsReplaced`,
`SynthesisSubsystem_AnnotatedHeteronym_IsSpokenWithTheChosenReading`

With a voice whose rows are filled with their own row index, the first sample is the number of phoneme tokens of the
utterance. A replacement annotation (`twenty twenty-six` for `2026`) gives more tokens than the plain call; the
annotations of the model's analysis of `A separate room.` give fewer tokens than the plain call, which speaks the
default reading.

##### A long segment is spoken in parts, one run of the graph each

**Test**: `SynthesisSubsystem_TextBeyondTokenLimit_IsSpokenInSeveralParts`

The fixture returns one 256-sample style row per run, so forty copies of a sentence (about 1,000 tokens) give a
length that is a multiple of 256 and at least three rows long.

##### A multi-row voice selects its style row by token count

**Scenario**: a voice style file with more rows than the utterance's phoneme-token count is used
to synthesize an utterance.

**Expected**: the row indexed by the token count is passed to the model.

**Test**: `Generate_MultiRowVoice_SelectsRowByTokenCount`

##### A short voice clamps to its last style row

**Scenario**: an utterance's phoneme-token count is not smaller than the voice's row count.

**Expected**: the voice's last row is passed to the model.

**Test**: `Generate_ShortVoice_ClampsToLastRow`

##### An unrecognized speaker id falls back to the first voice

**Test**: `Generate_UnknownSpeakerId_FallsBackToFirstVoice`

##### Disposing the engine releases the session

**Tests**: `Dispose_ReleasesSession`, `Dispose_WhileSynthesisRuns_WaitsForTheRunAndReleasesTheSessionOnce`

The second test holds one synthesis inside the engine (through an annotation list, a public input of the engine that
blocks until released), starts two disposals, waits until a new synthesis is refused (which shows the disposal has
begun), and checks that the session has not been disposed yet, that the held synthesis still returns audio once
released, and that the session was disposed exactly once (counted through
`DisposalTrackingInferenceSession.DisposeCount`).

##### Generate throws ArgumentNullException for null text

**Test**: `Generate_NullText_ThrowsArgumentNullException`

##### Generate throws ObjectDisposedException after disposal

**Tests**: `Generate_AfterDispose_ThrowsObjectDisposedException`,
`Generate_ConcurrentWithDispose_CompletesOrThrowsObjectDisposed`

The race test runs eight workers of 25 syntheses against one disposal and checks that every outcome is audio or
`ObjectDisposedException`.

##### Disposing twice does not throw

**Tests**: `Dispose_CalledTwice_DoesNotThrow`, `Dispose_ConcurrentCallers_ReleaseTheSessionOnce`

##### RunProbeInference runs successfully against a runnable session

**Test**: `RunProbeInference_RunnableSession_DoesNotThrow`

##### The real, installed production model synthesizes non-empty audio (skipped if not installed)

**Test**: `Generate_RealInstalledModel_ProducesNonEmptyAudio`

Skipped via `Assert.Skip` unless every one of the real model's 30 declared download files is
found installed under this environment's default installed-model directory. When installed,
constructs the real `OnnxKokoroEnglishSynthesisModel`'s backend and calls `Generate` with "The
quick brown fox jumps over the lazy dog.", asserting the returned audio is non-empty at 24000 Hz.
