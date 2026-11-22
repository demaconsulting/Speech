# SpeechOnnxNemotronStt System Verification Design

This document describes the system-level verification strategy for the SpeechOnnxNemotronStt library.

## Verification Approach

SpeechOnnxNemotronStt is verified at system level in two ways, both in the
`test/DemaConsulting.Speech.Onnx.NemotronStt.Tests` project:

- **Catalog integration**: the one public entry point, the `AddNemotronSttModels()` extension
  method, is exercised against a real Speech `SpeechModelCatalog` exactly as a host composes it.
  The tests prove the method returns the same catalog instance for chaining, registers exactly the
  one shipped recognition model, and accepts a list of preferred execution provider names.
- **Real-model system tests** (`SpeechOnnxNemotronSttTests.cs`): the real, installed Nemotron 3.5
  streaming model is loaded the way an application loads it - from the default per-user
  `SpeechModelStore` through `IRecognitionModel.CreateBackend` - and real recordings are streamed
  through the public `IRecognitionBackend` surface (`AcceptSamples`, `TryDecode`, `TryFlush`,
  `Reset`) in 20 ms blocks, as a recognition session does. The tests measure accuracy,
  segmentation, and the handling of silence, pauses, and session boundaries. Because the model is
  int4-quantized and ONNX Runtime may choose different kernels per CPU, they assert word error rate
  thresholds, result counts, result positions on the audio clock, and the presence or absence of
  words, never exact transcripts. They run one at a time in one xUnit collection; a shared fixture
  holds only the verified data and the LibriSpeech-subset transcripts, and every test creates its
  own backend.

The real-model tests use the shared evaluation helpers of the speech-to-text evaluation tool
(`tools/DemaConsulting.Speech.SttEvaluation/`), compiled into the test project: `SttDataSet` loads
and checksum-verifies the data, `SttTranscriber` streams audio and records each result with its
audio position, `WordErrorRateCalculator` aligns a transcript against its reference (word error
rate, insertions, deletions, and invented words after the last reference word), and
`SttEvaluationGates` holds the accuracy thresholds. The helpers are unit-tested in the same
project (`Evaluation/WordErrorRateCalculatorTests.cs`, `SttDataSetTests.cs`,
`SttTranscriberTests.cs`). The evaluation tool's `--check` option applies the same gates to the
same data as a local validation command; its measured results, including the sweep that chose
the default blank penalty, are recorded in `docs/evaluation/nemotron-stt/README.md`.

Below the system level, `OnnxNemotronMultilingualRecognitionModel`'s identity, license, 11-file
download descriptor, language parameter, and `CreateBackend` validation, plus the owned
`NemotronVocabulary`, are verified deterministically with no network access and no real model
files (see *SpeechOnnxNemotronStt ModelManagementSubsystem Verification*). The streaming
pipeline - `SilenceRunLimiter`, `DitherNoise`, `NemotronFeatureExtractor`,
`NemotronRnntGreedyDecoder`, `NemotronEndpointDetector`, and `OnnxNemotronRecognitionEngine` - is
verified against managed fakes of the encoder and prediction networks, and the ONNX wiring of
`NemotronEncoder` and `NemotronRnntNetwork` against tiny synthetic ONNX models, so the unit tests
never depend on model files (see *SpeechOnnxNemotronStt RecognitionSubsystem Verification*).

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline on Windows, Linux, and
  macOS
- **Real model in CI**: the CI build job downloads the model
  `nemotron-3.5-asr-streaming-0.6b-onnx-int4` (about 790 MB) with `tools/ModelDownloader` into
  the default per-user model store on each operating system and caches it per operating system.
  A failed download fails the job, so the real-model tests always run in CI and cannot silently
  skip there
- **Real model outside CI**: the real-model tests skip (not fail) when the model is not installed;
  it is installed with `dotnet run --project tools/ModelDownloader --
  nemotron-3.5-asr-streaming-0.6b-onnx-int4`
