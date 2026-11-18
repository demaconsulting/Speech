### EnglishTextAnalyzer

**Purpose**: Provide the English implementation of the Speech library's `ITextAnalyzer`: annotate the word
tokens of a speech request with their part of speech, describe with `en.norm` annotations how abbreviations,
symbols and apostrophes are spoken and with `en.spell` annotations which letter sequences (initialisms,
initials, meridiem letters) are spoken by letter name, apply the host's pronunciation overrides
(`en.norm`, `en.spell`, `en.pron`, a fixed reading or nothing for a word), annotate the heteronym words with the
reading the classifier chooses (or an override fixes) in an `en.reading` and an `en.reading-confidence` annotation, and
supply authoritative sentence boundaries reconciled with that normalization, with every offset indexing the exact text
analyzed. The `EnglishTextAnalysis`, `AnalyzedToken` and `AnalyzedNormalization` types, which carry the richer result of
`AnalyzeDetailed`, are documented with this unit.

**Data Model**: `EnglishTextAnalyzer` is a sealed class holding immutable fields: the `PartOfSpeechAdapter` seam, an
optional `TextNormalizer` (null means the shared default, resolved on first use), the `AnalysisDiagnostics` reporter,
flags saying whether part-of-speech annotation, normalization and the reading family are selected and whether the
reading classifier is effective (the family is selected and the model provider reports, without loading anything, that a
model source exists), the low-confidence threshold, the `HeteronymModelProvider` (the shared embedded one unless a test
supplied another) and the `PronunciationOverrides` dictionary (null when the options carry none or an empty one; the
dictionary is immutable, so keeping the reference is the snapshot). The one mutable field is an integer that is 1 once
the model warning was reported by this analyzer, changed only with `Interlocked`; no per-request state is kept.

- `AnalyzedToken` is a sealed record of `Start`, `Length`, `Text`, `PartOfSpeech` (a Universal Dependencies tag name,
  or null when the text was not tagged or part of speech was not selected), `Confidence` (0 when not tagged) and
  `IsKnownWord`, with the `init` properties `Clitic` (a `CliticKind`; `None` when normalization was not selected),
  `Reading` (the reading id, or null), `ReadingConfidence` (0 when there is no reading) and `IsLowConfidenceReading`
  (true for a classified reading below the threshold)
- `AnalyzedNormalization` is a sealed record of `Start`, `Length`, `Text` (the source slice), `SpokenText` and `Kind` (a
  `NormalizationKind`), with an `init` property `IsSpelled`: the same span as the `en.norm` or, when spelled, `en.spell`
  annotation. A pronunciation override is an annotation but not a replacement, so it is not listed
- `EnglishTextAnalysis` is a sealed class, constructible only by the analyzer, with read-only `Tokens`,
  `SentenceBreaks`, `Annotations` and `Normalizations` lists; `Normalizations` is empty when normalization was not
  selected
- `ReadingPass` is a private nested sealed class created once per request that owns the per-request reading state: the
  classifier once obtained (null when the model is unavailable), the reusable context array of the sentence being worked
  on, the four reading counts (classified, single-candidate, by override, below the threshold) and whether the model
  proved unusable. It creates no object per token

**Key Methods**:

- **EnglishTextAnalyzer()** and **EnglishTextAnalyzer(options)**: take the options (defaults for the parameterless
  form: part of speech, normalization and readings) and snapshot them. A null options value throws
  `ArgumentNullException`; a non-empty `Overrides` dictionary without the `Normalization` selection throws
  `ArgumentException` with the parameter name `options` (overrides are applied by the normalization pass, and ignoring
  them silently would hide a configuration mistake; an empty dictionary is accepted whatever the selection). Internal
  constructors additionally take the adapter, the normalizer and the heteronym model provider so tests can observe
  tagger creation, cancel inside normalization and supply a hand-built model
