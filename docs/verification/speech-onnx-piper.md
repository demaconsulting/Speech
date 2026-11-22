# SpeechOnnxPiper System Verification

This document describes the verification design of the SpeechOnnxPiper system.

## Verification Approach

SpeechOnnxPiper is verified through its public API, exactly as a host composes it, by system tests in
`test/DemaConsulting.Speech.Onnx.Piper.Tests/SpeechOnnxPiperTests.cs`, and by a FileAssert test of the built package. A
test registers the package's model in a real Speech `SpeechModelCatalog` with `AddPiperModels()`, takes the registered
model from the catalog's enumeration, checks its `models`-release download, installs a staged bundle, creates the
backend with `CreateBackend` and synthesizes with it. What differs from production is only the graph: the tests use a
tiny stand-in ONNX graph with the same four inputs as the Piper graph (`input`, `input_lengths`, `scales`, `sid`) whose
output echoes what it was fed (the phoneme ids, the three scales, the speaker and the input length), so a test observes
exactly what reached the voice without the real voice (about 78 MB). The voice configuration is a trimmed copy of the
real one (its symbol table, sample rate, speaker count and inference scales). The text analysis, the phonemizer, the
renderer, the symbol encoding, the installer and the execution-provider selector are the real ones.

Below the system level the units are verified in isolation and together (see *SpeechOnnxPiper ModelManagementSubsystem
Verification* and *SpeechOnnxPiper SynthesisSubsystem Verification*), including golden corpora of the phoneme pipeline.

Automated coverage **does not** include the intelligibility or naturalness of the speech from the real voice, which is a
listening judgment, nor playing audio on real hardware. One test,
`OnnxPiperSynthesisEngine_Generate_RealInstalledVoice_ProducesAudibleAudio`, runs the real installed voice when it is
present on the machine (audible, 22,050 Hz, no unknown symbol, a faster speed is shorter) and skips itself otherwise;
because it can skip, it is not linked to any requirement. The measurements of the phonemization against espeak-ng are
recorded in `docs/evaluation/phonemes-en/` and are not requirements evidence here.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library (net8.0 to net11.0)
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline; the FileAssert test (`dotnet fileassert`)
  runs after the packages are built
- **Project**: `test/DemaConsulting.Speech.Onnx.Piper.Tests`
- **Files**: the trimmed voice configuration (`TestData/en_US-libritts_r-medium.onnx.trimmed.json`, CC BY 4.0,
  attributed inside the file), the pinned pipeline golden (`TestData/piper-pipeline-golden.tsv`) and the golden corpora
  linked from the Phonemes.En and Kokoro test projects and the repository's data folder; the stand-in graph and the
  tampered bundle zips are built by the tests (`PiperTestFixtures`); no test needs network access or the real voice
- **Isolation**: every test uses its own scratch directory under the temporary folder and its own catalog and backend

## Acceptance Criteria

The system passes verification when every scenario below passes on every target framework and the FileAssert test passes
on a built pre-release package (a stable version builds no package and the test then has nothing to inspect).

## Test Scenarios

### Registration

**Test**: `SpeechOnnxPiper_AddPiperModels_RegistersTheVoiceAndChains`

`AddPiperModels()` on a new catalog returns that catalog and its enumeration has exactly one model, with the identifier
`piper-onnx-libritts-r-en-us-medium`, the Synthesis role and the license `CC BY 4.0` with its Creative Commons link; a
null catalog throws `ArgumentNullException`.

### Download and installation

**Tests**: `SpeechOnnxPiper_VoiceDownload_IsModelsReleaseBundlePinnedBySha256`,
`SpeechOnnxPiper_InstallAndLoad_TamperedBundleIsRejectedAndInstalledVoiceSpeaks`

The download descriptor has exactly one file, the zip
`https://github.com/demaconsulting/Speech/releases/download/models/piper-en-us-libritts-r-medium-20261122-fp32-b1.zip`
with the pinned SHA-256 `5196a8aa97f9053e0bdbbc2299007d48d55f37a74e88199869daebb710d8bd0f` (so nothing comes from
Hugging Face). A well-formed bundle whose `model.json` declares the compiled pins but whose vendor entries hold other
bytes fails the installation with `InvalidDataException`. An installed voice (the stand-in graph and the configuration
at the installed root, the layout both a bundle install and an earlier install from the sherpa-onnx archive leave)
creates a backend that speaks at 22,050 Hz with the framing ids. The installation of a genuine bundle is exercised by
the Speech library's `SpeechModelBundle` tests, and the real published bundle by the release smoke test.

### Voice configuration

**Test**: `SpeechOnnxPiper_CreateBackend_VoiceOfAnotherShapeOrBrokenConfiguration_Fails`

With a graph file that is not loadable, a configuration with one speaker, one with a 16,000 Hz sample rate and an empty
object each fail `CreateBackend` with `InvalidDataException`: the configuration is checked before the graph is loaded.

### Speaker and speed

**Test**: `SpeechOnnxPiper_Synthesize_SpeakerAndSpeed_ReachTheVoice`

The model has one integer parameter from 0 to 903 with the default 0 and no audio-tag support. The value 7 resolves to
speaker 7 and reaches the graph, 904 resolves to speaker 0, a speaker 5000 passed directly is clamped to 903, a speed of
2 gives the length scale 0.5 and a speed of 0.5 gives 2.0.

### Reading-aware speech

**Tests**: `SpeechOnnxPiper_Synthesize_HeteronymSentence_SpeaksTheChosenReading`,
`SpeechOnnxPiper_Synthesize_UnknownWord_IsStillSpoken`

The sentence with the verb `record` feeds the same ids with and without the annotations of the text analysis (the verb
is the default reading), and the sentence with the noun `record` feeds other ids with the annotations, so the chosen
reading reaches the voice. `Welcome Zorblax` feeds at least ten ids more than `Welcome`, so an unknown word is spoken.

### Unknown symbols

**Test**: `SpeechOnnxPiper_Synthesize_VoiceWithoutASymbol_ReportsTheLossAndStillSpeaks`

With one symbol removed from the voice's table, `hot` is still spoken, and the engine reports exactly one skipped
symbol, that one. (This test reads the internal diagnostics of the engine.)

### Text length

**Test**: `SpeechOnnxPiper_Synthesize_LongTextAndSoundlessText_AreSpokenWholeOrEmpty`

Forty sentences are spoken whole: the audio carries at least 90 percent of the ids of forty single sentences. A text
that is only an emoji gives empty audio at 22,050 Hz.

### Execution provider

**Test**: `SpeechOnnxPiper_CreateBackend_UnavailablePreferredProvider_FallsBackToCpu`

A model registered with the provider name `NotARealExecutionProvider` creates a working backend on the CPU provider.

### Disposal

**Test**: `SpeechOnnxPiper_Backend_Dispose_IsIdempotentAndEndsUse`

A backend can be disposed twice, and synthesis after disposal throws `ObjectDisposedException`.

### Package license files

**Test**: `Piper_Package_LicenseFilesPackaged` (FileAssert)

The `.nupkg` carries `PACKAGE-LICENSE.md` and `THIRD-PARTY-NOTICES.md` at the package root and its nuspec names the
license file. The text of the notices (the CC BY 4.0 attribution of the voice, the LibriTTS-R citation, the Piper
project, the lineage notice, the bundle and its hash, the pointer to the notices of SpeechTextEn and SpeechPhonemesEn)
is reviewed; the test also checks that the notices name LibriTTS-R, Koizumi, Blizzard and the non-commercial
restriction.