- **Target framework**: `SpeechOnnxNemotronSttTests.cs` is compiled for `net10.0` only; the engine
  logic is covered on every target framework by the fake-based unit tests, and decoding minutes of
  audio once per framework would only multiply the CI time
- **Test data**: the segregated folder `data/stt-en/`, copied beside the test assembly as
  `TestData/stt-en`: 24 utterances of the LibriSpeech `test-clean` set (16 kHz mono FLAC,
  CC BY 4.0) and the 50 s "Crossing the Bar" recitation (16 kHz mono WAV, CC0 1.0), with their
  attribution in `data/stt-en/ATTRIBUTION.md`. Every file is pinned by SHA-256 and size in
  `data/stt-en/MANIFEST.tsv` and verified before use; a mismatching file fails the test. The folder
  is used only by tests and developer tools and is never packed into a package
- **Accuracy gates**: `SttEvaluationGates` (6% for the LibriSpeech subset, 10% for the poem),
  shared with the evaluation tool
- **Execution provider**: CPU only; GPU execution providers are not exercised
- **Dependencies**: no physical audio hardware; no network access during the test run (the model
  download is a separate CI step)
- **Isolation**: each catalog-composing or model-loading test uses a fresh scratch directory or a
  fresh backend

## External Interface Simulation

The catalog tests use no simulation: the real Speech `SpeechModelCatalog` is populated through the
real `AddNemotronSttModels()` extension method. The real-model tests use no simulation either:
the audio comes from the committed recordings, from digital silence, from deterministic quiet
noise (uniform in -0.03 to 0.03 from a fixed seed), or from recordings attenuated by 20 dB. The
ONNX encoder and the prediction networks are simulated only at unit level, by managed fakes
(`Fakes.cs`).

## Acceptance Criteria

A SpeechOnnxNemotronStt system-level test run passes when every scenario below passes without
unexpected exceptions and every subsystem-level suite passes. In CI no real-model test may be
skipped; outside CI a real-model test may skip only because the model is not installed. The
real-model scenarios pass when:

- the corpus word error rate over the 24 LibriSpeech-subset utterances is at most 6% and the word
  error rate of the poem is at most 10%;
- no LibriSpeech-subset utterance, and no utterance followed by 3 s of trailing silence, has any
  word after its last aligned reference word, and the trailing-silence utterance has exactly one
  final committed before the flush;
- four utterances separated by 1.5 to 2 s pauses yield exactly four finals, each committed during
  the pause after its utterance with a word error rate of at most 20% against that utterance;
- two phrases separated by a 1.5 s or 2.5 s gap yield exactly two finals, the second opening with
  the second phrase, no word repeated, and a word error rate of at most 20%;
- more than 30 s of continuous speech yields at least two finals, each at most 21.5 s (20 s plus
  one encoder chunk and the carried word) after the previous one, with a word error rate of at
  most 6%;
- 9 s of digital silence and quiet noise yields no result with text, and neither does a session
  of silence after a reset;
- a phrase after 2.5 s or 4 s of digital silence keeps its opening word with no insertions, and
  speech after 4 s of quiet noise has no deletions and no insertions, each with a word error rate
  of at most 20%;
- twelve LibriSpeech-subset utterances attenuated by 20 dB have a corpus word error rate of at most
  8%;
- after an endpoint, decoding and flushing with no new audio report no text, and flushing an
  utterance abandoned mid-speech returns a final result holding its words.

The automated boundary is stated honestly: catalog registration, model metadata, vocabulary,
feature extraction, decoding, endpointing, limiting, and engine behavior are covered against
fakes or synthetic models on every target framework, and recognition with the real model is
covered on CPU by the real-model tests; GPU execution and physical audio I/O are not automated.

## System-Level Test Scenarios

### Integration: AddNemotronSttModels Returns the Same Catalog Instance

**Test**: `AddNemotronSttModels_Called_ReturnsSameCatalogInstance`

