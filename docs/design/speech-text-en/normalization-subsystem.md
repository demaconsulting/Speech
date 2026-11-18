## NormalizationSubsystem Design

![NormalizationSubsystem Structure](TextEnNormalizationSubsystemView.svg)

### Overview

The SpeechTextEn NormalizationSubsystem decides which spans of an English text are spoken differently from
how they are written (abbreviations, symbols, apostrophes, acronyms and initialisms, invisible characters,
and numbers, ordinals, years, currency, percent, units, times and dates) and records those decisions as spans
with their spoken replacements, together with the sentence-break edits that follow from them. It never
rewrites the text and never consults the part-of-speech tagger. Host-supplied pronunciation overrides arrive
as token directives that the normalizer applies before any built-in rule. It contains the following units:

- **NormalizationAlignment**: the alignment model: the public `NormalizationKind` and `CliticKind`
  enumerations, the internal token, span, pronunciation-mark and directive values, the
  `AlignedTextBuilder` that enforces the alignment contract and the immutable `AlignedText` result
- **TextNormalizer**: the internal orchestrator; one linear pass applies the host's token directives first
  and then the ordered rules (invisible characters, clitics, numeric expressions, abbreviations, acronyms and
  initialisms, symbols and marks, elision apostrophes) and the period and sentence rules
- **NumericExpressionRecognizer**: the internal, stateless recognizer of numeric expressions: token-local
  scanners with bounded look-behind and look-ahead that decide whether a token starts a number, currency
  amount, percent, unit, time, date or digit group, how each token of it is spoken and whether the reading
  was a counted default or a numeric fallback
- **NumberVerbalizer**: the internal, stateless builder of the English words of a number (cardinals,
  ordinals, years, decades, decimals, digit sequences, fractions, clock times and dates) over a bounded stack
  buffer
- **AbbreviationResolver**: the internal embedded abbreviation lexicon and the neighbor-based choice among
  the readings of `St.`, `Dr.`, `Sr.`, `No.` and of the month and weekday abbreviations
- **AcronymRecognizer**: the internal embedded initialism lexicon and the case-sensitive rules for dotted,
  ampersand and vowel-less all-capital forms

### Interfaces

The subsystem exposes only the `NormalizationKind` and `CliticKind` enumerations publicly, in the
`DemaConsulting.Speech.Text.En.NormalizationSubsystem` namespace; they are declared in
`NormalizationAlignment.cs` because the public analysis result of the AnalysisSubsystem names them. Everything
else is internal: the analyzer calls `TextNormalizer.Normalize(text, tokens, classifyClitics,
cancellationToken, directives)` with the neutralized text, an array of token offsets and the (possibly
empty) token directives of the host's pronunciation overrides, and reads the returned
`AlignedText` (spans, pronunciation marks, forced and suppressed breaks, clitic marks, ambiguity count, numeric
fallback count, the counts of overrides applied and ignored, and
`ReconcileBreaks`). The subsystem consumes nothing but the Speech library's `TextAnalysisConventions` (for
the tag placeholder character) and the framework: its production code does not reference the
`DemaConsulting.PartOfSpeech` package, the analyzer, the dictionary, the diagnostics reporter or the options.
A directive is a plain value (token index, kind, optional text), so the dictionary and its matching rules stay
entirely in the AnalysisSubsystem. Only the tests use the real tokenizer of that OTS package (see *PartOfSpeech
Design*), through a test helper, to produce the token offsets the analyzer would supply.

### Design

`TextNormalizer.Normalize` validates its arguments, the token list and the directive list (strictly
ascending, each naming an existing token), creates one `AlignedTextBuilder` and one `Pass` over the tokens, and
returns the builder's `AlignedText`. For each token, in order, the first matching rule wins (see *TextNormalizer
Design* for the rule order and every table):

0. a **user directive** for the token (speak as written, replace, spell, pronounce or keep a reading, which is
   spoken as written) wins over every rule below for the spoken form, while the sentence-break edits of rules 3
   and 4 are still made for the token (decision-only, nothing emitted or counted); a directive whose token an
   earlier multi-token match has consumed is ignored and counted;
1. invisible characters and bullets are dropped explicitly;
2. clitic tokens have their typographic apostrophe canonicalized and, on request, their role recorded;
2a. numeric expressions are recognized by `NumericExpressionRecognizer`, which says how each token of the
   expression is spoken; `NumberVerbalizer` builds the words; a unit that may carry an abbreviation period
   uses the same period and sentence rules as an abbreviation;
