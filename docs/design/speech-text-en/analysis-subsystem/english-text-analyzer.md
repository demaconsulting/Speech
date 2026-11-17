### EnglishTextAnalyzer

**Purpose**: Provide the English implementation of the Speech library's `ITextAnalyzer`: annotate the word
tokens of a speech request with their part of speech, describe with `en.norm` annotations how abbreviations,
symbols and apostrophes are spoken and with `en.spell` annotations which letter sequences (initialisms,
initials, meridiem letters) are spoken by letter name, apply the host's pronunciation overrides
(`en.norm`, `en.spell`, `en.pron` or nothing for a word), and supply authoritative sentence boundaries
reconciled with that
normalization, with every offset indexing the exact text analyzed. The `EnglishTextAnalysis`,
`AnalyzedToken` and `AnalyzedNormalization` types, which carry the richer result of `AnalyzeDetailed`, are
documented with this unit.

**Data Model**: `EnglishTextAnalyzer` is a sealed class holding immutable fields: the `PartOfSpeechAdapter` seam, an
optional `TextNormalizer` (null means the shared default, resolved on first use), the `AnalysisDiagnostics` reporter,
two flags saying whether part-of-speech annotation and normalization are selected, and the `PronunciationOverrides`
dictionary (null when the options carry none or an empty one; the dictionary is immutable, so keeping the reference is
the snapshot). It keeps no per-request state. `AnalyzedToken` is a sealed record of `Start`, `Length`, `Text`,
`PartOfSpeech` (a Universal Dependencies tag name, or null when the text was not tagged), `Confidence` (0 when not
tagged) and `IsKnownWord`, with an `init` property `Clitic` (a `CliticKind`; `None` for a non-clitic and always `None`
when normalization was not selected). `AnalyzedNormalization` is a sealed record of `Start`, `Length`, `Text` (the
source slice), `SpokenText` and `Kind` (a `NormalizationKind`), with an `init` property `IsSpelled` (false by default):
the same span as the `en.norm` or, when `IsSpelled`, `en.spell` annotation. A pronunciation override is an annotation
but not a replacement, so it is not listed. `EnglishTextAnalysis` is a sealed class, constructible only by the analyzer,
with read-only `Tokens`, `SentenceBreaks`, `Annotations` and `Normalizations` lists; `Normalizations` is empty when
normalization was not selected.

**Key Methods**:

- **EnglishTextAnalyzer()** and **EnglishTextAnalyzer(options)**: take the options (defaults for the
  parameterless form: part of speech and normalization) and snapshot them. A null options value throws
  `ArgumentNullException`; a non-empty `Overrides` dictionary without the `Normalization` selection throws
  `ArgumentException` with the parameter name `options` (overrides are applied by the normalization pass, and
  ignoring them silently would hide a configuration mistake; an empty dictionary is accepted whatever the
  selection). An internal constructor additionally takes the adapter so tests can observe tagger
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
the token (tagging runs on the raw tokens). When normalization is selected, flatten the tokens; when a
dictionary is configured, call `PronunciationOverrides.Resolve` over the neutralized text and the flattened tokens to
get the token directives (one pass, no per-token object); then call
`TextNormalizer.Normalize` over the neutralized text (the default normalizer when none was supplied) with the
directives, asking for clitic roles only when the detailed token list was supplied. Then, for each sentence in
order: if it is not the first, add the offset of its first token to the tokenizer breaks; for each token, count it
and, only when a token list was supplied, add an `AnalyzedToken` whose `Text` is sliced from the original text and
whose `Clitic` is the role recorded at that token's start; if the token has a tag that is neither `PUNCT` nor
`SYM`, and contains a letter or digit (the tagger may label a bare `*` or an invisible character a word, but such
a token speaks nothing of its own), count it as an unknown word when its lexicon flag is false and its tag is not
`NUM`, and, unless it would start or end inside a surrogate pair, add one `en.upos` `SegmentAnnotation` over
exactly the token with the tag name as its value; then add one replacement `SegmentAnnotation` (value: the spoken
text) for each normalized span that starts inside that token (a two-pointer walk over the ascending span list):
kind `en.spell` when the span is marked spelled, `en.norm` otherwise; and, when the detailed list was supplied,
its
`AnalyzedNormalization`; then add one `en.pron` `SegmentAnnotation` (value: `scheme:phonemes`) for each
pronunciation mark that starts inside that token (a second two-pointer walk over the ascending mark list).
A token carries a replacement or a pronunciation, never both, so at an equal start the order is `en.upos`, then
the replacement or the pronunciation. A final flush adds any span or mark left over, so none is
ever lost. The sentence breaks are the tokenizer breaks reconciled by `AlignedText.ReconcileBreaks` when
normalization is selected, and the tokenizer breaks unchanged otherwise. Finally report the counts (sentences
= reconciled breaks plus one when there is at least one token; tokens; annotations of all kinds; unknown
words; normalizations (the replacement spans, `en.norm` and `en.spell` together); ambiguous readings; numeric
fallbacks; overrides applied; overrides ignored) and return the lists as read-only. A span of the numeric shapes
S3 and S4 (a currency symbol or an apostrophe with its adjacent number token) starts inside the symbol token,
which carries no `en.upos` annotation, so its `en.norm` annotation follows the part of speech of the previous
word and precedes that of the number, which keeps the annotations in text order. Because tokens come from
the tokenizer and spans and marks from the normalizer, an annotation never contains whitespace or a placeholder and so
starts and ends on non-whitespace; the surrogate-pair guard on `en.upos` is a defensive check, and the
normalizer's builder enforces the same for the replacement and pronunciation annotations. A text without
tokens yields no annotations and an
empty, non-null break list.

**Error Handling**: a null text throws `ArgumentNullException`; a canceled token throws
`OperationCanceledException`, checked before any work, after the tagger is obtained, after each sentence is
tagged (all in the adapter), every 256 visited tokens while the overrides are looked up and while the text is
normalized (in the dictionary and the normalizer), periodically while
the tokens, annotations and breaks are built, and once more before the diagnostics are reported. A canceled
request therefore never returns a result and never reports diagnostics, whether the cancellation arrives
during tagger initialization, during the override lookup, during normalization or during the only or last
sentence. Nothing else is
thrown by this unit for any text: the diagnostics sink's failures are contained by `AnalysisDiagnostics`. A failure
inside the tagging package propagates to the caller, and the Speech library's session contains it and
synthesizes without annotations. A failed tagger creation is not remembered (see *PartOfSpeechAdapter
Design*), so a later request retries. An exception from the normalizer (which can only mean a defective
build, such as a malformed embedded lexicon) propagates the same way.

**Retention**: the analyzer keeps no reference to the analyzed text or to a result in its own state after
the call returns (no field holds either, and a weak-reference test proves that). The tagging package's own
per-thread working buffers are cleared by the pinned version (0.1.0-beta.5 or later; upstream issue
<https://github.com/demaconsulting/PartOfSpeech/issues/29>), which this repository cannot inspect through a
public API; see *SpeechTextEn System Design*, *Retention Verification*.

**Dependencies**: `PartOfSpeechAdapter` (splitting and tagging), `PronunciationOverrides` (the token
directives of the host's dictionary), the NormalizationSubsystem's
`TextNormalizer`, `AlignedText` and the `NormalizationKind` and `CliticKind` enumerations (normalization),
`AnalysisDiagnostics` (reporting), `EnglishTextAnalyzerOptions` and `TextEnAnnotationSelection`
(configuration), `TextEnAnnotations` (the kind constants), and the Speech library's `ITextAnalyzer`,
`TextAnalysisResult`, `SegmentAnnotation` and `TextAnalysisConventions`. No PartOfSpeech type is named by
this unit.

**Callers**: the Speech library's `SynthesisSession`, through the model's `ITextAnalyzer`; a model package
that wants the detailed analysis; the test projects.