Verifies that `AddNemotronSttModels()` returns the exact catalog instance it was called on.
Covers `SpeechOnnxNemotronStt-Models-AddNemotronSttModelsChaining`.

### Integration: AddNemotronSttModels Registers the One Shipped Model

**Test**: `AddNemotronSttModels_Called_RegistersOneRecognitionModel`

Verifies that the catalog enumeration contains exactly the one recognition model.
Covers `SpeechOnnxNemotronStt-Models-AddNemotronSttModels`.

### Integration: AddNemotronSttModels Accepts Execution Provider Names

**Test**: `AddNemotronSttModels_WithProviders_Registers`

Verifies that supplying preferred execution provider names still registers the model. Together
with the registration scenario above, this covers `SpeechOnnxNemotronStt-Models-AddNemotronSttModels`.

### Integration: the registered model is a real, downloadable recognition model

**Tests**: `Metadata_IsDeclared`, `OnnxNemotronMultilingualRecognitionModel_DownloadDescriptor_IsModelsReleaseBundle`

Covers `SpeechOnnxNemotronStt-Models-RecognitionModel`. Verifies the registered model declares its catalog identity,
audio format, and its license (`OpenMDW-1.1`, with the license URL `https://openmdw.ai/license/1-1/`), and downloads one
pinned bundle from the project's `models` release over HTTPS, whose eleven files each carry a checksum and their
original HTTPS URI. See *SpeechOnnxNemotronStt ModelManagementSubsystem Verification*.

### Integration: the host selects the recognition language

**Test**: `Parameters_LanguageChoice`

Covers `SpeechOnnxNemotronStt-Models-SelectableLanguage`. Verifies the model offers an en-US
(default) or en-GB language choice without a second set of model files.

### Integration: language and blank ids come from the model's vocabulary

**Tests**: `TryGetLanguageId_KnownLocales_ReturnsOrdinal`, `BlankId_IsBlankTokenIndex`

Covers `SpeechOnnxNemotronStt-Models-VocabularyDerivedIds`. Verifies the language id and blank
token id match the positions declared in the vocabulary rather than fixed values.

### Integration: emitted tokens become readable text

**Test**: `Detokenize_Tokens_NormalizesText`

Covers `SpeechOnnxNemotronStt-Models-ReadableTranscript`. Verifies token pieces and locale
markers are converted into normalized text.

### Integration: an unusable installation is rejected

**Tests**: `CreateBackend_EmptyDirectory_Throws`, `CreateBackend_MissingFiles_Throws`,
`CreateBackend_VocabularyWithoutLocale_Throws`

Covers `SpeechOnnxNemotronStt-Models-UnusableInstallationRejected`. Verifies backend creation
fails with an exception, rather than returning a partial recognizer, for an empty directory,
missing files, and a vocabulary lacking the selected locale.

### Integration: streamed audio is transcribed

**Tests**: `Extract_Chunked_EqualsOneShot`, `Decode_Tokens_AreCollectedAndAdvanced`,
`TryDecode_SpeechChunk_ReportsProvisionalText`

Covers `SpeechOnnxNemotronStt-Recognition-StreamingTranscription`. Verifies features are
chunk-invariant, tokens are decoded greedily, and an engine over the pipeline reports text.
See *SpeechOnnxNemotronStt RecognitionSubsystem Verification*; the real-model scenarios below
exercise the same pipeline end to end.

### Integration: provisional text is reported once per change

**Tests**: `TryDecode_SpeechChunk_ReportsProvisionalText`, `TryDecode_NoNewTokens_ReportsNothing`,
`TryDecode_QuietAfterShortSpeech_ReportsBeforeEndpoint`

Covers `SpeechOnnxNemotronStt-Recognition-ProvisionalText`. Verifies changed text is reported,
unchanged text is not, and the last words are reported before the endpoint.

### Integration: native model resources are released on disposal