3. lexicon abbreviations (including the month and weekday abbreviations) are resolved by
   `AbbreviationResolver` from the neighboring tokens, and the period and sentence rules decide the span
   extent and the break edits;
4. acronyms and initialisms are recognized by `AcronymRecognizer` with the same period and sentence rules
   for dotted forms; an ampersand initialism is emitted as spelled letter-group spans and `and` spans;
5. symbols, marks and the slash and ampersand inside a word are spoken as words;
6. typographic apostrophes inside a word (elisions) are canonicalized;
7. everything else is identity, including every number the recognizer does not read.

Every span goes through `AlignedTextBuilder.Add`, which enforces the alignment contract (see *Alignment
Contract* in *SpeechTextEn System Design* and *NormalizationAlignment Design*), so a defective rule fails
loudly with an `ArgumentException` instead of producing an annotation that a backend would misapply. The
normalizer adds the A6 separating spaces itself when it emits a span, including the separator between two
adjacent word-producing replacements (`50%` gives `fifty` and `" percent"`, `5–10` gives `five`, `" to "` and
`ten`). When it emits a span it also decides, once, by the shape of the spoken text, whether the replacement is
**spelled** (single capital letters separated by single spaces: `F B I`), so the analyzer annotates it
`en.spell` rather than `en.norm`; a pronunciation directive adds a mark that replaces nothing.

**Unit collaboration.** `NumericExpressionRecognizer`, `AbbreviationResolver` and `AcronymRecognizer` are
pure lookups over their tables and a few neighbor tokens; they know nothing of builders or breaks.
`TextNormalizer` owns every decision about what the recognizer's spans cover when they are applied, whether a
period is consumed or kept and which sentence breaks change. `NumberVerbalizer` knows nothing of tokens: it
turns validated ASCII digits into words and is called only by the recognizer. `NormalizationAlignment` is the
passive model all of them share. The host override table is the one extra input: it is resolved to token
directives by the AnalysisSubsystem and enters the pass as rule 0, in front of every other rule, without
changing the contract.

**Numeric decomposition.** A numeric construct is never one wide span. Each token of it gets its own span, in
source order, or a currency symbol (or an apostrophe) is joined with its adjacent number token (spans S3 and
S4 of the alignment contract); a meridiem glued to its number is the one token that is split, into the span of
the number or time words and the span of the spelled meridiem letters (S5). No numeric span contains
whitespace, so every numeric span survives the Speech library's chunking by construction. A unit word or a
magnitude word is attached to the token that holds it (`$5 million` has the spans `$5` for `five` and
`million` for `million dollars`).

**Concurrency.** Every unit is immutable after construction or stateless. The two default lexicons are
loaded under `Lazy<T>` with `ExecutionAndPublication`, so concurrent first use loads each exactly once; a
failure to load means a defective build (a malformed embedded table), is deterministic, and is therefore
cached rather than retried. The numeric units are `static` classes over read-only tables and keep no
mutable static state. The per-request state of a pass lives in a `ref struct` on the caller's stack, so one
`TextNormalizer` serves any number of concurrent requests.

**Cancellation and linearity.** The normalizer checks the token at the start and every 256 visited tokens (a
token consumed by a numeric expression still counts as visited). Each rule looks at a constant number of
neighboring tokens and each character of a token is visited a constant number of times; the numeric scanners
refuse a token longer than 128 characters before scanning it, cap the digit groups and digits at the documented
limits, and use no regular expression, so there is no backtracking to exploit. The pass is therefore linear in
the text length.

**Culture invariance.** The numeric units use `char` arithmetic only: no culture-sensitive parsing,
formatting, casing or comparison, so the result never depends on the current culture. Only the ASCII digits
`0-9` are ever read as digits.

**Resources.** The two lexicons are embedded resources of the library assembly, `abbreviations.tsv` and
`acronyms.tsv` in the `Resources` folder, written from scratch by DEMA Consulting under the package's MIT
License. They are plain tab-separated text so a non-programmer can review them; their formats are documented
in *AbbreviationResolver Design* and *AcronymRecognizer Design*. The numeric tables (units, currencies,
month and weekday names, cue words) are read-only arrays in the code of the recognizer and are documented in
*NumericExpressionRecognizer Design*; the month vocabulary is shared with the abbreviation lexicon and the two
are kept consistent by review.
