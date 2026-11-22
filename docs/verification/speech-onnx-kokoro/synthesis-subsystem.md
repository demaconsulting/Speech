## SynthesisSubsystem Verification

### Verification Approach

The SpeechOnnxKokoro SynthesisSubsystem contains two units: `KokoroPhonemePipeline`, which turns the text of a
segment (with the annotations of the English text analysis when there are any) into Kokoro's phoneme token ids,
and `OnnxKokoroSynthesisEngine`, the real, ONNX-Runtime-backed implementation of the Speech library's public
`ISynthesisBackend` and `IAnnotatedSynthesisBackend` seams over Kokoro v1.0's bare ONNX graph.

The units are verified by their own tests (see the unit verification documents): `KokoroPhonemePipelineTests`
runs the real analysis, the real phonemizer and renderer and Kokoro's real vocabulary against a golden corpus with
no ONNX model, and `OnnxKokoroSynthesisEngineTests` runs the engine over a tiny, hand-built ONNX fixture
(`TestData/fake-kokoro-model.onnx`, whose single node forwards its `style` input straight through as the output).
The subsystem-level tests (`SynthesisSubsystem_*`, in the same class as the engine tests, the engine being the way
into the subsystem) drive the engine together with the real pipeline: the fixture returns the style row the engine
selected, and the row is indexed by the number of phoneme tokens, so the audio samples show how many sounds the
pipeline produced. They check the behavior that only the two units together have: text with no sound gives empty
audio, a word absent from the dictionary is still spoken, the annotations of a segment change what is spoken (a
replacement, the reading of a heteronym), and a long segment is spoken in several runs of the graph.

One additional test, `Generate_RealInstalledModel_ProducesNonEmptyAudio`, exercises the real production Kokoro
model end-to-end - through `OnnxKokoroEnglishSynthesisModel.CreateBackend` and `ISynthesisBackend.Generate`
together - producing real, non-empty 24000 Hz audio with audible (not near-zero) samples for known English text. It
skips (rather than fails) when the real model is not installed in the running environment (checking every one of the
30 vendor files of its bundle, not merely a non-empty directory), mirroring this repository's established
SpeechSherpa real-model-skip convention. The system tests (`SpeechOnnxKokoroTests`) repeat these behaviors through
the public API only (see *SpeechOnnxKokoro System Verification*).

All synthesis policy above the seam - Layer 2 rendering, chunking, the session state machine, cancellation, and fault
containment - remains fully verified in the Speech library against a fake backend (see *Speech SynthesisSubsystem
Verification*), and this model's engine configuration is verified in *SpeechOnnxKokoro ModelManagementSubsystem
Verification*.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Onnx.Kokoro.Tests`
- **Dependencies**: no network access and no physical audio hardware for the fixture-based tests; one test
  additionally depends on the real, already-installed Kokoro model, self-skipping when it is not installed
- **Isolation**: the pipeline is immutable and shared by the tests of its class; every engine test creates its own
  engine and session over the read-only fixture file

### Acceptance Criteria

This subsystem is accepted when: text with no sound (empty, an emoji) gives empty audio at 24000 Hz without running
the graph; a word absent from the dictionary adds sounds (it is spoken, not dropped); a replacement annotation is
spoken instead of the written text and the adjective reading of `separate` is chosen when the annotations say so; a
text of 40 sentences is spoken in at least three runs of the graph; the pipeline's 41 golden sentences give exactly
their reviewed phonemes and token ids; the engine's constructor throws `ArgumentNullException` for each null
required dependency and `ArgumentException` for an empty voice-styles dictionary; `SampleRate` always reports
24000 Hz; `Generate` throws `ArgumentNullException` for null text and `ObjectDisposedException` after disposal; a
second `Dispose` call is a no-op; `RunProbeInference` runs successfully against a runnable session; and, when the
real production model is installed, the real engine produces audible 24000 Hz audio for known English text.

### Subsystem-Level Test Scenarios

#### Text without sounds gives empty audio

**Test**: `SynthesisSubsystem_TextWithoutSounds_ReturnsEmptyAudio` (a `[Theory]` over empty text and an emoji)

The engine returns an empty `BackendAudio` at 24000 Hz and the graph is not run.

#### A word absent from the dictionary is still spoken

**Test**: `SynthesisSubsystem_UnknownWord_IsStillSpoken`

A made-up 22-letter word produces audio: the word is pronounced from its spelling.

#### Annotations change what is spoken

**Tests**: `SynthesisSubsystem_AnnotatedReplacement_IsSpokenAsReplaced`,
`SynthesisSubsystem_AnnotatedHeteronym_IsSpokenWithTheChosenReading`

A replacement annotation `twenty twenty-six` for `2026` makes the segment longer than the plain call (the style row
selected by the token count is a later one); the annotations of the model's own analysis of `A separate room.` make
it shorter than the plain call, which speaks the default (verb) reading.

#### A long segment is spoken in parts

**Test**: `SynthesisSubsystem_TextBeyondTokenLimit_IsSpokenInSeveralParts`

Forty copies of one sentence (about 1,000 tokens) are spoken in at least three runs of the graph (the fixture
returns one style row per run, so the audio length is a multiple of 256 and at least three rows long).

### Unit-Level Test Scenarios

See the units' own verification documents: `kokoro-phoneme-pipeline.md` and `onnx-kokoro-synthesis-engine.md`.
