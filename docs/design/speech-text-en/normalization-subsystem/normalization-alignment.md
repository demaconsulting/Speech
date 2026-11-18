### NormalizationAlignment

**Purpose**: Own the alignment model of the normalization: the values that describe a changed span, a token,
a clitic role and a pronunciation mark, the builder that refuses any decision that would break the alignment
contract, and the immutable result that reports the spans, the sentence-break edits and the clitic roles of
one pass. The two public enumerations of the subsystem, `NormalizationKind` and `CliticKind`, are declared in
this unit's file, `NormalizationAlignment.cs`, and are documented here.

**Data Model**:

`NormalizationKind` (public) says what produced one `en.norm` replacement. The numeric values are part of the
contract and are not reused:

- `Abbreviation` (1): a lexicon abbreviation (`Dr.`, `etc.`), a listed slash form (`w/o`, `N/A`) or a
  single-letter initial (`J.`)
- `Initialism` (2): an acronym or initialism spoken letter by letter: listed (`FBI`), dotted (`U.S.`), ampersand
  (`AT&T`) or recognized by rule
- `Symbol` (3): a non-numeric symbol or mark spoken as words (`&`, `@`, the copyright sign), including an `&` or
  `/` inside a word
- `Apostrophe` (4): a typographic apostrophe replaced by U+0027 inside a clitic or an elision
- `Dropped` (5): an invisible format character or a bullet spoken as nothing; the replacement is empty
- `Number` (6): a number spoken as words (cardinal, ordinal, decimal, fraction, year, decade, percent sign, `#`
  number, range, digit group); the spoken text has no digits
- `Currency` (7): a currency amount (`$5.50`), or the unit word of a currency code or of a magnitude after a
  symbol (`USD`, `million`)
- `DateTime` (8): a clock time, a meridiem, a numeric date, or a day or year next to a month name
- `Unit` (9): a unit of measure after a number (`kg`, `ft.`, `°F`), spoken in full, singular or plural by the
  number
- `Override` (10): a host pronunciation override replaced the word: a `Say` entry (words) or a `SpellOut` entry
  (letters); the replacement is exactly what the dictionary entry says

`CliticKind` (public) is the grammatical role of a clitic token, reported only by the detailed analysis (no
annotation is emitted for it): `None` (0, not a clitic, or classification was not requested), `Possessive` (1),
`Is` (2), `Has` (3), `Would` (4), `Had` (5), `Will` (6), `Have` (7), `Are` (8), `Am` (9), `Not` (10) and `Us`
(11).

The internal values are read-only record structs:

- `NormalizationToken(Start, Length, StartsSentence)`: one token as the normalizer sees it; `End` is the
  exclusive end and `In(text)` returns the token's characters as a span without copying. `StartsSentence`
  is the tokenizer's opinion, before any reconciliation
- `NormalizedSpan(Start, Length, Spoken, Kind, Spelled)`: one replacement; `Spoken` is the exact spoken text,
  already padded where A6 requires, empty only for `Dropped`; `Spelled` (false by default) says the spoken
  text is a letter sequence to be spoken by letter name, so the analyzer annotates the span `en.spell` instead of
  `en.norm`
- `PronunciationMark(Start, Length, Value)`: a `scheme:phonemes` value attached to one token's exact source
  characters; the characters are not replaced
- `TokenDirective(TokenIndex, Kind, Value)` with `TokenDirectiveKind` (`Skip`, `Replace`, `Spell`, `Pronounce`,
  `Reading`): one decision of the host's override dictionary about one token, passed in by the analyzer
- `SpelledGrammar.IsLetterSequence(value)`: the shared test for `[ ]?[A-Z]( [A-Z])*[ ]?` (single ASCII capitals
  separated by single spaces, at most one padding space per side), used by the builder and by the typed
  annotation readers so the two cannot drift apart
- `CliticMark(Start, Length, Kind)`: the role of one clitic token, never `None`

`AlignedTextBuilder` is a sealed, single-threaded class used for exactly one pass; it holds the source text, the
accepted spans, pronunciation marks and clitic marks, the requested forced and suppressed break offsets, the end
of the last accepted span or mark, and the counters (ambiguity, numeric fallback, overrides applied and
ignored). `AlignedText` is a sealed immutable class with `Source`, `Spans` (changed spans only, ascending and
disjoint; the span list is what the unit exposes, a consumer rebuilds the spoken text by applying the spans to
the source), `Pronunciations` (ascending, disjoint from each other and from every span), `ForcedBreaks` and
`SuppressedBreaks` (ascending, unique token-start offsets), `Clitics` (ascending; empty unless classification
was requested), `AmbiguousCount`, `OverridesApplied`, `OverridesIgnored` and `FallbackCount` (the number of
numeric-looking expressions left as written or read digit by digit because of a limit or an unsupported numeric
shape).

**Alignment invariants and where each is enforced** (the normative statement is in *SpeechTextEn System
Design*, *Alignment Contract*):

- **A1** spans ascending, disjoint, in bounds; **A1'** spans and pronunciation marks form one ascending,
  disjoint sequence: `Add` and `AddPronunciation` throw `ArgumentException` for an empty, out-of-bounds,
  out-of-order or overlapping span or mark; both compare against one "end of the last covered text" value
  (bounds are checked without computing `Start + Length`)
