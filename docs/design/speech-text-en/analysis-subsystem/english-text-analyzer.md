### EnglishTextAnalyzer

**Purpose**: Provide the English implementation of the Speech library's `ITextAnalyzer`: annotate the word
tokens of a speech request with their part of speech and supply authoritative sentence boundaries, with
every offset indexing the exact text analyzed. The `EnglishTextAnalysis` and `AnalyzedToken` types, which
carry the richer result of `AnalyzeDetailed`, are documented with this unit.

**Data Model**: `EnglishTextAnalyzer` is a sealed class holding three immutable fields: the
`PartOfSpeechAdapter` seam, the `AnalysisDiagnostics` reporter, and a flag saying whether part-of-speech
annotation is selected. It keeps no per-request state. `AnalyzedToken` is a sealed record of `Start`,
`Length`, `Text`, `PartOfSpeech` (a Universal Dependencies tag name, or null when the text was not tagged),
`Confidence` (0 when not tagged) and `IsKnownWord`. `EnglishTextAnalysis` is a sealed class, constructible
only by the analyzer, with read-only `Tokens`, `SentenceBreaks` and `Annotations` lists.

**Key Methods**:

- **EnglishTextAnalyzer()** and **EnglishTextAnalyzer(options)**: take the options (defaults for the
  parameterless form) and snapshot them. A null options value throws `ArgumentNullException`. An internal
  constructor additionally takes the adapter so tests can observe tagger creation
- **Analyze(text, cancellationToken)**: runs the shared analysis core without a detailed-token list and
  returns its annotations and sentence breaks as a `TextAnalysisResult`; no `AnalyzedToken` and no
  per-token text copy is created, because this is the path every synthesis request takes
- **AnalyzeDetailed(text, cancellationToken)**: runs the same core with a token list, so each token is also
  materialized as an `AnalyzedToken`; see the algorithm below
- **AnalyzeCore(text, detailedTokens, cancellationToken)**: private; the algorithm below, shared by both
  entry points so their annotations, breaks, cancellation checkpoints, counts and thread-safety cannot
  diverge. The optional token list is the only difference between the two paths
- **NeutralizePlaceholders(text)**: returns the text itself when it contains no `U+FFFC`, otherwise a copy
  of the same length in which each placeholder is a space

**Algorithm of the shared core**: after rejecting a null text and an already-canceled token, neutralize the
placeholders and call `PartOfSpeechAdapter.Analyze` with the neutralized text, the selection flag and the
token. For each sentence in order: if it is not the first, add the offset of its first token to the sentence
breaks; for each token, count it and, only when a token list was supplied (`AnalyzeDetailed`), add an
`AnalyzedToken` whose `Text` is sliced from the original text; if the token
has a tag that is neither `PUNCT` nor `SYM`, count it as an unknown word when its lexicon flag is false and
its tag is not `NUM`, and, unless it would start or end inside a surrogate pair, add one `en.upos`
`SegmentAnnotation` over exactly the token with the tag name as its value. Finally report the counts and
return the lists as read-only. Because tokens come from the tokenizer, an annotation never contains
whitespace or a placeholder and so starts and ends on non-whitespace; the surrogate-pair guard is a
defensive check that keeps an annotation from naming half of a pair. A text without tokens yields no
annotations and an empty, non-null break list.

**Error Handling**: a null text throws `ArgumentNullException`; a canceled token throws
`OperationCanceledException`, checked before any work, after the tagger is obtained, after each sentence is
tagged (all in the adapter), periodically while the tokens, annotations and breaks are built, and once more
before the diagnostics are reported. A canceled request therefore never returns a result and never reports
diagnostics, whether the cancellation arrives during tagger initialization or during the only or last
sentence. Nothing else is
thrown by this unit: the diagnostics sink's failures are contained by `AnalysisDiagnostics`. A failure
inside the tagging package propagates to the caller, and the Speech library's session contains it and
synthesizes without annotations. A failed tagger creation is not remembered (see _PartOfSpeechAdapter
Design_), so a later request retries.

**Retention**: the analyzer keeps no reference to the analyzed text or to a result in its own state after
the call returns (no field holds either, and a weak-reference test proves that). That claim is scoped to
this unit: the tagging package keeps token characters in a private, never-cleared per-thread buffer, which
currently violates the core `ITextAnalyzer` contract that an analyzer must not retain the text, so the
package is gated and not published (see _SpeechTextEn System Design_, _Known Limitations_ and _Release
Gate_, and upstream issue <https://github.com/demaconsulting/PartOfSpeech/issues/29>).

**Dependencies**: `PartOfSpeechAdapter` (splitting and tagging), `AnalysisDiagnostics` (reporting),
`EnglishTextAnalyzerOptions` and `TextEnAnnotationSelection` (configuration), `TextEnAnnotations` (the kind
constant), and the Speech library's `ITextAnalyzer`, `TextAnalysisResult`, `SegmentAnnotation` and
`TextAnalysisConventions`. No PartOfSpeech type is named by this unit.

**Callers**: the Speech library's `SynthesisSession`, through the model's `ITextAnalyzer`; a model package
that wants the detailed analysis; the test projects.
