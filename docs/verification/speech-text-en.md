# SpeechTextEn System Verification Design

This document describes the system-level verification strategy for the SpeechTextEn library.

## Verification Approach

SpeechTextEn is verified at system level through its public API, exactly as a model package and a host use
it: the tests construct `EnglishTextAnalyzer` (with default and with explicit options), call the Speech
library's `ITextAnalyzer.Analyze` on it, and read the results with the library's own annotation reader. The
end-to-end tests go one step further and drive the real Speech `SynthesisSession`: a small test model
supplies an `EnglishTextAnalyzer` through `ISynthesisModel.TextAnalyzer` and a recording
`IAnnotatedSynthesisBackend`, the real `SpeechSynthesizerFactory` loads the engine, and the tests assert
what the backend actually receives - which proves the analyzer works across the real seam, that its
annotations survive chunking of a long text, that its sentence breaks (not the punctuation heuristic) decide
where sentences are split, and that audio tags inside a request leave the words around them annotated.

Below the system level, the unit and subsystem behavior is verified by the AnalysisSubsystem tests (see
_SpeechTextEn AnalysisSubsystem Verification_), and the part-of-speech package the analysis builds on is
verified separately as an OTS item (see _PartOfSpeech Verification_).

Automated coverage does not extend to the accuracy of the tagger beyond a few unambiguous golden sentences:
the analyzer relays the tagger's decisions, so the tests assert structure, offsets and wiring and use only
sentences whose tags the tagger assigns with full confidence.

System and unit tests reside in the `test/DemaConsulting.Speech.Text.En.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: no external services, no network access, no native runtime and no audio hardware; the
  part-of-speech package's embedded model is the only data used
- **Isolation**: each end-to-end test uses a fresh scratch model directory and its own recording backend

## External Interface Simulation

The Speech library is used for real. The model and the backend are small test doubles that implement the
library's public `ISynthesisModel` and `IAnnotatedSynthesisBackend` interfaces and record what they
receive; a recording `ISpeechDiagnostics` sink (optionally throwing) simulates a host sink.

## System-Level Test Scenarios

### Annotation and Reading

**Tests**: `SpeechTextEn_Analyze_Sentence_AnnotatesWordsWithReadablePartOfSpeech`,
`SpeechTextEn_Analyze_PartOfSpeechDisabled_ProducesNoAnnotations`

Verifies that a sentence gets one readable part-of-speech annotation per word and none on its final period,
and that selecting no annotations produces none.

### Sentence Breaks

**Tests**: `SpeechTextEn_Analyze_MultipleSentencesAndTag_ReturnsAuthoritativeBreaks`,
`SpeechTextEn_SynthesisSession_AnalyzerSentenceBreaks_DriveChunkBoundaries`

Verifies that the breaks are non-null and sit at the first word of each later sentence, not inside
"Dr. Smith", and that through the real session the text is split exactly there, whereas the same text
without the analyzer is split after "Dr.".

### Placeholders

**Tests**: `SpeechTextEn_Analyze_TagPlaceholder_IsNeverCoveredByAnAnnotation`,
`SpeechTextEn_SynthesisSession_TextWithAudioTag_AnnotatesWordsAroundTheTag`

Verifies that no annotation covers a placeholder, and that a request such as "I've [whispers] read it."
delivers annotated words on both sides of the tag to the backend with no placeholder in any received text.

### Contract: Concurrency, Cancellation and End-to-End Delivery

**Tests**: `SpeechTextEn_Analyze_ConcurrentRequestsOnSharedAnalyzer_ReturnIdenticalResults`,
`SpeechTextEn_Analyze_CanceledToken_ThrowsOperationCanceledException`,
`SpeechTextEn_SynthesisSession_AnnotatedBackend_ReceivesPartOfSpeechAnnotationsThatSurviveChunking`

Verifies that one shared analyzer gives identical results to many concurrent requests, that a canceled token
throws `OperationCanceledException`, and that a long, multi-sentence request (several times the chunk
length) reaches an annotation-aware backend as several chunks, each carrying part-of-speech annotations on
words of that chunk, with exactly the annotations the analyzer produced.

### Diagnostics

**Test**: `SpeechTextEn_Diagnostics_AnalyzedRequest_ReportsCountsOnlyAndSinkFailureIsContained`

Verifies that one event in the `TextEn.Analysis` category reaches the sink with counts only and none of the
words of the text, and that an analyzer whose sink throws still returns its full result.

### Package Files

**Tests**: `SpeechTextEn_PackageLicense_ProjectFile_NamesLicenseFileAtPackageRoot`,
`SpeechTextEn_PackageLicense_File_DeclaresMitOnlyAndPointsToDependencyNotice`,
`SpeechTextEn_ThirdPartyNotices_File_AttributesDependencyData`,
`SpeechTextEn_ProjectFile_Dependencies_PinTaggerExactlyAndReferenceNoInferenceRuntime`,
`TextEn_Package_LicenseFilesPackaged`

Verifies that the project file names `PACKAGE-LICENSE.md` as the license file (and no license expression)
and packs both it and `THIRD-PARTY-NOTICES.md` at the package root; that the license file starts with the
SPDX line `MIT` and nothing else, contains the MIT license text, and describes the tagging package as a
separate dependency that is not bundled, carries its own license files, and whose data this package does
not embed; that the notices are scoped as a dependency notice, reproduce the Manually Annotated Sub-Corpus
and Open English WordNet attribution and the Princeton WordNet notice, and record the dependency's
text-retention limitation with its upstream issue link; and that the project pins the tagging package with
an exact version range, references only the Speech project, and references no ONNX package. The first four
run in the test project against the repository files; the last is a FileAssert test that, after
`dotnet pack`, confirms the built `.nupkg` contains both files, that the license file's declaration is
exactly `MIT`, and that its `.nuspec` references the license file.

## Acceptance Criteria

A SpeechTextEn system-level test run passes when all scenarios above pass without unexpected exceptions,
every subsystem and unit suite passes, and the automated verification boundary remains honest: structure,
offsets, wiring, sentence breaks, placeholder handling, diagnostics and packaging are claimed as automated
coverage, while the linguistic accuracy of the tagger is left to the tagging package's own evaluation, and
the retention of analyzed words inside the tagging package's private buffer (a known limitation of the
dependency) is documented rather than claimed as verified. The package license files are also part of the
acceptance criteria: both must be present at the package root, the license declaration must be MIT only,
and the dependency notice must keep the attribution chain intact.
