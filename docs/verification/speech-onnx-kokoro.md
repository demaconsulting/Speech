# SpeechOnnxKokoro System Verification Design

This document describes the system-level verification strategy for the SpeechOnnxKokoro library.

## Verification Approach

SpeechOnnxKokoro is verified at system level through its public API, by the system tests in
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests/SpeechOnnxKokoroTests.cs`, and by a FileAssert test of the built
package. The system tests compose the library the way a host does: a real Speech `SpeechModelCatalog` populated
through `AddKokoroModels()`, and the real `OnnxKokoroEnglishSynthesisModel` loaded with
`SpeechSynthesizerFactory.LoadAsync` from an installed directory into a speech synthesizer engine whose
`SynthesizeAsync` speaks the text. Everything above the ONNX graph is real: the Speech library's session and
chunking, the English text analysis of SpeechTextEn, the phonemizer and the Kokoro renderer of SpeechPhonemesEn,
Kokoro's own vocabulary and the engine.

The one thing that is not real is the model: the installed directory holds a tiny, hand-built ONNX graph
(`TestData/fake-kokoro-model.onnx`) whose single node returns its `style` input as the audio, and synthetic voice
files in which every row of voice `v` holds `v * 1000 + row`. Every run of the graph therefore returns 256 samples
that all equal the voice's marker plus the number of phoneme tokens of that run (the style row is chosen by the
token count). That makes two things observable through the public API without a model of 88 MiB: which voice
spoke, and how many sounds were spoken - so a test can show that a reading, a spoken number, a word absent from
the dictionary or a long text changes what the model is asked to say, and that the right voice was used. The
phonemes themselves are never mocked.

Below the system level, `OnnxKokoroEnglishSynthesisModel`'s identity, 30-file download descriptor, speaker-id
resolution and text analysis are verified deterministically with no network access (see *SpeechOnnxKokoro
ModelManagementSubsystem Verification*), `KokoroPhonemePipeline` against a golden corpus of 40 sentences, and
`OnnxKokoroSynthesisEngine` against the same fixture graph (see *SpeechOnnxKokoro SynthesisSubsystem
Verification*).

Automated coverage **does not** extend to the audible quality of speech from the real ~88 MiB production model,
or to playing or capturing audio through real hardware. The audible quality - in particular the choice of weak
forms for function words, which was made on measurement against Kokoro's reference phonemizer and not on
listening, and the American phonemes spoken by the British voices - is verified by the owner's blinded listening
check on a pack of 30 sentences kept outside the repository. One test,
`Generate_RealInstalledModel_ProducesNonEmptyAudio`, exercises the real production model end-to-end when it is
genuinely installed in the running environment, self-skipping (not failing) otherwise.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline; the FileAssert test (`dotnet fileassert`)
  runs after the package is built
- **Project**: `test/DemaConsulting.Speech.Onnx.Kokoro.Tests`
- **Dependencies**: no external services, no network access and no physical audio hardware; one test additionally
  depends on the real, already-installed Kokoro model, self-skipping when absent
- **Isolation**: the system tests install the fixture graph and the synthetic voices once into a scratch directory
  under `Path.GetTempPath()` (a class fixture, deleted on disposal), and each test loads its own engine from it; no
  test reads or writes a developer's real installed-model store except the one real-model test, which only reads

## External Interface Simulation

Only the Kokoro ONNX graph and the voice files are simulated (a fixture graph and synthetic style rows, as described
above). The catalog, the model, the session, the text analysis, the phonemizer, the renderer, the vocabulary and
ONNX Runtime itself are the real ones.

## Acceptance Criteria

A system test passes when the observable values named in its scenario hold. The system passes verification when
every scenario below passes on every target framework, the FileAssert test passes on a built pre-release package
(a stable version builds no package and the test then has nothing to inspect), every subsystem-level suite
passes, and the automated verification boundary stays honest: catalog registration, voice selection, reading-aware
and normalized speech, the speaking of unknown words, the splitting of long text and the package license files are
claimed as automated coverage, while real speech intelligibility and physical audio I/O are left to the listening
check and manual verification.

## System-Level Test Scenarios

### Catalog registration

**Test**: `SpeechOnnxKokoro_AddKokoroModels_OffersTheEnglishSynthesisModelWithItsVoices`

`AddKokoroModels()` on a real catalog returns that same catalog and makes exactly the one shipped model appear in
the enumeration with the synthesis role and a `voice` choice of 29 options whose default is one of them.

### Voice selection

**Test**: `SpeechOnnxKokoro_Synthesize_SelectedVoice_SpeaksWithItsOwnStyle`

`The cat sat on the mat.` spoken with no voice selected is spoken by `af_heart` (the default), and with `bm_george`
selected by `bm_george`, as the voice marker of every run of the graph shows; the same text has the same number of
sounds in both.

### Reading chosen by the text analysis

**Test**: `SpeechOnnxKokoro_Synthesize_HeteronymSentence_SpeaksTheChosenReadings`

`A separate room.` through the engine (which runs the model's text analysis) has fewer sounds than the same text
through the backend's plain `Generate`, which speaks the default reading `separate` as a verb: the analysis chose
the adjective, and the adjective reached the phonemes.

### Numbers and symbols spoken as words

**Test**: `SpeechOnnxKokoro_Synthesize_NumbersAndSymbols_AreSpokenAsWords`

`It costs $5.` through the engine has more sounds than the plain `Generate` of the same text (`five dollars`
against the symbol and the digit as written).

### A word absent from the dictionary is still spoken

**Test**: `SpeechOnnxKokoro_Synthesize_WordAbsentFromDictionary_IsStillSpoken`

`Hello there Kathmandu.` has at least six more sounds than `Hello there.`: the name (which the dictionary does not
list) is pronounced from its
spelling and not omitted.

### Long text

**Test**: `SpeechOnnxKokoro_Synthesize_LongSegment_IsSpokenInPartsWithoutLoss`

Forty copies of one sentence without punctuation (one segment of about 1,500 sounds) are spoken as at least three
runs of the graph, each of at most 510 tokens, which together carry at least forty times the sounds of the sentence
(less one per cut) - nothing is truncated.

### Package license files

**Test**: `Kokoro_Package_LicenseFilesPackaged` (FileAssert)

After `dotnet pack` of a pre-release version, the `.nupkg` contains `PACKAGE-LICENSE.md` starting with
`SPDX-License-Identifier: MIT AND Apache-2.0` and naming `THIRD-PARTY-NOTICES.md`, the notices naming Kokoro-82M and
holding the Apache License text, and a nuspec that names the license file. The text of the license and the notices
themselves is reviewed, not scanned by tests.

### Execution provider probe and the real model

**Tests**: `RunProbeInference_RunnableSession_DoesNotThrow`, `Generate_RealInstalledModel_ProducesNonEmptyAudio`

The probe inference completes against a runnable session (see *SpeechOnnxKokoro SynthesisSubsystem Verification*).
The real-model test is not linked to a requirement (the CI model bootstrap does not provision this model); it runs
and reports whenever a developer has the real model installed.
