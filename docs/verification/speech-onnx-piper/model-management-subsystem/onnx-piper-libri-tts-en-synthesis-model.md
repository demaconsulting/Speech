### OnnxPiperLibriTtsEnSynthesisModel Verification

#### Verification Approach

`OnnxPiperLibriTtsEnSynthesisModel` is verified by unit tests in
`test/DemaConsulting.Speech.Onnx.Piper.Tests/ModelManagementSubsystem/OnnxPiperLibriTtsEnSynthesisModelTests.cs` that
never touch the network or the real voice. The declared facts and the speaker resolution are checked directly. The
bundle and its three per-file pins are checked directly, the installation with a tampered bundle zip that the tests
build with `PiperTestFixtures`, and the backend creation with a stand-in ONNX graph that has the four inputs of the
Piper graph, written beside the trimmed copy of the real voice configuration (the layout of both a bundle install and an
earlier install from the sherpa-onnx archive).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: each test class instance uses its own scratch directory under `Path.GetTempPath()`; no network, no real
  voice

#### Acceptance Criteria

The model declares the identifier `piper-onnx-libritts-r-en-us-medium`, the Synthesis role, no audio-tag support, the
license `CC BY 4.0` with `https://creativecommons.org/licenses/by/4.0/`, a preferred format of 22,050 Hz mono and a text
analyzer that is the same instance for every model instance and a capability profile that speaks the clauses of a
sentence as one segment (and keeps a sentence end, a pause and the 300-character budget as boundaries); its only
parameter is an integer from 0 to 903 with the default 0; the speaker values `7`, `7.0`, `7.4f` and `903` resolve to 7,
7, 7 and 903 and `null`, 904, -1, NaN, infinity and the text `"7"` resolve to 0; the bundle holds exactly
`en_US-libritts_r-medium.onnx`, `en_US-libritts_r-medium.onnx.json` and `MODEL_CARD` with their pinned SHA-256 and their
original `rhasspy/piper-voices` URLs; the download descriptor has one file, the
`piper-en-us-libritts-r-medium-20261122-fp32-b1.zip` asset of the `models` release with the SHA-256
`5196a8aa97f9053e0bdbbc2299007d48d55f37a74e88199869daebb710d8bd0f`; a bundle whose vendor bytes differ from the pins
throws `InvalidDataException`; a backend is created at 22,050 Hz and is an `IAnnotatedSynthesisBackend`; an empty
directory argument, a configuration of one speaker, an empty configuration and a missing graph fail; and a model with an
unavailable preferred provider falls back to the CPU and synthesizes.

#### Test Scenarios

##### Catalog facts

**Test**: `OnnxPiperLibriTtsEnSynthesisModel_CatalogFacts_AreDeclared`

##### Capability profile

**Tests**: `OnnxPiperLibriTtsEnSynthesisModel_CapabilityProfile_SentenceClauses_AreOneSegmentWithTheirAnnotations`,
`OnnxPiperLibriTtsEnSynthesisModel_CapabilityProfile_JoinsOnlyWhereNothingSetsTheClausesApart`,
`OnnxPiperLibriTtsEnSynthesisModel_CapabilityProfile_PauseTag_StaysABoundary`,
`OnnxPiperLibriTtsEnSynthesisModel_CapabilityProfile_AnalyzerSentenceBreakAfterClauseMark_StaysABoundary`,
`OnnxPiperLibriTtsEnSynthesisModel_CapabilityProfile_TagsAndLongSentences_KeepTheBoundariesAndTheBudget`
`Wait for 5 minutes, then ask Dr. Smith; he will know. It was late, so we left.` becomes two segments (one per sentence,
commas kept, the words and their order as in the default plan, which has one segment per clause), and every annotation
covers the very text it covered in its clause piece. The join function keeps segments apart for a different set of
parameter overrides, silence on either side, a first piece that ends a sentence, an empty text and a joined text beyond
300 characters, joins four pieces ending in `,`, `;`, `:` into one with the first one's leading silence, the last one's
trailing silence and the shared overrides, and joins pieces cut at a dash (`--`, a spaced or attached hyphen, an en or
em dash) or inside a long clause into one text. A pause tag stays its own segment with the clauses on each side joined
within themselves only; a null argument is rejected. `Consider 5 options:` followed by a paragraph break and
`Should we pick 7, or wait?` stays two segments (an analyzer sentence boundary after a clause mark is kept, and each
number's replacement stays in its own sentence, and every part-of-speech annotation stays inside its segment); an
analyzer break next to a tag is kept, a stripped tag inside a sentence does not separate its clauses, and a long
sentence of twelve clauses arrives in segments of at most 300 characters that read back as the same text.

##### Speaker parameter

**Tests**: `OnnxPiperLibriTtsEnSynthesisModel_Parameters_AreTheSpeakerSelectionOnly`,
`OnnxPiperLibriTtsEnSynthesisModel_ResolveSpeakerId_Value_ResolvesOrFallsBackToZero`

##### Bundle and download descriptor

**Tests**: `OnnxPiperLibriTtsEnSynthesisModel_BundleContents_PinsTheThreeVendorFiles`,
`OnnxPiperLibriTtsEnSynthesisModel_DownloadDescriptor_IsModelsReleaseBundle`

The bundle's contents are the three vendor files at the installed root, each with its exact SHA-256 and its original
`https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/libritts_r/medium/` URL (provenance only); the
descriptor is the one zip on the `models` release with its pinned SHA-256.

##### Installation

**Test**: `OnnxPiperLibriTtsEnSynthesisModel_InstallAsync_BundleWithWrongFile_Throws`

A well-formed bundle zip whose `model.json` declares the compiled pins but whose vendor entries hold other bytes fails
the installation with `InvalidDataException`; unsafe entry names, extra or missing entries and a genuine bundle's
extraction are verified by the Speech library's `SpeechModelBundle` and `SpeechModelBundleInstaller` tests.

##### Backend creation

**Tests**: `OnnxPiperLibriTtsEnSynthesisModel_CreateBackend_InstalledDirectory_ReturnsBackendAtVoiceRate`,
`OnnxPiperLibriTtsEnSynthesisModel_CreateBackend_UnusableInstallation_Throws`,
`OnnxPiperLibriTtsEnSynthesisModel_CreateBackend_ConfigurationOfAnotherVoice_Throws`,
`OnnxPiperLibriTtsEnSynthesisModel_CreateBackend_UnavailablePreferredProvider_FallsBackToCpu`

The configuration-of-another-voice test is a theory over speaker counts of 1, 903 and 905 and a sample rate of 16,000
Hz; each is refused, because only exactly 904 speakers at 22,050 Hz describes this voice.
