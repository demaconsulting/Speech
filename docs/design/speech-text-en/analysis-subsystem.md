## AnalysisSubsystem Design

![AnalysisSubsystem Structure](TextEnAnalysisSubsystemView.svg)

### Overview

The SpeechTextEn AnalysisSubsystem turns the text of one speech request into the language-neutral
analysis the Speech library carries to a synthesis backend: part-of-speech annotations on the words,
normalization annotations that name how abbreviations, symbols, apostrophes and initialisms are spoken, and
authoritative sentence boundaries reconciled with that normalization. It contains the following units:

- **EnglishTextAnalyzer**: the public `ITextAnalyzer` implementation, together with the
  `EnglishTextAnalysis`, `AnalyzedToken` and `AnalyzedNormalization` result types of its detailed analysis
- **EnglishTextAnalyzerOptions**: the public options record, with the `TextEnAnnotationSelection` flags
  enumeration
- **TextEnAnnotations**: the public annotation kind constants and the typed part-of-speech and
  normalization readers
- **PartOfSpeechAdapter**: the internal seam that isolates the `DemaConsulting.PartOfSpeech` package
- **AnalysisDiagnostics**: the internal reporter of one counts-only event per request

The text normalization itself is not part of this subsystem: the analyzer calls the NormalizationSubsystem
(see _SpeechTextEn NormalizationSubsystem Design_), which is one-way, and the analyzer is what turns its
decisions into annotations and breaks.

### Interfaces

The subsystem exposes `EnglishTextAnalyzer`, `EnglishTextAnalysis`, `AnalyzedToken`,
`AnalyzedNormalization`, `EnglishTextAnalyzerOptions`, `TextEnAnnotationSelection` and `TextEnAnnotations` as
its public API; `PartOfSpeechAdapter` and `AnalysisDiagnostics` are internal. It consumes the Speech
library's `ITextAnalyzer`, `TextAnalysisResult`, `SegmentAnnotation`, `TextAnalysisConventions` and
`ISpeechDiagnostics` (see _Speech SynthesisSubsystem Design_ and _Speech Diagnostics Design_), the
NormalizationSubsystem's `TextNormalizer`, `AlignedText` and public `NormalizationKind` and `CliticKind`
enumerations, and, only inside `PartOfSpeechAdapter`, the tokenizer and tagger of
`DemaConsulting.PartOfSpeech` (see _PartOfSpeech Design_). The public classes live in the
`DemaConsulting.Speech.Text.En.AnalysisSubsystem` namespace, following the convention of the Speech
library's subsystem namespaces.

### Design

`EnglishTextAnalyzer` is the composition point. Its constructors take the options (the parameterless
constructor uses the defaults, which select part of speech and normalization) and snapshot what they need:
whether part-of-speech annotation is selected, whether normalization is selected, an `AnalysisDiagnostics`
over the options' sink, the adapter (the process-wide shared adapter in production, a test-supplied one
through an internal constructor) and, for tests only, an optional `TextNormalizer` (null means the shared
default, resolved on first normalization so constructing an analyzer loads no lexicon).

Analyzing a text is a fixed sequence (see _EnglishTextAnalyzer Design_ for the algorithm):

1. Reject a null text and an already-canceled token before any work.
2. Neutralize placeholders: every `U+FFFC` becomes a space of the same length, so the tokenizer treats it
   as a separator and no offset moves.
3. Ask the adapter to split (and, when selected, tag) the neutralized text. Splitting alone needs no
   tagger; the adapter obtains the shared tagger lazily, once, only when a sentence is actually tagged.
   Tagging runs on the raw tokens, never on normalized text.
4. When normalization is selected, flatten the sentences into one token-offset array and call the
   NormalizationSubsystem's `TextNormalizer` with the neutralized text. This needs no tagger and does not
   read the tags. Clitic roles are requested only for `AnalyzeDetailed`.
5. Build the annotations in one walk in text order: one `en.upos` per token that is not tagged `PUNCT` or
   `SYM` and has a letter or digit, immediately followed by the `en.norm` annotations of the spans that start inside that token, so at
   an equal start the part of speech comes first and a part of speech may overlap a normalization. Build
   the sentence breaks (the first token of every sentence after the first), reconciled with the
   normalization's break edits when normalization is selected, all from the original text; only
   `AnalyzeDetailed` also materializes each token and each normalization.
6. Check the token one last time, report one counts-only event through `AnalysisDiagnostics`, then return
   the result.

`Analyze` returns the language-neutral `TextAnalysisResult` through the shared core without creating any
per-token object or text copy; `AnalyzeDetailed` runs the same core and returns the same analysis
with every token's text, tag, confidence, known-word flag and clitic role, and every normalization with its
source text, spoken text and kind, which only the caller sees. The sentence breaks are always non-null so the
Speech library treats the analyzer's opinion as authoritative. Selecting `PartOfSpeech` or nothing leaves the
tokenizer's breaks unchanged; selecting `Normalization` reconciles them, and the part-of-speech selection never
changes them.

**Unit collaboration.** `EnglishTextAnalyzerOptions` configures the analyzer; `TextEnAnnotations` supplies
the kind strings the analyzer stamps on each annotation and the readers a backend uses to read them back;
`PartOfSpeechAdapter` is the only unit that names a PartOfSpeech type; `AnalysisDiagnostics` is the only
unit that talks to the diagnostics sink; the NormalizationSubsystem is the only code that decides how text
is spoken. This separation lets a later increment add readings by adding units beside these, and lets the
tagging package be replaced by changing one unit.

**Concurrency.** Every unit is immutable after construction. The only shared mutable state is the adapter's
tagger field, created under a lock with a null check (not `Lazy<T>`, which would cache a failed creation
for the process lifetime; see _PartOfSpeechAdapter Design_), so concurrent first requests create it exactly
once, a failed attempt is retried by a later request, and tagging itself is documented by the package as
safe for concurrent use. The shared default normalizer and its two lexicons are immutable after a one-time,
thread-safe load (see _SpeechTextEn NormalizationSubsystem Design_).

**Cancellation.** The analyzer, the adapter and the normalizer check the token before any work, after the
tagger is obtained, after each sentence is tagged, periodically while the text is normalized (every 256
tokens) and while the result is built, and the analyzer checks it one last time before reporting, so a
canceled request throws `OperationCanceledException` at the next check, never returns a result and never
reports diagnostics, including when the cancellation arrives during tagger initialization, during
normalization or during the only or last sentence.

**Dependency boundary.** The adapter's constructor takes a tagger factory so tests can count tagger
creation without disturbing the process-wide tagger; the shared adapter's factory is the package's own
shared default tagger.