**Test**: `Dispose_DisposesCollaboratorsAndBlocksUse`

Covers `SpeechOnnxNemotronStt-Recognition-ModelResourceRelease`. Verifies disposal releases the
encoder and decoder once and later use throws.

### Integration: speech samples survive limiting

**Tests**: `Process_SilenceThenTone_KeepsAllToneSamples`, `Process_VeryQuietSpeech_IsNotDropped`

Covers `SpeechOnnxNemotronStt-Recognition-SpeechPreservedWhenLimiting`. Verifies every speech
sample passes through the limiter.

### Integration: limited audio is dithered deterministically

**Tests**: `Apply_SameSeed_IsDeterministicAndResettable`, `Apply_Statistics_MatchAmplitude`

Covers `SpeechOnnxNemotronStt-Recognition-LeadingSilenceDithering`. Verifies the dither is
reproducible and its standard deviation is within 10% of the configured amplitude.

### Real model: LibriSpeech-subset accuracy is within the gate

**Scenario**: each of the 24 LibriSpeech-subset utterances is streamed on its own through one
backend that is reset before every utterance, and flushed at its end.

**Expected**: the corpus word error rate (all errors over all reference words) is at most 6%.

**Test**: `SpeechOnnxNemotronStt_Transcribe_LibriSpeechSubset_WordErrorRateWithinGate`

Covers `SpeechOnnxNemotronStt-Recognition-Accuracy`.

### Real model: poem accuracy is within the gate

**Scenario**: the complete 50 s "Crossing the Bar" recitation is streamed as one recording with its
natural pauses.

**Expected**: the word error rate of the joined transcript is at most 10%.

**Test**: `SpeechOnnxNemotronStt_Transcribe_CrossingTheBar_WordErrorRateWithinGate`

Covers `SpeechOnnxNemotronStt-Recognition-Accuracy`.

### Real model: no words are invented at the end of input

**Scenario**: the LibriSpeech-subset transcripts above are aligned against their references; in
addition, two utterances that showed tail hallucinations before silence padding are each followed
by 3 s of digital silence and then flushed.

**Expected**: no transcript has a word after its last aligned reference word; each trailing-silence
utterance has exactly one final, committed during the silence rather than by the flush.

**Tests**: `SpeechOnnxNemotronStt_Transcribe_LibriSpeechSubset_AddsNoInventedWords`,
`SpeechOnnxNemotronStt_Transcribe_TrailingSilenceThenFlush_AddsNoInventedWords`

Covers `SpeechOnnxNemotronStt-Recognition-NoInventedWords` and, for the final committed during
the silence, `SpeechOnnxNemotronStt-Recognition-FinalText`.

### Real model: each paused utterance is finalized on its own

**Scenario**: four LibriSpeech-subset utterances without internal pauses of 0.6 s or more are
joined with silent gaps of 1.5, 2.0, 1.75, and 2.0 s and streamed as one recording.

**Expected**: exactly four finals; each is committed between the end of its utterance and the
start of the next, and has a word error rate of at most 20% against that utterance alone.

**Test**: `SpeechOnnxNemotronStt_Transcribe_PausedUtterances_FinalizesEachUtterance`

Covers `SpeechOnnxNemotronStt-Recognition-UtteranceSegmentation`.

### Real model: speech resuming after an endpoint keeps every word once

**Scenario**: the poem's first phrase, a silent gap of 1.5 s or 2.5 s (each beyond the endpoint
rule), then the poem's second phrase. Separately, the poem's opening up to a natural pause plus
3 s of silence is fed without a flush, and decoding and flushing are then requested with no new
audio.

**Expected**: exactly two finals, the second opening with the second phrase ("may" among its first
three words); "call", "may", and "moaning" each occur exactly once; the word error rate is at most
20%. After the endpoint, neither decoding nor flushing reports text.

