### OnnxKokoroEnglishSynthesisModel Verification

#### Verification Approach

**No automated unit test project exists for this package** (see _SpeechOnnxKokoro System
Verification_); none of this unit's requirements link to a test, because none exists. This class
is verified by manual inspection and code review:

- Its declared `Id`/`DisplayName`/`Role`/`LicenseName`/`LicenseUrl`/`AudioTagSupport` and its
  29-voice `ChoiceParameter` option list were reviewed against the real upstream
  `onnx-community/Kokoro-82M-v1.0-ONNX` model card
- Its `DownloadDescriptor`'s 30 declared files, relative install paths, and SHA-256 checksums were
  reviewed against the real downloaded bytes recorded directly in this class's own XML
  documentation (fetched through a locally configured download mirror, since the development
  sandbox could not reach Hugging Face directly)
- Its `CreateBackend` implementation was reviewed for correct delegation to
  `OnnxExecutionProviderSelector.Create`, correct voice-style-vector loading keyed by speaker id,
  and correct construction of `OnnxKokoroSynthesisEngine`
- Its `ResolveSpeakerId` implementation was reviewed for correct, never-throwing fallback
  behavior across every one of its 29 declared voices, a null bag, a missing key, and an
  unrecognized value

A future pass may add `OnnxKokoroEnglishSynthesisModelTests` to a new
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests` project, mirroring the sibling SpeechSherpa
package's `SherpaOnnxKokoroEnglishSynthesisModelTests` conventions (deterministic identity/
metadata/engine-configuration tests requiring no network access or native runtime). Adding that
project is explicitly out of scope for this pass.

#### Test Environment

N/A - no automated test project exists for this unit.

#### Acceptance Criteria

This unit is accepted on the strength of: successful compilation with zero warnings, static
analysis via `Microsoft.CodeAnalysis.NetAnalyzers` and `SonarAnalyzer.CSharp`, and the
manual/code-review verification described above - not an automated test run.

#### Test Scenarios

N/A - no automated test scenarios exist for this unit.