- **A2** non-empty, non-whitespace edges, no placeholder, no cut surrogate pair, non-null replacement: `Add`;
  `AddPronunciation` applies the same edge, placeholder and surrogate checks to a mark and also rejects an
  empty value or one that contains a placeholder
- **A3** the spans and the untouched text between them tile the source: by construction, applying every span as
  its own source text is the identity
- **A4** nothing outside a span changes: by construction, only `Spans` carry replacements and no unit re-cases
  or re-punctuates text
- **A5** chunk-safe spans (A5'): no whitespace, never `;`, `,` or `:` only between two ASCII digits, shapes S1
  to S5: `Add` rejects whitespace edges and placeholders; the shapes are the design of `TextNormalizer` and
  `NumericExpressionRecognizer`, verified by the subsystem property tests against the real chunker
- **A6** separating space where a letter or digit touches a letter-edged replacement, or the previous span
  directly touches the replacement and its spoken text ends with a letter: `TextNormalizer` when it emits a
  span (using `TouchesPreviousSpokenLetter` for adjacency); never for `Dropped` or `Apostrophe`
- **A7** no silent drop; the characters of a recognized numeric expression are consumed into its spoken words:
  by construction (text outside spans is copied verbatim; `Add` accepts an empty replacement only for
  `Dropped`); the dropped character set belongs to `TextNormalizer`, the consumed numeric characters to
  `NumericExpressionRecognizer`
- **A8** break edits on token starts, ascending and unique; no break strictly inside a span (A8b):
  `ForceBreak` and `SuppressBreak` reject an offset outside the text after its first character, on whitespace or
  on a placeholder; `Build` sorts and de-duplicates; `ReconcileBreaks` merges without duplicates;
  `TextNormalizer` suppresses a tokenizer break inside a numeric span
- **A9** deterministic and thread-safe: `AlignedText` and the values are immutable; a builder is never shared; no
  unit keeps per-request state
- **A10** empty replacement only for `Dropped`, no placeholder, no stray edge space; numeric kinds have no ASCII
  digit and only letters, spaces and an apostrophe: `Add` (empty exactly when `Dropped`, no placeholder); the A6
  padding by `TextNormalizer`; the numeric vocabulary by `NumberVerbalizer`, verified by the closed-vocabulary
  property test
- **A11** a spelled span's value is a letter sequence: `Add` throws `ArgumentException` for a span marked
  `Spelled` whose value is not a letter sequence (`SpelledGrammar`); a span not marked spelled may hold any text
- **A12** pronunciation marks are ascending, disjoint from each other and from every span, each one token with
  no whitespace edge, placeholder or cut surrogate pair, with a non-empty value: `AddPronunciation` (see A1' and
  A2)

**Key Methods**:

- **AlignedTextBuilder(source)**: stores the text; a null source throws `ArgumentNullException`
- **Add(span)**: validates the span against A1, A2, A10 and A11 and appends it; an invalid span throws
  `ArgumentException`, always a defect of the calling rule, never of the input text
- **AddPronunciation(mark)**: validates a pronunciation mark against A1', A2 and A12 and appends it; an
  invalid mark throws `ArgumentException`
- **ForceBreak(offset)** and **SuppressBreak(offset)**: record a break edit before the token starting at
  `offset`; an offset outside `[1, length - 1]`, on whitespace or on the placeholder character throws
  `ArgumentOutOfRangeException`. Requests may come in any order because a later rule can name an earlier token
- **MarkClitic(mark)**: records a clitic role; a non-ascending, empty, out-of-bounds or `None` mark throws
  `ArgumentException`
- **CountAmbiguous()**, **CountFallback()**, **CountOverrideApplied()**, **CountOverrideIgnored()**: add one to
  the matching counter (an override is ignored when it matched a token an earlier multi-token match had
  consumed, or was defective); all are reported in the result and are zero by default
- **TouchesPreviousSpokenLetter(start)**: true when the last accepted span ends exactly at `start` and its
  spoken text ends with a letter (false for a `Dropped` span, a space-ended or apostrophe-ended value, and any
  gap); constant time. It lets the normalizer separate two touching replacements with exactly one space
- **Build()**: returns the `AlignedText`, with the break requests sorted and de-duplicated
- **ReconcileBreaks(tokenizerBreaks)**: merges the ascending tokenizer breaks with the forced breaks and
  removes duplicates and every suppressed offset, returning an ascending, unique list; a null list throws
  `ArgumentNullException`

**Error Handling**: every violation of the contract by a calling rule is an exception at the point of the
defect (`ArgumentException`, `ArgumentOutOfRangeException` or `ArgumentNullException`), never a silently
corrected result, so a defect cannot reach a backend as a misaligned annotation. None of these is thrown for
any input text when the rules are correct, which the subsystem property tests check with random hostile
text. The builder does not catch anything.

**Dependencies**: the Speech library's `TextAnalysisConventions` (the placeholder character). No other
unit.

**Callers**: `TextNormalizer` (the builder), `EnglishTextAnalyzer` (the result, `ReconcileBreaks` and the
public enumerations through `AnalyzedToken` and `AnalyzedNormalization`), and the tests.
