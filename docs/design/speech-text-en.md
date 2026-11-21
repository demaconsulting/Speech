# SpeechTextEn System Design

This document provides the system-level design for the SpeechTextEn library.

![SpeechTextEn Structure](SpeechTextEnView.svg)

## Architecture

SpeechTextEn is a .NET library, published as the `DemaConsulting.Speech.Text.En` NuGet package (a pre-release; see
*Release*), that supplies an English implementation of the Speech library's `ITextAnalyzer` seam. A model package
returns an `EnglishTextAnalyzer` from `ISynthesisModel.TextAnalyzer`; the Speech library runs the analyzer once per
speech request over the whole request text and delivers the result to an annotation-aware synthesis backend as
language-neutral annotations on each speech segment. The system is a preview. It provides part-of-speech annotation,
authoritative sentence boundaries, alignment-preserving normalization of English text (abbreviations, symbols,
apostrophes, acronyms and initialisms, web addresses and identifiers that mix letters and digits, and en-US numbers,
ordinals, years, currency, percent, units, times and dates), a
spell-out annotation for letter sequences, host-supplied pronunciation overrides (whole words spoken as written,
replaced by plain words, spelled, given a phoneme string or given a fixed heteronym reading), and heteronym readings: a
choice of which reading a word such as `record` has, reported as a reading id with a confidence, supplied by a trained
model embedded in the package.

SpeechTextEn is a sibling system to Speech rather than a subsystem of it, for the same reasons the model packages are:
it is a separately built, separately packaged item with its own dependency, and the Speech library must never depend on
it. It uses the Speech library only through public API (`ITextAnalyzer`, `TextAnalysisResult`, `SegmentAnnotation` and
`TextAnalysisConventions`); the package has one project reference, to `DemaConsulting.Speech`, and none to ONNX Runtime
or any inference package, so any model package can use it.

The library consists of three runtime subsystems:

- **AnalysisSubsystem**: `EnglishTextAnalyzer` (with `EnglishTextAnalysis`, `AnalyzedToken` and
  `AnalyzedNormalization`), `EnglishTextAnalyzerOptions`, the `TextEnAnnotations` vocabulary and typed readers, the
  `PronunciationOverrides` dictionary (with `PronunciationOverrideEntry`, `OverrideAction` and `PronunciationScheme`),
  the internal `PartOfSpeechAdapter` that isolates the part-of-speech package, and the internal `AnalysisDiagnostics`
  reporter
- **HeteronymSubsystem**: the public `ReadingInventory`, the internal feature extractor with its hash functions, the
  internal sparse model with its strict file loader, and the internal classifier with its lazy model provider
- **NormalizationSubsystem**: the alignment model (which also declares the public `NormalizationKind` and `CliticKind`),
  the `TextNormalizer` that decides which spans are spoken differently, the `AbbreviationResolver` and
  `AcronymRecognizer` with their embedded lexicons, the `NumericExpressionRecognizer` and the `NumberVerbalizer`, the
  `WebAddressRecognizer` and the `AlphanumericTokenRecognizer`

The dependencies between the subsystems are one way: AnalysisSubsystem uses the other two, and neither knows anything of
the analyzer, the tagger or the diagnostics sink. NormalizationSubsystem and HeteronymSubsystem do not use the
part-of-speech package: the analyzer hands them token offsets and plain values (a tag name, a confidence). They do not
depend on each other.

Four further subsystems belong to the system but not to the package: the non-shipped trainer tool
(`tools/DemaConsulting.Speech.Text.En.Trainer`), modeled as subsystems because their requirements are system
requirements of this repository (the licensing and integrity of the training data, the reproducibility and gate of the
model trained from it, the pinned, evaluation-only measurement of that model, and the pinned and audited public-domain
corpus for later features):

- **TrainingDataSubsystem**: the `check-data` command that verifies the data in `data/text-en/`
- **TrainingSubsystem**: the `train` and `evaluate` commands that train the model, verify that a committed model is what
  retraining produces, and decide the quality gate
- **ExternalEvaluationSubsystem**: the `evaluate-external` command that measures the embedded model on external,
  evaluation-only datasets fetched from pinned revisions into a cache that is never committed
- **ClusteringSubsystem**: the `cluster` command that fetches a pinned public-domain corpus from an official mirror and
  audits it against every evaluation set

None is packed or loaded by a host. TrainingDataSubsystem shares no code with the library. TrainingSubsystem references
the library, and only the library: the Text.En assembly grants its internals to the trainer and its tests with
`InternalsVisibleTo`, so the trainer reuses the analyzer's own adapter, token description, feature extractor, scoring
and model format (see *Feature parity*). ExternalEvaluationSubsystem uses the evaluator, the corpus loader and the data
folder discovery. ClusteringSubsystem uses the deterministic logarithm, the strict file reader, the data loader and the
sentence keys, and the library's tokenizer and lookup hash. The runtime subsystems do not know the tool exists.

## External Interfaces

| Interface | Direction |
| --- | --- |
| `EnglishTextAnalyzer` (`ITextAnalyzer.Analyze`) | Inbound |
| `EnglishTextAnalyzer.AnalyzeDetailed` | Inbound |
| `EnglishTextAnalyzerOptions` | Inbound |
| `PronunciationOverrides` and its entry and enumeration types | Inbound |
| `TextEnAnnotations` | Inbound |
| `ReadingInventory` | Inbound |
| `NormalizationKind`, `CliticKind` | Inbound |
| `ITextAnalyzer`, `TextAnalysisResult`, `SegmentAnnotation` | Outbound |
| `ISpeechDiagnostics.Report` | Outbound |
| `DemaConsulting.PartOfSpeech` tokenizer and tagger | Outbound |
| Embedded lexicons, inventory tables and heteronym model | Internal |
| Pinned external dataset archives (trainer tool only, on request) | Outbound |

**Interface details:**

- `EnglishTextAnalyzer` (`ITextAnalyzer.Analyze`)
  - Format: Public method call
  - Constraints: Thread-safe; honors cancellation; throws only for a null argument or cancellation
- `EnglishTextAnalyzer.AnalyzeDetailed`
  - Format: Public method call
  - Constraints: Result carries words, normalizations, clitic roles and heteronym readings; local use only