- **Analyze(text, cancellationToken)**: runs the shared core without detailed lists and returns its annotations and
  sentence breaks as a `TextAnalysisResult`; no `AnalyzedToken`, no `AnalyzedNormalization`, no per-token text copy and
  no clitic classification, because this is the path every synthesis request takes
- **AnalyzeDetailed(text, cancellationToken)**: runs the same core with a token list and a normalization list, so each
  token (with its clitic role and its reading) and each normalization is also materialized. Sharing one core keeps
  annotations, breaks, cancellation checkpoints, counts and thread safety from diverging
- **FlattenTokens(sentences)** and **NeutralizePlaceholders(text)**: private; the first builds the exact-size
  `NormalizationToken` array the normalizer takes, marking the first token of each tokenizer sentence, the second
  returns the text itself when it holds no `U+FFFC` and otherwise a same-length copy in which each placeholder is
  a space

**Algorithm of the shared core**: after rejecting a null text and an already-canceled token, neutralize the
placeholders and call `PartOfSpeechAdapter.Analyze` with the neutralized text and a flag that says whether to tag
(part of speech selected, or the reading classifier effective); tagging runs on the raw tokens. When normalization is
selected, flatten the tokens, resolve the dictionary (when one is configured) over the neutralized text into token
directives in one pass, and call `TextNormalizer.Normalize` with the directives, asking for clitic roles only for the
detailed path. Then walk the sentences and tokens in order:

1. Add the offset of the first token of each sentence after the first to the tokenizer breaks.
2. Decide the token's reading when the family is selected (see below) and count it; only for the detailed path add an
   `AnalyzedToken` whose `Text` is sliced from the original text.
3. When part of speech is selected and the token has a tag that is neither `PUNCT` nor `SYM` and contains a letter or
   digit (the tagger may label a bare `*` or an invisible character a word, but such a token speaks nothing of its
   own), count it as an unknown word when its lexicon flag is false and its tag is not `NUM`, and, unless it would
   start or end inside a surrogate pair, add one `en.upos` annotation over exactly the token.
4. When the token has a reading, add `en.reading` (the reading id) and `en.reading-confidence` (the confidence with
   three decimals and a period, invariant culture) over exactly the token.
5. Add one replacement annotation (the spoken text; `en.spell` for a spelled span, `en.norm` otherwise) for each
   normalized span that starts inside the token (a two-pointer walk over the ascending span list), and its
   `AnalyzedNormalization` for the detailed path; then one `en.pron` annotation (`scheme:phonemes`) for each
   pronunciation mark that starts inside the token (a second walk over the ascending mark list).

