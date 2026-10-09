### KokoroLexiconPhonemizer Verification

#### Verification Approach

**No automated unit test project exists for this package** (see _SpeechOnnxKokoro System
Verification_); none of this unit's requirements link to a test, because none exists. This class
is verified by manual inspection and code review:

- `LoadLexicon()`'s gzip decompression and TSV parsing were reviewed by inspection against the
  embedded resource's own format
- `Phonemize`'s tokenization regex, whitespace collapsing, lexicon lookup, and passthrough
  punctuation set were reviewed by inspection, cross-checked against the real
  `kokoro-v1.0-tokenizer.json`'s declared vocabulary characters for punctuation it recognizes
- The lexicon's provenance (produced by running the real `misaki.en.G2P` tool offline over the
  CMUdict word list) was reviewed as documented directly in this class's own XML documentation
- The documented Stage-1 limitations (out-of-vocabulary words, homographs) were confirmed to be
  honestly and completely described, matching this class's actual return-value behavior

A future pass may add `KokoroLexiconPhonemizerTests` to a new
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