- `EnglishTextAnalyzerOptions`
  - Format: Public record
  - Constraints: Validated on initialization; the default selects part of speech, normalization and readings (readings
    are effective where the analyzer has a model source, which the default analyzer has); carries the optional
    `Overrides` dictionary, which requires normalization, and the `LowConfidenceThreshold` from 0 to 1
- `PronunciationOverrides` and its entry and enumeration types
  - Format: Public classes and enumerations; `Create(entries)` and `FromTsv(reader)`
  - Constraints: Immutable and thread-safe; entries validated and bounded; errors give an index or line number and never
    content; see *Pronunciation Overrides*
- `TextEnAnnotations`
  - Format: Public constants and readers
  - Constraints: Closed kind vocabulary; unknown kinds are ignored; `en.norm` and `en.spell` follow the *Alignment
    Contract*; `en.pron` follows *Pronunciation Overrides*; `en.reading` and `en.reading-confidence` follow *Heteronym
    Readings*
- `ReadingInventory`
  - Format: Public sealed class (`Default`, `Version`, `ReadingIds`, `Contains`, `CandidatesFor`)
  - Constraints: Immutable and thread-safe; a reading id is compared exactly and a form with ASCII case folding
- `ITextAnalyzer`, `TextAnalysisResult`, `SegmentAnnotation`, `ISpeechDiagnostics.Report`
  - Format: Public Speech types and a method call
  - Constraints: Consumed; offsets are UTF-16 indices into the exact input; diagnostics are counts only and a failing
    sink is contained
- `DemaConsulting.PartOfSpeech` tokenizer and tagger
  - Format: Method call
  - Constraints: Internal to `PartOfSpeechAdapter`; no type of the package appears in a public signature
- Embedded lexicons, inventory tables and heteronym model
  - Format: Embedded resources (tab-separated tables; the model in the `HETW` binary format)
  - Constraints: Read once, lazily; a malformed table is a defective build; the model is strictly validated
- Pinned external dataset archives
  - Format: HTTPS download, reached only by `evaluate-external` and only when asked to fetch
  - Constraints: Never part of the package or of a request; every pinned file is verified by size and SHA-256 before it
    is used; only pinned paths are written, into a git-ignored or outside-the-repository cache

## Dependencies

SpeechTextEn has one project dependency, the Speech library, and one NuGet dependency (plus its own embedded tables -
two lexicons and the reading inventory - which are data written for this package, not a dependency):

- **DemaConsulting.PartOfSpeech** supplies the English tokenizer, the sentence splitter and the Universal Dependencies
  tagger with its embedded lexicon and weights. It is an OTS item of this repository (see *OTS Integration Design* and
  *PartOfSpeech Design*). Its use is confined to the internal `PartOfSpeechAdapter`, so the package can be replaced or
  upgraded without changing this library's API

The tagging package is pre-release software and is referenced with an exact-version pin, so a restore can never move to
a different build and silently change the analysis. NuGet does not allow a stable package to depend on a pre-release
package (NU5104), so until the tagging package publishes a stable version this package is packed and released only for a
pre-release version (see *Release*). The pin is never moved below the first version that clears the tagger's working
buffers (see *Retention*).

The trainer tool is not a dependency of the package; the dependency runs the other way. The tool references this library
(and, through it, the tagging package), and the library's `InternalsVisibleTo` entries name the trainer assembly and its
test assembly without adding anything to the public surface or the package.

## Risk Control Measures

N/A - SpeechTextEn provides no safety-critical functionality requiring risk control measures (IEC 62304 §5.3.3). It
annotates text for speech synthesis and has no clinical or safety role.

## Data Flow

**Analysis path (one speech request):**

1. **Input**: Speech's `SynthesisSession` has normalized the request text, parsed the audio tags and built the analysis
   text, in which every recognized tag is exactly one `U+FFFC` placeholder; it calls `EnglishTextAnalyzer.Analyze` on a
   thread-pool thread with the operation's cancellation token
2. **Neutralization**: each placeholder becomes a space of the same length, for tokenizing and tagging only, so every
   offset in the neutralized text is also an offset in the original
3. **Splitting**: `PartOfSpeechAdapter` splits the neutralized text into sentences and tokens (needing no tagger)
4. **Tagging**: when part-of-speech annotation is selected, or the reading family is effective (selected and a model
   source exists), the adapter tags each sentence, checking cancellation before each one. Tagging runs on the raw
   tokens, never on normalized text (see *Why tagging runs on raw tokens*)
5. **Normalization**: when selected, the analyzer hands the flattened tokens, the override directives (from
   `PronunciationOverrides.Resolve`, one per matching token) and the neutralized text to `TextNormalizer`. One linear
   pass applies the ordered rules (an override directive is rule 0 and wins for the spoken form) and returns the changed
   spans (replacement, kind, spelled or not), the pronunciation marks, the sentence-break edits, the ambiguity and
   numeric-fallback counts, the override counts and, for `AnalyzeDetailed`, the clitic roles. It needs no tagger
6. **Result building**: one `en.upos` annotation per word token (tokens tagged `PUNCT` or `SYM`, and tokens without a
   letter or digit, are skipped), followed by the token's reading pair when it has one, then the replacement annotations
   of the spans that start inside the token (`en.spell` or `en.norm`), then the `en.pron` of a pronunciation mark. So
   annotations are in text order and, at an equal start, part of speech precedes reading, replacement and
   pronunciation. Sentence breaks are the tokenizer's breaks **reconciled** with the normalization edits (every
   tokenizer or forced break, minus the suppressed ones)
7. **Reporting**: unless the request was canceled, `AnalysisDiagnostics` reports one counts-only event, one counts-only
   heteronym event when a reading was assigned and, once per analyzer, a warning when a model exists but is unusable
8. **Output**: a `TextAnalysisResult` whose annotations and non-null reconciled sentence breaks index the original text;
   the session re-bases them onto its segments and chunks, and an `IAnnotatedSynthesisBackend` receives them with each
   segment

**Why reconciliation exists.** The tokenizer treats titles such as `Dr.` and `St.` as never ending a sentence, so
`I live on Main St. It is nice.` is one tokenizer sentence. The normalizer knows how it read each abbreviation: a period
after one read as the last word of its sentence (`Street`) gets a forced break before the next capitalized word, and a
period after one read as a prefix (`Doctor`) never ends a sentence, so a tokenizer break there is suppressed. A name
suffix or credential directly followed by another (`John Smith Jr. Ph.D. spoke.`) behaves like a prefix, and month and
weekday abbreviations follow the same rule (`Jan. 5` is one date; `He left Mon. He came Tue.` keeps both sentence ends).
Without reconciliation the chunker would keep two sentences in one chunk or split inside `Dr. Smith`.

