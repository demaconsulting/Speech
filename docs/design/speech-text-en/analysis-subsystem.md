## AnalysisSubsystem Design

![AnalysisSubsystem Structure](TextEnAnalysisSubsystemView.svg)

### Overview

The SpeechTextEn AnalysisSubsystem turns the text of one speech request into the language-neutral
analysis the Speech library carries to a synthesis backend: part-of-speech annotations on the words and
authoritative sentence boundaries. It contains the following units:

- **EnglishTextAnalyzer**: the public `ITextAnalyzer` implementation, together with the
  `EnglishTextAnalysis` and `AnalyzedToken` result types of its detailed analysis
- **EnglishTextAnalyzerOptions**: the public options record, with the `TextEnAnnotationSelection` flags
  enumeration
- **TextEnAnnotations**: the public annotation kind constants and the typed part-of-speech reader
- **PartOfSpeechAdapter**: the internal seam that isolates the `DemaConsulting.PartOfSpeech` package
- **AnalysisDiagnostics**: the internal reporter of one counts-only event per request

### Interfaces

The subsystem exposes `EnglishTextAnalyzer`, `EnglishTextAnalysis`, `AnalyzedToken`,
`EnglishTextAnalyzerOptions`, `TextEnAnnotationSelection` and `TextEnAnnotations` as its public API;
`PartOfSpeechAdapter` and `AnalysisDiagnostics` are internal. It consumes the Speech library's
`ITextAnalyzer`, `TextAnalysisResult`, `SegmentAnnotation`, `TextAnalysisConventions` and
`ISpeechDiagnostics` (see _Speech SynthesisSubsystem Design_ and _Speech Diagnostics Design_) and, only
inside `PartOfSpeechAdapter`, the tokenizer and tagger of `DemaConsulting.PartOfSpeech` (see
_PartOfSpeech Design_). The public classes live in the `DemaConsulting.Speech.Text.En.AnalysisSubsystem`
namespace, following the convention of the Speech library's subsystem namespaces.

### Design

`EnglishTextAnalyzer` is the composition point. Its constructors take the options (the parameterless
constructor uses the defaults) and snapshot what they need: whether part-of-speech annotation is selected,
an `AnalysisDiagnostics` over the options' sink, and the adapter (the process-wide shared adapter in
production, a test-supplied one through an internal constructor).

Analyzing a text is a fixed sequence (see _EnglishTextAnalyzer Design_ for the algorithm):

1. Reject a null text and an already-canceled token before any work.
2. Neutralize placeholders: every `U+FFFC` becomes a space of the same length, so the tokenizer treats it
   as a separator and no offset moves.
3. Ask the adapter to split (and, when selected, tag) the neutralized text. Splitting alone needs no
   tagger; the adapter obtains the shared tagger lazily, once, only when a sentence is actually tagged.
4. Build the tokens, the annotations (one `en.upos` per token that is not tagged `PUNCT` or `SYM`) and the
   sentence breaks (the first token of every sentence after the first), all from the original text.
5. Check the token one last time, report one counts-only event through `AnalysisDiagnostics`, then return
   the result.

`Analyze` returns the language-neutral `TextAnalysisResult`; `AnalyzeDetailed` returns the same analysis
with every token's text, tag, confidence and known-word flag, which only the caller sees. The sentence
breaks are always non-null so the Speech library treats the analyzer's opinion as authoritative, and the
annotation selection never changes them.

**Unit collaboration.** `EnglishTextAnalyzerOptions` configures the analyzer; `TextEnAnnotations` supplies
the kind string the analyzer stamps on each annotation and the reader a backend uses to read it back;
`PartOfSpeechAdapter` is the only unit that names a PartOfSpeech type; `AnalysisDiagnostics` is the only
unit that talks to the diagnostics sink. This separation is what lets a later increment add normalization
or readings by adding units beside these, and lets the tagging package be replaced by changing one unit.

**Concurrency.** Every unit is immutable after construction. The only shared mutable state is the adapter's
tagger field, created under a lock with a null check (not `Lazy<T>`, which would cache a failed creation
for the process lifetime; see _PartOfSpeechAdapter Design_), so concurrent first requests create it exactly
once, a failed attempt is retried by a later request, and tagging itself is documented by the package as
safe for concurrent use.

**Cancellation.** The analyzer and the adapter check the token before any work, after the tagger is
obtained, after each sentence is tagged and periodically while the result is built, and the analyzer checks
it one last time before reporting, so a canceled request throws `OperationCanceledException` at the next
check, never returns a result and never reports diagnostics, including when the cancellation arrives during
tagger initialization or during the only or last sentence.

**Dependency boundary.** The adapter's constructor takes a tagger factory so tests can count tagger
creation without disturbing the process-wide tagger; the shared adapter's factory is the package's own
shared default tagger.
