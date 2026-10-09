# SpeechOnnxKokoro System Verification Design

This document describes the system-level verification strategy for the SpeechOnnxKokoro library.

## Verification Approach

**This package currently has no automated unit test project** - there is no
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests` directory. This is a known, pre-existing gap, not
an oversight of this document: the subsystem and unit requirements in
`docs/reqstream/speech-onnx-kokoro/` describe the required behavior of every unit in this system
regardless of test coverage, but no requirement in this system links to a test, because none
exists to link to. Correctness of this package today rests entirely on manual verification and
code review: careful inspection of `OnnxKokoroEnglishSynthesisModel`'s declared identity, download
descriptor, and voice ordering against upstream `onnx-community/Kokoro-82M-v1.0-ONNX`'s own
published files; inspection of `KokoroLexiconPhonemizer`'s embedded lexicon provenance (produced
by running the real `misaki.en.G2P` tool offline, once, over the CMUdict word list); inspection of
`KokoroPhonemeVocabulary`'s embedded `tokenizer.json` against the real upstream file; and
inspection of `OnnxKokoroSynthesisEngine`'s ONNX Runtime input/output tensor wiring against the
model's own published inference contract.

A future pass may add `test/DemaConsulting.Speech.Onnx.Kokoro.Tests` with unit tests mirroring the
sibling SpeechSherpa package's `DemaConsulting.Speech.Sherpa.Tests` conventions (deterministic
identity/metadata tests with no network access, and real-native synthesis tests that self-skip
when the model is not installed). Adding that project is explicitly out of scope for this pass.

## Test Environment

N/A - no automated test project exists for this package. A future test project would use the same
environment as the sibling SpeechSherpa system: xUnit v3 running under the .NET SDK via
`dotnet test`, invoked by `build.ps1` and the CI pipeline.

## Acceptance Criteria

There is no automated acceptance criterion for this system today. The package is accepted into a
release build on the strength of: successful compilation with zero warnings
(`TreatWarningsAsErrors`), static analysis via `Microsoft.CodeAnalysis.NetAnalyzers` and
`SonarAnalyzer.CSharp`, and manual/code-review verification of the identity, download, and
pipeline behavior described above. This is an honestly-documented, reduced verification posture
relative to every other system in this repository, which does carry automated test coverage.

## Test Scenarios

N/A - no automated test scenarios exist for this package. See each subsystem's own verification
document (_SpeechOnnxKokoro ModelManagementSubsystem Verification_, _SpeechOnnxKokoro
SynthesisSubsystem Verification_) for the manual/code-review verification performed for each unit
in lieu of automated test scenarios.