**Why tagging runs on raw tokens.** The tagger was trained on ordinary written text; tagging normalized text gives worse
tags and unknown words (`FBI` becomes `F`, `B`, `I`). Normalization and tagging are therefore independent consumers of
one tokenizer output: every `en.upos` stays on its source token, normalization never loads the tagger's data, and the
abbreviation and clitic context cues are lexical and orthographic only (never part-of-speech tags), so the result does
not depend on the options.

**Offline paths (not part of a request).** The trainer prepares every labeled row through the same units the analyzer
calls (neutralization, `PartOfSpeechAdapter`, `HeteronymFeatureExtractor.Extract`) and evaluates by running the real
analyzer and reading its reading annotations, so the measured result is what a user gets; `evaluate-external` does the
same for pinned external datasets after converting their rows and records counts only (see *Training Data, Training and
External Evaluation*).

## Alignment Contract

This section is normative. The `en.norm`, `en.spell` and `en.pron` annotations are a contract between the analyzer that
produces them and every backend that applies them. The invariants below are enforced by the NormalizationSubsystem (see
*NormalizationAlignment Design* for the enforcement point of each) and verified by its property tests. The analyzed text
is never rewritten: an `en.norm` or `en.spell` annotation names a span of the source text and the exact words or letters
that replace it when the text is spoken, and an `en.pron` annotation names a word and the phonemes to speak for it. The
two replacement kinds together are the *replacement family*.

- **A1**: The spans of one analysis are ascending and disjoint and lie within the text. **A1'**: the replacement
  annotations of one analysis, every `en.norm` and every `en.spell` together, are one such sequence
- **A2**: Every span is non-empty, starts and ends on a non-whitespace character, never contains a tag placeholder and
  never cuts a surrogate pair; its replacement is never null
- **A3**: The spans and the untouched gaps between them tile the text exactly; applying every span as its own source
  text returns the input unchanged
