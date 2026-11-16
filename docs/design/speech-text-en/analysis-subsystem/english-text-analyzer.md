### EnglishTextAnalyzer

**Purpose**: Provide the English implementation of the Speech library's `ITextAnalyzer`: annotate the word
tokens of a speech request with their part of speech, describe with `en.norm` annotations how abbreviations,
symbols, apostrophes and initialisms are spoken, and supply authoritative sentence boundaries reconciled with
that normalization, with every offset indexing the exact text analyzed. The `EnglishTextAnalysis`,
`AnalyzedToken` and `AnalyzedNormalization` types, which carry the richer result of `AnalyzeDetailed`, are
documented with this unit.

**Data Model**: `EnglishTextAnalyzer` is a sealed class holding immutable fields: the `PartOfSpeechAdapter`
seam, an optional `TextNormalizer` (null means the shared default, resolved on first use), the
`AnalysisDiagnostics` reporter, and two flags saying whether part-of-speech annotation and normalization are
selected. It keeps no per-request state. `AnalyzedToken` is a sealed record of `Start`, `Length`, `Text`,
`PartOfSpeech` (a Universal Dependencies tag name, or null when the text was not tagged), `Confidence` (0 when
not tagged) and `IsKnownWord`, with an `init` property `Clitic` (a `CliticKind`; `None` for a non-clitic and
always `None` when normalization was not selected). `AnalyzedNormalization` is a sealed record of `Start`,
`Length`, `Text` (the source slice), `SpokenText` and `Kind` (a `NormalizationKind`): the same span as the
`en.norm` annotation. `EnglishTextAnalysis` is a sealed class, constructible only by the analyzer, with
read-only `Tokens`, `SentenceBreaks`, `Annotations` and `Normalizations` lists; `Normalizations` is empty when
normalization was not selected.

**Key Methods**:

- **EnglishTextAnalyzer()** and **EnglishTextAnalyzer(options)**: take the options (defaults for the
  parameterless form: part of speech and normalization) and snapshot them. A null options value throws
  `ArgumentNullException`. An internal constructor additionally takes the adapter so tests can observe tagger
  creation, and another also takes the normalizer so tests can observe and cancel inside normalization
- **Analyze(text, cancellationToken)**: runs the shared analysis core without detailed lists and returns its
  annotations and sentence breaks as a `TextAnalysisResult`; no `AnalyzedToken`, no `AnalyzedNormalization`
  and no per-token text copy is created, and clitic roles are not classified, because this is the path every
  synthesis request takes
- **AnalyzeDetailed(text, cancellationToken)**: runs the same core with a token list and a normalization
  list, so each token (with its clitic role) and each normalization is also materialized
- **AnalyzeCore(text, detailedTokens, detailedNormalizations, cancellationToken)**: private; the algorithm
  below, shared by both entry points so their annotations, breaks, cancellation checkpoints, counts and
  thread-safety cannot diverge. The optional lists are the only difference between the two paths
- **FlattenTokens(sentences)**: private; builds the one exact-size `NormalizationToken` array the normalizer
  takes, marking the first token of each tokenizer sentence
- **NeutralizePlaceholders(text)**: returns the text itself when it contains no `U+FFFC`, otherwise a copy
  of the same length in which each placeholder is a space