**Tests**: `SpeechOnnxNemotronStt_Transcribe_SpeechResumingAfterEndpoint_KeepsEveryWordExactlyOnce`,
`SpeechOnnxNemotronStt_TryDecodeAndTryFlush_NoNewAudioAfterEndpoint_ReportNothing`

Covers `SpeechOnnxNemotronStt-Recognition-ResumedSpeechKeepsWords`.

### Real model: continuous speech is split at the maximum utterance length

**Scenario**: four unpaused LibriSpeech-subset utterances, their quiet ends trimmed to 100 ms, are
joined into more than 30 s of continuous speech.

**Expected**: at least two finals; each final, the flushed last one included, is committed at most
21.5 s after the previous one; the word error rate of the joined transcript is at most 6%.

**Test**: `SpeechOnnxNemotronStt_Transcribe_ContinuousSpeechBeyondMaximum_FinalizesAtMostEveryTwentySeconds`

Covers `SpeechOnnxNemotronStt-Recognition-MaximumUtteranceLength`.

### Real model: silence alone produces no text

**Scenario**: 3 s of digital silence, 3 s of quiet noise, and 3 s of digital silence are streamed
and flushed, long enough to reach every endpoint rule.

**Expected**: no result of any kind, provisional or final, carries text.

**Test**: `SpeechOnnxNemotronStt_Transcribe_SilenceOnly_ReportsNoText`

Covers `SpeechOnnxNemotronStt-Recognition-SilenceProducesNoText`.

### Real model: opening words survive leading silence and quiet noise

**Scenario**: the poem's first phrase after 2.5 s or 4.0 s of digital silence (2.5 s lets the long
trailing-silence rule fire just before the speech begins); separately, the poem's opening up to a
natural pause after 4 s of quiet noise.

**Expected**: after digital silence, "sunset" is among the first two words, there are no
insertions, and the word error rate is at most 20%. After quiet noise, there are no deletions and
no insertions, the transcript continues "and evening star" after its first word, and the word
error rate is at most 20%.

**Tests**: `SpeechOnnxNemotronStt_Transcribe_LeadingSilenceBeforeSpeech_KeepsPhraseWithoutStrayWords`,
`SpeechOnnxNemotronStt_Transcribe_QuietNoiseBeforeSpeech_KeepsOpeningWords`

Covers `SpeechOnnxNemotronStt-Recognition-LeadingSilenceMitigation`.

### Real model: quiet speech is recognized

**Scenario**: every other LibriSpeech-subset utterance (twelve, both speaker genders) is attenuated
by 20 dB and streamed on its own.

**Expected**: the corpus word error rate is at most 8% (the value measured at the shipped defaults
plus a margin for CPU-dependent differences of the int4 model).

**Test**: `SpeechOnnxNemotronStt_Transcribe_QuietSpeech_RecoversWords`

Covers `SpeechOnnxNemotronStt-Recognition-QuietWordRecovery`.

### Real model: an abandoned utterance is recovered on flush

**Scenario**: the poem's first 4.5 s are fed with no trailing silence, as when a push-to-talk
button is released mid-sentence, and the backend is then flushed.

**Expected**: the flush returns a final result that holds "sunset", "evening", and "star".

**Test**: `SpeechOnnxNemotronStt_TryFlush_AbandonedUtterance_RecoversTrailingWords`

Covers `SpeechOnnxNemotronStt-Recognition-FinalText`.

### Real model: a reset session does not inherit an abandoned utterance

**Scenario**: the poem's first 3 s are fed and left mid-utterance; the backend is reset and a new
session of 1.5 s of silence is streamed and flushed.

**Expected**: the new session reports no text at all.

**Test**: `SpeechOnnxNemotronStt_Reset_AbandonedUtterance_DoesNotBleedIntoNextSession`

Covers `SpeechOnnxNemotronStt-Recognition-SessionIsolation`.

See *SpeechOnnxNemotronStt RecognitionSubsystem Verification* for the endpoint, padding, feature,
decoding, limiting, and dither scenarios these system scenarios decompose into.