- **A4**: Nothing outside a span is changed, re-cased or re-punctuated; unchanged text is never annotated
- **A5**: A span is chunk-safe (A5'): it contains no whitespace and never `;`, and a `,` or `:` only when an ASCII digit
  directly precedes and follows it inside the span (`1,234.50`, `5:30`), which the core chunker never cuts. It has one
  of five shapes: **S1** one tokenizer token; **S2** one token and its adjacent period token; **S3** a currency symbol
  token and its adjacent number token (`$` `5.50`); **S4** an apostrophe token and its adjacent decade token (`'`
  `90s`); **S5** one of the two halves of a token split into two adjacent spans: the number or time and the meridiem
  letters of a glued meridiem (`5pm` gives `5` and `pm`), the same way an ampersand initialism is split into letter
  groups. A construct of several tokens (`January 5, 2024`, `$5 million`, `3 1/2`) is several spans, one per token or
  S3/S4 pair, in source order. So a span always lies wholly inside one chunk of the Speech library
- **A6**: Separation: where a letter or digit of the source touches a span, or the previous span directly touches it and
  its replacement ends with a letter (`A™®B`), and the replacement starts (or ends) with a letter, the replacement
  carries one separating space on that side (`he/she` gives the replacement `" slash "` for the `/`). Dropped and
  apostrophe replacements are never padded, and two adjacent replacements are separated by exactly one space (the later
  one pads its own left side, so no offset moves)
- **A7**: No silent drop: every source character is either copied verbatim (outside spans) or replaced by a non-empty
  replacement; the only empty replacements are `Dropped` spans over invisible format characters and bullets, and the
  only source periods that vanish are those consumed inside an abbreviation, initialism or abbreviated unit span. The
  digits, separators and symbols of a recognized numeric expression are consumed into its spoken words;
  numeric-looking text that is not recognized is never partially consumed
- **A8**: Break edits sit on token starts (non-whitespace, never a placeholder) strictly inside the text, ascending and
  unique; the reconciled sentence breaks are ascending, unique, on word starts and non-null. **A8b**: no reconciled
  break lies strictly inside a span
- **A9**: The analysis is deterministic and thread-safe: repeated and concurrent analysis of one text yields identical
  annotations and breaks; neither the analyzer nor the normalizer keeps per-request state
- **A10**: An `en.norm` value is non-empty except for a `Dropped` span, contains no tag placeholder and has no leading
  or trailing space except the A6 padding. The value of a numeric span (kinds `Number`, `Currency`, `DateTime` and
  `Unit`) contains no ASCII digit and only letters, spaces and the apostrophe of `o'clock`, and is at most 1,024
  characters long
- **A11**: A spelled span (`en.spell`) has a value that, minus at most one A6 padding space on either side, matches
  `[A-Z]( [A-Z])*`: single ASCII capital letters separated by single spaces, which are letter names, never words (`A` is
  the letter, not the article; `I` is the letter, not the pronoun). It is chunk-safe (A5')
- **A12**: The `en.pron` annotations of one analysis are ascending, pairwise disjoint and disjoint from every
  replacement annotation (a word is either replaced or pronounced, never both); each covers exactly one token, with no
  whitespace, placeholder or cut surrogate pair; its value is `<scheme>:<phonemes>` with a scheme from the closed list
  `ipa` and `arpabet`. `en.upos` stays independent of all of this and may overlap any annotation

**Enforcement.** The `AlignedTextBuilder` rejects a replacement span that is not ascending and disjoint from every
earlier span and pronunciation mark (A1', A12), a spelled span whose value is not a letter sequence (A11), and a
pronunciation mark with an edge, placeholder, surrogate, ordering or overlap defect, each with an `ArgumentException`. A
defective rule therefore fails the whole analysis of the request (the Speech session contains an analyzer exception and
speaks without annotations); it can never reach a backend as a misaligned annotation. The core library does not
validate overlap between annotations, so this check is the only one.

**Consumer algorithm.** To obtain the spoken text of a segment, a backend that reads the replacement family:

1. collects the annotations that `TextEnAnnotations.TryReadReplacement` accepts (`en.norm` and a well-formed `en.spell`,
   reporting whether the value is spelled) and sorts them by `Start` (they never overlap);
2. walks the sorted list, copying the text between the end of the previous span and the start of the next one unchanged;
3. at each span, appends the annotation's `Value` instead of the covered characters (a `Value` carries its own A6
   padding, so nothing is inserted between pieces, and an empty `Value` means speak nothing). When the reader reports
   `isSpelled`, the backend speaks each capital letter by its **letter name**, never as a word;
4. copies the text after the last span.

Independently, a backend applies an `en.pron` annotation (`TextEnAnnotations.TryReadPronunciation`) to the word it
covers: when it supports the scheme it uses the phonemes instead of its own letter-to-sound result, otherwise it speaks
the word as it would without the annotation. Annotations of other kinds, notably `en.upos`, are independent and may
overlap a replacement span; a backend ignores every kind it does not recognize.

**Spell-out is typed, never inferred.** Only an `en.spell` annotation (a reader result with `isSpelled` true) means
spell out. An `en.norm` value is always spoken as words, whatever letters it contains: a host's `Say` override whose
payload is a single capital letter (`Say("foo", "I")`) is the word `I`, and the reader reports `isSpelled == false` for
it. A backend never inspects the shape of an `en.norm` value to decide whether to spell it. The meridiem of a glued time
or hour is split into two adjacent spans so that the letters have their own kind: `5:30pm` is `5:30` (`en.norm`,
`five thirty`) and `pm` (`en.spell`, `P M`); a separate-token meridiem (`5 pm`, `p.m.`) is its own `en.spell` span.

**Double normalization.** A model that supplies an `EnglishTextAnalyzer` with normalization selected should keep
`ISpeechModel.NormalizeText` as the identity (the default): the Speech library applies that hook before analysis, so a
model that rewrote `Dr.` itself would hand the analyzer text in which the abbreviation no longer exists, and the two
normalizations would disagree. A model that selects part of speech only may keep its own hook.

## Spoken-Text Conventions

This section is normative for consumers. Every `en.norm` value produced for a numeric span (kinds `Number`, `Currency`,
`DateTime` and `Unit`) follows these conventions, so a phoneme backend sees only plain words and needs no number parser:

- **Plain words**: ASCII letters separated by single spaces, with **no hyphens, no commas, no digits and no other
  punctuation**: `twenty four`, never `twenty-four` or `24`. No clause punctuation can enter a value, so a value never
  changes how a backend splits clauses
- **Closed exceptions**: the apostrophe appears only in `o'clock`; capital letters appear only in the month names
  (`January`) and in `Fahrenheit` and `Celsius`; the meridiem letters `P M` and `A M` appear only as the value of their
  own `en.spell` span, never inside the words of the number or time
- **Bounded**: a numeric value is never longer than 1,024 characters
- **Padding**: a value may carry one leading or trailing space for the separation rule A6 (`50%` gives `fifty` for the
  digits and `percent` for the sign); a backend applies the values as they are
- **The word `and`** appears only in a currency amount with cents (`five dollars and fifty cents`) and in a mixed number
  (`three and one half`); there is no `and` inside a cardinal
- **Unrecognized numeric text stays as written**: a number the library does not read (a Roman numeral, a name before a
  number such as `mid-1990s`, a number beyond the limits) is left unannotated, and a backend must be prepared to
  receive digits for it. Numeric-looking text that cannot be read is counted as a numeric fallback

A numeric span covers the digits, separators and symbols of one token (or a currency symbol or apostrophe with its
adjacent number token); words of the same construct that are separate tokens get spans of their own: `$5 million` has
the span `$5` (`five`) and the span `million` (`million dollars`); `January 5, 2024` has the spans `5` (`fifth`) and
`2024` (`twenty twenty four`) and leaves `January` and the comma untouched. A consumer applies each span independently,
in text order. A spelled value is capital letters separated by single spaces; a `Say` override value (kind `Override`)
is the host's plain words (letters, combining marks, apostrophes, hyphens and single spaces, never a digit or clause
punctuation) and is never treated as spelled.

### Numeric Normalization

The library reads the numeric expressions of en-US text with a fixed, lexical policy (digits, separators, symbols,
units, month names and a few cue words; never the part of speech); every choice the words around a number do not decide
uses a documented default that is counted in the diagnostics. The families are integers (`1,234`, `-5`), decimals,
ordinals, years and decades (`in 1066` `in ten sixty six`), currency, percent and units, times, dates, digit groups and
month and weekday abbreviations (`Jan. 5`); the complete policy, every row and its limits are in
*NumericExpressionRecognizer Design* and *NumberVerbalizer Design*. What a consumer most needs to know:

- **Only the ASCII digits `0-9` are numbers**, and a number with letters mixed in (`3D`, `1e5`, `0x1F`, `Q3`) is left
  alone
- **en-US conventions only**: `,` separates thousands and `.` is the decimal point; a month comes before the day in a
  numeric date unless the first part can only be a day. A number written with other conventions (`1,23`, `1.234,5`) is
  left as written and counted as a numeric fallback
- **Years versus quantities**: a four-digit number from 1000 to 2099 is a year after a year cue and a quantity after a
  quantity cue or before a counted noun; with no cue it is a year (from 1100) or a cardinal (1000 to 1099), and the
  choice is counted as an ambiguous reading
- **Limits**: a cardinal has at most 15 digits; 16 to 64 digits are read digit by digit and counted as a fallback; more
  than 64 digits, a token over 128 characters or a decimal with more than 32 fraction digits is left as written and
  counted
- **Nothing is guessed silently**: readings chosen by default are counted as ambiguous readings, text left as written as
  numeric fallbacks, both counts only, never text

A span of the numeric kinds replaces whole tokens of the source: a period that ends a sentence after a number
(`Item 5.`) is never part of a span, and the period of a unit abbreviation (`5 ft.`) is consumed in the middle of a
sentence and kept, with a sentence break forced where a capitalized word follows.

## Pronunciation Overrides

This section is normative for hosts and for the units that implement it (*PronunciationOverrides Design*,
*TextNormalizer Design*): how a host overrides what the built-in rules do for a word, how an override is matched and how
the rules rank. The entry grammar, the limits, the tab-separated file format and the lookup are specified in
*PronunciationOverrides Design* and shown with examples in the user guide.

A host builds one immutable `PronunciationOverrides` dictionary (`Create` from entries, or `FromTsv` from a
tab-separated file) and sets it as `EnglishTextAnalyzerOptions.Overrides`. Each entry has a word (the key), a case flag,
an action and, for three actions, a payload. The text is never rewritten; the action decides which annotation, if any,
the matching word gets:

| Action | TSV word | Payload |
| --- | --- | --- |
| `Skip` | `skip` | none |
| `Say` | `say` | plain words |
| `SpellOut` | `spell` | none |
| `Pronunciation` | `pron` | `ipa:...` or `arpabet:...` |
| `Reading` | `reading` | a reading id such as `record.noun` |

- `Skip`: the word is spoken as written: every built-in abbreviation, acronym, symbol and number rule is bypassed and no
  replacement is produced (the abbreviation rules' sentence-break decision is still made)
- `Say`: one `en.norm` annotation (kind `Override`) whose value is the payload
- `SpellOut`: one `en.spell` annotation with the word's ASCII letters in capitals separated by single spaces
- `Pronunciation`: one `en.pron` annotation on the word; the word is not replaced
- `Reading`: one `en.reading` annotation with the payload and one `en.reading-confidence` of `1.000`; the word is spoken
  as written and no model is needed. It beats the classifier and is validated strictly when the dictionary is built (a
  well-formed id, active in the inventory, with the entry's word, ASCII-folded, among its forms). Its annotations are
  emitted only when the reading family is selected; the override is counted as applied in every case

**Precedence.** Each rank beats every rank below it: (1) an override entry, case-sensitive (exact UTF-16 code units);
(2) an override entry, case-insensitive (`OrdinalIgnoreCase`), consulted only when rank 1 has none; (3) the built-in
normalization rules in their existing order (dropped characters, clitics, numeric expressions, lexicon abbreviations, the
acronym table, symbols and marks, elision apostrophes); (4) part of speech, which is independent (`en.upos` is still
emitted for an overridden word); (5) heteronym classification, which skips every token that rank 1 to 3 replaced, spelled
or pronounced. One exception to rank 1 and 2 beating rank 3: a word that an **earlier** multi-token match has already
consumed. A number with its unit (`5 kg`), an amount with its magnitude word (`$5 million`) and a time with its meridiem
(`5 pm`) are one construct read as a whole, so an override for `kg`, `million` or `pm` in that position is **ignored and
counted** (`overrides ignored`); the same word on its own is overridden normally. An override on the first token of a
built-in construct always wins (an entry for `US` beats the acronym table), which is why a `Skip` entry is the way to stop
a rule from touching a word.

**Matching.** An entry is compared with one whole token of the analyzed text, in full: never a substring and never
several tokens (multi-word entries are not supported, because an annotation across whitespace could be cut by the core
chunker). The character before and after the token must not be a letter, a combining mark or a decimal digit, so the
stem of a split word (the `Do` of `Do` + `n't`) never matches. The text is tokenized exactly as for tagging, so a key
must survive tokenization as one token: `NASA's` matches the key `NASA`, while `Nginx-based` and `Wi-Fi` are whole
tokens that match only as a whole. A word with a trailing abbreviation period (`Dr.`, `U.S.`) cannot be a key; a word
followed by a split-off period (`Ph.D` and `.`) matches its key. A case-sensitive entry beats a case-insensitive one for
the same token. `OrdinalIgnoreCase` is culture-invariant (the Turkish dotted `İ` matches neither `I` nor `i`). A key
with an apostrophe also matches the typographic apostrophe spelling. Tag placeholders are separators.

**Sentence breaks.** Overrides never change sentence breaks: the reconciled breaks are identical with and without the
dictionary, including for an abbreviation whose period the tokenizer splits off (`Fig` and `.` in `See Fig. 5 for
details.`). An override replaces only the spoken form; the force or suppress decision of the abbreviation rule is still
applied, so the chunker never splits `See Fig.` from `5 for details.`.

**Phoneme scheme policy.** The value of an `en.pron` annotation is always `<scheme>:<phonemes>` with a scheme from the
closed list `ipa` and `arpabet`: a new notation is a new release of this library, never a host decision, so a backend
knows every scheme it may be offered. The library checks only the syntax of the phonemes; a backend that cannot use the
scheme or the phonemes speaks the word as it would without the annotation, and `TextEnAnnotations.TryReadPronunciation`
returns `false`, never an exception, for what it cannot use.

**Names and acronyms.** The built-in policy is the case-sensitive initialism table, the dotted and ampersand forms, the
shouting guard (an all-capital sentence suspends the ambiguous forms) and the vowel-less rule (*AcronymRecognizer
Design*). A word that policy gets wrong is corrected with an override, not a public policy type, which keeps the API
small: a product name said as words is `Say`, an initialism to spell is `SpellOut`, an acronym said as a word (`NASA`)
or the country `US` versus the pronoun `us` is a case-sensitive `Skip`. Case-sensitive entries are the safer default.

**Diagnostics.** The analysis event carries `overrides applied` and `overrides ignored`, both zero without a dictionary;
an ignored count above zero tells a host that an entry is shadowed by a number or unit rule. Only counts are reported;
the dictionary builders report only a position and a fixed reason.

## Heteronym Readings

This section is normative for hosts, backends and the implementing units (*HeteronymSubsystem Design*,
*EnglishTextAnalyzer Design*). A *reading* is one pronunciation of a written word (`record` as a noun or as a verb); the
**reading id** names it (`record.noun`, `record.verb`, `read.past`).

**Annotations and selection.** The analyzer marks a heteronym with two annotations on the **same span**: `en.reading`
whose value is the reading id, and `en.reading-confidence` whose value is the confidence written as `0.000` to `1.000`
(three decimals, invariant culture). A backend reads them with `TextEnAnnotations.TryReadReading` and
`TextEnAnnotations.TryReadReadingConfidence` (never an exception; the id reader does not look the id up in the
inventory) and may ignore the confidence. A reading id has two or three segments of `a` to `z`, `0` to `9` and `-`
separated by single dots, at most 64 characters; the text after the first dot is its role. The family is selected by
`TextEnAnnotationSelection.Reading`, part of the default selection, and is **effective only where the analyzer has a
model source**: the package embeds the trained model, so the default analyzer classifies every heteronym word with no
configuration. An analyzer with no source (`HeteronymModelProvider.None`) emits no classified reading, and a model that
cannot be used emits none and one warning. A `Reading` override is emitted with confidence `1.000` without any model.
`LowConfidenceThreshold` (default 0.60) only counts and flags a classified reading below it
(`AnalyzedToken.IsLowConfidenceReading`); the reading is **still emitted**, and a backend that wants to ignore uncertain
readings compares the confidence itself.

**Which words get a reading.** A token is a candidate when the whole tokenizer token is a form of the inventory,
compared with ASCII case folding only (`record-label` is one token and not a form). A candidate that is not overlapped
by a replacement or a pronunciation and carries no override other than a `Reading` override gets a reading: a form with
several candidate readings is **classified**; a form with exactly one candidate gets it with confidence `1.000`, but
only when the family is effective; a token with a `Reading` override gets the override's id with confidence `1.000`.

**Coexistence rules** (normative, verified by the analyzer tests):

1. `en.reading` and `en.reading-confidence` annotate exactly one whole tokenizer token, the same span for both; the span
   starts and ends on a letter, never contains whitespace, a comma, a colon or a semicolon, and lies in one chunk
2. They coexist with `en.upos` on the same span (the part of speech comes first)
3. They are never emitted for a token that overlaps an `en.norm`, `en.spell` or `en.pron` annotation, nor for a token
   with an override other than a `Reading` override
4. There is at most one of each per token, in text order; at an equal start the order is `en.upos`, `en.reading`,
   `en.reading-confidence`, then a replacement or pronunciation (which cannot coexist with a reading)
5. A backend ignores kinds it does not recognize and may ignore the confidence; a backend that cannot use a reading id
   speaks the word as it would without the annotation

**How the reading is chosen.** For a form with several candidates, the classifier extracts the hashed features of the
word in its own sentence (the word and its neighbors, the part of speech and closed classes around it, the position; see
*HeteronymFeatureExtractor Design*), binds them to each candidate through two salts (the full reading id and its role)
and adds the model weights its features select. The highest integer score wins and an exact tie goes to the first
candidate in the order of `forms.tsv`. The confidence is the softmax probability of the winner at the model's
calibration temperature (*HeteronymClassifier Design*). Reading needs the tagger, so the analyzer tags when part of
speech or an effective reading family is selected; `en.upos` stays conditioned on the part-of-speech selection alone.

**Feature parity (decision).** A trained model is valid only while the runtime extracts exactly the features the trainer
extracted. The feature extractor, the token description, the hash functions, the bucket of a feature and the integer
score of a candidate are therefore **one implementation**, `internal` in this assembly and called by the trainer through
`InternalsVisibleTo`; the step that turns tagged tokens into context tokens (`PartOfSpeechAdapter.DescribeSentence`) is
shared the same way. Rejected: a source-linked copy in the trainer (drift and a double review) and a public extractor
(it would freeze the feature set under semantic versioning). Three guards keep the parity checkable: golden feature
vectors under several cultures, a tokenization-parity test, and the evaluation's comparison of the real analyzer's
readings with the trainer's direct classification of every row (the parity counts).

**Inventory and model compatibility.** The inventory has an integer revision that a test pins with the counts of the
embedded tables. A model records the revision it was trained for and binds weights to reading ids through salts, never
to table positions; it is accepted when its revision is the same or lower than the inventory's, and a well-formed model
with another feature-set version is rejected as incompatible, not as invalid.

**Model format and delivery.** The model is a `HETW` file: a fixed little-endian header (format and feature-set
versions, a compression flag, inventory version, hash bits, calibration temperature, weight scale, counts and a checksum
of the stored bytes) and a payload of sparse buckets with one signed 16-bit weight each. The loader never trusts a file:
it checks every field and length against fixed caps before allocating, verifies the checksum before inflating, inflates
through a strict reader (a cut-off stream, trailing bytes, a size mismatch and an inflation bomb are detected) and
decodes the payload strictly; every defect is an `InvalidDataException` with a fixed message that never carries the data
(see *SparsePerceptronModel Design*). The model is an embedded resource, loaded **lazily and once** per process, only
when an analyzer that selects readings meets a candidate word. An absent model is silent; one that is invalid or
incompatible disables the family and is reported once.

**Diagnostics.** Besides the unchanged `TextEn.Analysis` event, `TextEn.Heteronym` (Info, at most one per request, when
a reading was assigned) reports
`Assigned {n} heteronym readings: {c} classified, {s} single-candidate, {o} by override, {l} below confidence {t}.`, and
`TextEn.Model` (Warning, once per analyzer, only when a model exists but cannot be used) reports
`Heteronym model unusable (invalid or incompatible file); readings disabled.` Both use only fixed words and numbers, and
a canceled request reports nothing. The reading state of one request lives in a private pass object on the caller's
thread; the model and the inventory are immutable and serve any number of concurrent requests.

## Training Data, Training and External Evaluation

The data, the trainer and the evaluations change nothing in the library's runtime except that the two small inventory
tables (`readings.tsv`, `forms.tsv`) and the trained model (`Resources/heteronyms.bin`) are embedded in the package;
nothing else of `data/text-en/` is embedded, referenced or shipped. The subsystem designs (*TrainingDataSubsystem*,
*TrainingSubsystem*, *ExternalEvaluationSubsystem*, *ClusteringSubsystem*) specify the rules; the decisions are:

- **Data and licensing (owner decisions).** `data/text-en/` holds the reading inventory (identifiers owned by this
  repository), labeled sentences split into training, development and two holdout files, provenance for every row and a
  mapping of external evaluation identifiers to readings (identifiers only). Every row carries a license (`CC0-1.0`,
  `Public-Domain` or `CC-BY-3.0-US`, with the attribution the last requires); no share-alike, non-commercial,
  no-derivatives or Misaki material is used. Content that DEMA Consulting generated or labeled (including readings
  chosen by language models, judged blind by a second model of another family; no human reviewed them) is dedicated to
  the public domain under CC0 1.0. The evaluation-only corpora are never committed
- **What `check-data` proves.** The committed files are exactly the reviewed files (a manifest of hashes pins them),
  every file and row is well formed and consistent with the inventory including a strict whole-word span, no license
  outside the allow-list is present, the splits share no sentence, document or template, and provenance, attribution and
  license statements exist. It also checks the regression overlay and the generalization set (their references, seen
  marks and novelty) and the pins and removals of the public-domain corpus. It runs in the lint script,
  in the build on every operating system and as a test over the real data. It does not prove a label is linguistically
  correct, that a harvested sentence really has its source's license, or freedom from overlap with an evaluation set
  nobody supplied
- **Training.** The trainer is a candidate-restricted averaged perceptron over the hashed features in integer arithmetic
  with its own seeded shuffle, exponential and logarithm (never `Math.Exp` or `Math.Log`), ordinal sorts and the
  invariant culture, so the same data, settings and seed give the same model on every platform and a committed model is
  verified by retraining (`train --check`) rather than trusted. Settings were fixed on the development set only, by a
  grid written down before it was run, and `TrainingCorpus.Load` opens exactly `train.tsv`, `dev.tsv`, `readings.tsv`
  and `forms.tsv`, so the holdout sets cannot influence a model by construction
- **Evaluation and the quality gate.** A row is decided by the real `EnglishTextAnalyzer` over the model under test,
  reading the reading annotation on exactly the row's span (no reading counts as wrong). Baselines are the own
  part-of-speech baseline, the majority reading in `train.tsv` and the first candidate; the improvement over the
  part-of-speech baseline has a paired document-clustered bootstrap interval computed in integers. On the natural
  holdout the gate requires a pooled end-to-end accuracy of at least 930 per mille and an interval lower bound exactly
  above zero; the gate settings are fixed before the first holdout evaluation and never changed by a result. The
  independent holdout and the seed study are reported, not gated
- **Evaluation overlays (owner decisions).** Two further sets are measured, never gated: a regression overlay that
  references owner-flagged sentences, golden targets and existing rows by identifier and marks each as seen or unseen in
  training (not a new split, so the separation checks keep holding), and a generalization set written blind before any
  training that uses word classes, whose novelty against the training and development data `check-data` proves. Each is
  measured with and without the hand-written cue priors, with intervals that resample a family of rows together, and the
  results are appended to the evaluation record without changing its earlier lines. These are the baseline of the
  criterion, recorded in the evaluation README before any training that uses word classes, by which a later model with
  word classes is judged
- **Committed files.** The model, the training record and the evaluation record (`docs/evaluation/text-en/`) are
  committed, and CI runs `train --check` and `evaluate --check` on every operating system, so a change of the data, the
  tagger, the extractor or the trainer that makes them stale fails the build. Replacing the model is a deliberate act;
  experiments use `--model-dir`, which a guard requires, so they never overwrite the committed files. The evaluation
  record states the figures and their limits (labels not human verified, tagger exposure to part of the natural holdout,
  small counts per reading), and the limits travel with every figure
- **External evaluation (owner decisions).** The model is also measured on two external, evaluation-only datasets
  (WikipediaHomographData and heteronym-bench), so the figures are not only those of data written for this repository;
  they are informational and never gate a build or release. The datasets are fetched at evaluation time with `--fetch`
  into a git-ignored or outside-the-repository cache; the repository commits, embeds and ships none of their content;
  the input is exactly one pinned revision (the commit, and every file by size and SHA-256) and a cache that is missing
  or differs is refused with exit code 2 and no figure; the only committed traces are the pins and the mappings of their
  labels to this repository's reading ids; neither the output nor the record contains a sentence, a target word, an item
  identifier or a dataset label; the external rows are never trained on or used to choose a setting. A row that cannot
  be used is skipped, counted by reason and reported. The caveats travel with the figures (a domain shift, a coarser
  mapping, far fewer words than the inventory, labels not all verified by a person, clustering by sentence). The command
  needs the network once and third-party data, so no build, lint or workflow runs it
- **Public-domain corpus (owner decisions).** The `cluster` command fetches US public-domain books only from an
  official mirror with a polite rate limit, pins each by the hash of its normalized body, excludes every book that holds
  a development or natural-holdout sentence, and lists every corpus sentence that repeats or quotes an evaluation
  sentence for review; the audit is deterministic for any thread count (see *ClusteringSubsystem Design*). A book whose
  mirror text no longer matches its pin fails the fetch and is re-pinned only after review. Fetching and auditing are
  manual steps that no build runs; instead `check-data` (in CI) checks the committed pins and removals offline. Word
  classes induced from this corpus were tried and removed after they did not improve the model (see the evaluation
  README); the corpus is kept for later features and changes no model

## Design Constraints

- **Speech must never depend on SpeechTextEn**: the dependency direction is one way
- **Offsets are the contract**: all offsets are UTF-16 code unit indices into the exact analyzed text, and the analyzer
  never rewrites or re-indexes it (see *Alignment Contract*), whatever the input
- **Placeholders are separators, not text**: the words on both sides of a tag are analyzed as one running text, so
  context such as the words around "[whispers]" is preserved; a sentence break is placed at the next word, never at the
  placeholder
- **Diagnostics are counts only**: the library's diagnostics contract forbids synthesized text in any report, so the
  analyzer reports counts and one fixed warning about an unusable model, never a word, a spoken replacement, a reading
  id or dictionary content; detailed per-word data is available only through `AnalyzeDetailed`, which returns it to the
  caller
- **Linear time**: every normalization rule looks at a constant number of neighboring tokens (numeric scanners behind a
  128-character token limit, no regular expression), the override lookup visits each token once with a logarithmic
  search, and both check the cancellation token every 256 visited tokens, so the pass is linear in the text length,
  including for adversarial repetitions, digit-heavy texts and one huge token, and a canceled request stops
- **Overrides need normalization**: a non-empty `Overrides` dictionary with `Normalization` not selected makes the
  `EnglishTextAnalyzer` constructor throw `ArgumentException` (parameter name `options`), because silently ignoring it
  would hide a configuration mistake; an empty dictionary is harmless
- **US English only**: the lexicons, spoken forms and context cues are those of written US English; other varieties and
  languages are passed through unchanged
- **First-use latency**: the shared tagger inflates and validates about 1.3 MB of embedded data on first use. It is not
  loaded at construction, for a text without tokens, or when neither part-of-speech annotation nor an effective reading
  family is selected, and no warm-up API is provided (a host can analyze one short text at start-up). The lexicons and
  the inventory are parsed once, on first use, and a model is loaded once on the first candidate word
- **Immutable and thread-safe**: an analyzer keeps no per-request state, copies its options at construction, keeps no
  reference to an analyzed text or a result after the call returns, and serves any number of concurrent requests; the
  normalizer, the lexicon units, the inventory and a loaded model are immutable after loading
- **Platform**: the library targets the same frameworks as the Speech library (`net8.0`, `net9.0`, `net10.0`, `net11.0`)
- **No command-line or demo integration of its own** in this release, and no inference dependency (see
  *Architecture*); the command-line tool and the demo application use it only through the Kokoro and Piper model
  packages, which build on it
- **Compliance**: all functionality must be traceable to requirements

### Not Provided

Everything below is passed through unchanged (never annotated) or not provided, so a host does not assume it; the
pass-through items are asserted by golden rows, so a later increment changes them knowingly:

- Markup and the hyphen and dash policy; web addresses other than a URL with an `http`, `https` or `ftp` scheme or a
  leading `www`, an e-mail address, or a bare domain with one of a short list of top-level domains (`report.pdf` and
  `Node.js` stay as written, and so does a domain such as `example.xyz`); a colon is never inside a span, so the
  colon of a URL stays a pause mark for the core chunker (the pieces that are read are described in
  *WebAddressRecognizer Design*)
- Roman numerals and names before a number (`mid-1990s`): no safe lexical rule exists without a name and
  part-of-speech lexicon (the number of a hyphenated compound such as `12-year-old`, identifiers such as `A4` and
  names joined to a number such as `COVID-19` are read, see *AlphanumericTokenRecognizer Design*)
- Prefix currency codes (`USD 5`); `h:mm:ss` times, ratios, non-ASCII digits, `1e5`, `0x1F`, bare magnitude letters
  after a decimal (`1.5M`), feet and inches, decimal-comma locales, and single-letter or word-like units (`m`, `g`,
  `in`), which are too ambiguous after a number
- Heteronyms outside the inventory, and context beyond the word's own sentence. The model depends on the tagger (its
  features use the tag and its confidence), so another tagger version needs a retrained model (the build reports a
  feature-set mismatch as incompatible and `train --check` reports a drift of the feature stream). The model's accuracy
  is the figures of the committed evaluation record with the limits stated there; a host that needs a specific reading
  fixes it with a `Reading` override
- Multi-word override entries, keys with a trailing period, overrides of the unit, magnitude or meridiem token of a
  number construct (ignored and counted), sub-word matching, and built-in rules for plural initialisms (`DVDs`) or
  acronyms spoken as words (`NASA`), which a host overrides

### Retention

The core `ITextAnalyzer` contract requires an analyzer to not retain the text. Three parts carry that property:

- **What a host can observe** (requirement `SpeechTextEn-Isolation-RequestNoninterference`): the result and diagnostics
  of a request depend only on that request, equal what a fresh analyzer gives and never carry text of another request,
  also under concurrent use. Tests assert exactly that; none uses garbage-collector timing, weak references or
  reflection, because those measure object lifetime and not an output contract
- **This library's own state**: no field holds an analyzed text or a result after a call returns (per-request objects
  only, a shared reporter that formats counts, no caches keyed by text). This is a design obligation checked by review
  of the unit designs, not by an automated test
- **The tagging package's working buffers**: early versions of the tagging package kept the characters of the tokens
  they tagged in thread-static buffers that were never cleared (upstream issue 29); a later version clears them in a
  `finally` block on every public entry point (upstream pull request 30), and the project pins a version that does. The
  buffers are private to the dependency with no public inspection hook, so this repository has no test over them: the
  upstream fix's regression tests, cited in *PartOfSpeech Verification*, are the evidence, and the remaining gap
  (managed strings the dependency itself creates are outside what it can clear) is stated there for the repository
  owner's approval

The pin is never moved below the first version that clears its buffers. The requirement
`SpeechTextEn-Packaging-PinnedDependency` verifies only that the built package pins one exact version; the minimum is a
dependency policy kept by review of the upstream evidence.

### Release

The package is packable only for a pre-release version. The project sets `IsPackable` to `false` and turns it back to
`true` only when the effective package version (`PackageVersion` when given, as the workflows do, otherwise `Version`)
has a pre-release label (a hyphen before any build metadata); the local default `0.0.0` is stable. With a stable version
the project is skipped and every other package still packs. The shared release workflow packs every packable project
with one version, which may be stable, and NuGet refuses a stable package that depends on a pre-release one (NU5104), so
without the guard a stable release would fail or publish an invalid package. The guard is temporary: it goes once the
tagging package publishes a stable version and the pin moves to it. The build's pack verification evaluates `IsPackable`
with MSBuild and checks that a `.nupkg` is produced, and two FileAssert tests inspect the built package:
`TextEn_PackageLicenseFiles_Content` (the license file and notices at the package root and the `.nuspec` license
reference) and `TextEn_PackageDependencies_Nuspec` (the exact-version pin and no inference runtime); for a stable
version no package is built and both accept its absence.

### Package License Files

The package contains code under the MIT License and embeds two small lexicon tables written for it under the same
license, the reading inventory tables (written by DEMA Consulting and dedicated to the public domain under CC0 1.0) and
the trained heteronym model, whose weights are numbers computed from labeled sentences (it contains no sentence text).
The training sentences are partly adapted from the Manually Annotated Sub-Corpus (CC BY 3.0 US), partly public domain
and mostly CC0 1.0 (generated or labeled by DEMA Consulting).

The package therefore declares `MIT AND CC-BY-3.0-US` through a license file, `PACKAGE-LICENSE.md`, that starts with the
SPDX line, contains the MIT license text, states that the inventory is CC0-1.0 and that the embedded model is provided
under the terms of CC BY 3.0 US, and states that the required tagging package is a separate dependency with its own
license files. `THIRD-PARTY-NOTICES.md` carries the corpus attribution and the changes made, the CC0 1.0 dedication of
the generated and labeled content, and the notice of the data embedded in the tagging package (Open English WordNet
under CC BY 4.0, derived from Princeton WordNet, and the same corpus). Both files are packed at the package root and are
packaging files, not software items.

**Why the model carries CC BY 3.0 US (decision, treated conservatively).** Whether trained weights are a derivative of
their training data is not settled, so the package treats them as derived from the corpus-adapted sentences and provides
the model under that license's terms, mirroring the tagging package's own composite declaration. This package has no CC
BY 4.0 content of its own (that belongs to the data embedded in the dependency), so the declaration omits it. This is a
design choice and not legal advice; the license position is the repository owner's decision and is revisited if the
training data changes.
