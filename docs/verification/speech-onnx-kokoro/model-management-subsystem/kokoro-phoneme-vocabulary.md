### KokoroPhonemeVocabulary Verification

#### Verification Approach

**No automated unit test project exists for this package** (see _SpeechOnnxKokoro System
Verification_); none of this unit's requirements link to a test, because none exists. This class
is verified by manual inspection and code review:

- `LoadVocab()`'s JSON parsing of `model.vocab` and its single-UTF-16-code-unit key filtering were
  reviewed by inspection against the real embedded `kokoro-v1.0-tokenizer.json`
- `ToTokenIds`'s per-character lookup-and-drop logic and `MaxPhonemeTokens` truncation were
  reviewed by inspection and confirmed to match the proven Python reference pipeline's own
  `[vocab[c] for c in phonemes if c in vocab]` behavior
- `MaxPhonemeTokens = 510` and `PadTokenId = 0` were confirmed against the model's own published
  512-token context window

A future pass may add `KokoroPhonemeVocabularyTests` to a new
`test/DemaConsulting.Speech.Onnx.Kokoro.Tests` project. Adding that project is explicitly out of
scope for this pass.

#### Test Environment

N/A - no automated test project exists for this unit.

#### Acceptance Criteria

This unit is accepted on the strength of: successful compilation with zero warnings, static
analysis via `Microsoft.CodeAnalysis.NetAnalyzers` and `SonarAnalyzer.CSharp`, and the
manual/code-review verification described above - not an automated test run.

#### Test Scenarios

N/A - no automated test scenarios exist for this unit.