**Algorithm of the shared core**: after rejecting a null text and an already-canceled token, neutralize the
placeholders and call `PartOfSpeechAdapter.Analyze` with the neutralized text, the part-of-speech flag and
the token (tagging runs on the raw tokens). When normalization is selected, flatten the tokens and call
`TextNormalizer.Normalize` over the neutralized text (the default normalizer when none was supplied), asking
for clitic roles only when the detailed token list was supplied. Then, for each sentence in order: if it is
not the first, add the offset of its first token to the tokenizer breaks; for each token, count it and, only
when a token list was supplied, add an `AnalyzedToken` whose `Text` is sliced from the original text and whose
`Clitic` is the role recorded at that token's start; if the token has a tag that is neither `PUNCT` nor
`SYM`, and contains a letter or digit (the tagger may label a bare `*` or an invisible character a word,
but such a token speaks nothing of its own), count it as an unknown word when its lexicon flag is false and its tag is not `NUM`, and, unless it
would start or end inside a surrogate pair, add one `en.upos` `SegmentAnnotation` over exactly the token with
the tag name as its value; then add one `en.norm` `SegmentAnnotation` (value: the spoken text) for each
normalized span that starts inside that token (a two-pointer walk over the ascending span list), and, when the
detailed list was supplied, its `AnalyzedNormalization`. A final flush adds any span left over, so none is
ever lost. The sentence breaks are the tokenizer breaks reconciled by `AlignedText.ReconcileBreaks` when
normalization is selected, and the tokenizer breaks unchanged otherwise. Finally report the counts (sentences
= reconciled breaks plus one when there is at least one token; tokens; annotations of both kinds; unknown
words; normalizations; ambiguous readings; numeric fallbacks) and return the lists as read-only. A span of the numeric shapes S3 and S4 (a currency symbol or an apostrophe with its adjacent number token) starts inside the symbol token, which carries no `en.upos` annotation, so its `en.norm` annotation follows the part of speech of the previous word and precedes that of the number, which keeps the annotations in text order. Because tokens come from
the tokenizer and spans from the normalizer, an annotation never contains whitespace or a placeholder and so
starts and ends on non-whitespace; the surrogate-pair guard on `en.upos` is a defensive check, and the
normalizer's builder enforces the same for `en.norm`. A text without tokens yields no annotations and an
empty, non-null break list.

**Error Handling**: a null text throws `ArgumentNullException`; a canceled token throws
`OperationCanceledException`, checked before any work, after the tagger is obtained, after each sentence is
tagged (all in the adapter), periodically while the text is normalized (in the normalizer), periodically while
the tokens, annotations and breaks are built, and once more before the diagnostics are reported. A canceled
request therefore never returns a result and never reports diagnostics, whether the cancellation arrives
during tagger initialization, during normalization or during the only or last sentence. Nothing else is
thrown by this unit: the diagnostics sink's failures are contained by `AnalysisDiagnostics`. A failure
inside the tagging package propagates to the caller, and the Speech library's session contains it and
synthesizes without annotations. A failed tagger creation is not remembered (see _PartOfSpeechAdapter
Design_), so a later request retries. An exception from the normalizer (which can only mean a defective
build, such as a malformed embedded lexicon) propagates the same way.

**Retention**: the analyzer keeps no reference to the analyzed text or to a result in its own state after
the call returns (no field holds either, and a weak-reference test proves that). That claim is scoped to
this unit: the tagging package keeps token characters in a private, never-cleared per-thread buffer, which
currently violates the core `ITextAnalyzer` contract that an analyzer must not retain the text, so the
package is gated and not published (see _SpeechTextEn System Design_, _Known Limitations_ and _Release
Gate_, and upstream issue <https://github.com/demaconsulting/PartOfSpeech/issues/29>).

**Dependencies**: `PartOfSpeechAdapter` (splitting and tagging), the NormalizationSubsystem's
`TextNormalizer`, `AlignedText` and the `NormalizationKind` and `CliticKind` enumerations (normalization),
`AnalysisDiagnostics` (reporting), `EnglishTextAnalyzerOptions` and `TextEnAnnotationSelection`
(configuration), `TextEnAnnotations` (the kind constants), and the Speech library's `ITextAnalyzer`,
`TextAnalysisResult`, `SegmentAnnotation` and `TextAnalysisConventions`. No PartOfSpeech type is named by
this unit.

**Callers**: the Speech library's `SynthesisSession`, through the model's `ITextAnalyzer`; a model package
that wants the detailed analysis; the test projects.