A token carries a replacement or a pronunciation, never both, and never a reading together with either, so at an equal
start the order is `en.upos`, then the reading pair, or the replacement or the pronunciation. A final flush adds any
span or mark left over. A span of the numeric shapes S3 and S4 (a currency symbol or an apostrophe with its adjacent
number token) starts inside the symbol token, which carries no `en.upos` annotation, so its `en.norm` annotation follows
the part of speech of the previous word and precedes that of the number, which keeps the annotations in text order.
Because tokens come from the tokenizer and spans and marks from the normalizer, an annotation never contains whitespace
or a placeholder; the surrogate-pair guard on `en.upos` is a defensive check (a reading token is an ASCII-letter form,
and the normalizer's builder enforces the same for its annotations). The sentence breaks are the tokenizer breaks
reconciled by `AlignedText.ReconcileBreaks` when normalization is selected and the tokenizer breaks unchanged otherwise;
a text without tokens yields no annotations and an empty, non-null break list. Finally report the counts (sentences =
reconciled breaks plus one when there is at least one token; tokens; annotations of all kinds; unknown words;
normalizations, the `en.norm` and `en.spell` spans together; ambiguous readings; numeric fallbacks; overrides applied;
overrides ignored), then the heteronym counts when any reading was assigned, then, once per analyzer, the model warning
when a model exists but could not be used, and return the lists as read-only.

**Heteronym readings in the core.** The reading of a token is decided before its annotations are added, in this
order, only when the reading family is selected:

1. **Overlap**: a token that starts before the end of an already emitted replacement or pronunciation, or inside which
   the next replacement or pronunciation starts, is skipped, so a reading never overlaps an `en.norm`, `en.spell` or
   `en.pron` annotation
2. **Directive**: a token with a `Reading` directive gets its id with confidence 1 and counts as an override; a token
   with any other directive gets no reading (the host's override owns the word). Directives are ascending, so one cursor
   serves the whole request
3. **Effectiveness**: without an effective classifier, or after the provider already reported that the model is
   unavailable, there is nothing more to do
4. **Form lookup**: the inventory's allocation-free whole-token lookup; a token that is not a form is skipped
5. **Cancellation and classifier**: for a form, the token is checked, then the classifier is obtained once per request
   from the provider. A status of `NotShipped` (no model source) turns the family off silently for the request;
   `Invalid` and `Incompatible` turn it off and mark the model as unusable, which causes the warning (once per analyzer)
6. **Single candidate**: a form with one candidate gets that reading with confidence 1 and counts as single-candidate
7. **Classification**: otherwise the tokens of the sentence are described into the reusable context array with
   `PartOfSpeechAdapter.DescribeSentence` (once per sentence that has a candidate), the classifier chooses the reading
   and the confidence, the reading counts as classified, and it counts as below the threshold when its confidence is
   lower than the threshold (it is emitted all the same)

**Error Handling**: a null text throws `ArgumentNullException`; a canceled token throws `OperationCanceledException`,
checked before any work, after the tagger is obtained, after each sentence is tagged (all in the adapter), every 256
visited tokens while the overrides are looked up and while the text is normalized, periodically while the tokens,
annotations and breaks are built, before each heteronym candidate word is handled and once more before the diagnostics
are reported, so a canceled request never returns a result and never reports diagnostics, wherever the cancellation
arrives. Nothing else is thrown by this unit for any text: the diagnostics sink's failures are contained by
`AnalysisDiagnostics`, and a model that cannot be used is a status, not an exception, so it disables the readings and is
reported once. A failure inside the tagging package propagates to the caller, and the Speech library's session contains
it and synthesizes without annotations; a failed tagger creation is not remembered (see *PartOfSpeechAdapter Design*),
so a later request retries. An exception from the normalizer or from the inventory (which can only mean a defective
build, such as a malformed embedded table) propagates the same way.

**Retention**: the analyzer keeps no reference to the analyzed text or to a result in its own state after the call
returns (no field holds either; this is a design obligation checked by review, and the observable side, that a request's
result and diagnostics never carry text of another request, is tested). The tagging package's own per-thread working
buffers are cleared by the pinned version (0.1.0-beta.5 or later; upstream issue
<https://github.com/demaconsulting/PartOfSpeech/issues/29>), which this repository cannot inspect through a public API;
see *SpeechTextEn System Design*, *Retention Verification*.

**Dependencies**: `PartOfSpeechAdapter` (splitting, tagging and `DescribeSentence`), `PronunciationOverrides` (the token
directives of the host's dictionary), the NormalizationSubsystem's `TextNormalizer`, `AlignedText` and the
`NormalizationKind` and `CliticKind` enumerations, the HeteronymSubsystem's `ReadingInventory`, `ContextToken`,
`HeteronymClassifier` and `HeteronymModelProvider`, `AnalysisDiagnostics`, `EnglishTextAnalyzerOptions` and
`TextEnAnnotationSelection`, `TextEnAnnotations` (the kind constants), and the Speech library's `ITextAnalyzer`,
`TextAnalysisResult`, `SegmentAnnotation` and `TextAnalysisConventions`. No PartOfSpeech type is named by this unit.

**Callers**: the Speech library's `SynthesisSession`, through the model's `ITextAnalyzer`; a model package that
wants the
detailed analysis; the test projects.
