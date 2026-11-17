### NormalizationAlignment

**Purpose**: Own the alignment model of the normalization: the values that describe a changed span, a token,
a clitic role and a partition piece, the builder that refuses any decision that would break the alignment
contract, and the immutable result that reports the spans, the sentence-break edits and the clitic roles of
one pass. The two public enumerations of the subsystem, `NormalizationKind` and `CliticKind`, are declared in
this unit's file, `NormalizationAlignment.cs`, and are documented here.

**Data Model**:

`NormalizationKind` (public) says what produced one `en.norm` replacement:

| Member | Value |
| --- | --- |
| `Abbreviation` | 1 |
| `Initialism` | 2 |
| `Symbol` | 3 |
| `Apostrophe` | 4 |
| `Dropped` | 5 |
| `Number` | 6 |
| `Currency` | 7 |
| `DateTime` | 8 |
| `Unit` | 9 |
| `Override` | 10 |

**Member details** (Produced by):

- `Abbreviation`: A lexicon abbreviation (`Dr.`, `etc.`), a listed slash form (`w/o`, `N/A`) or a single-letter initial
  (`J.`)
- `Initialism`: An acronym or initialism spoken letter by letter: listed (`FBI`), dotted (`U.S.`), ampersand (`AT&T`) or
  recognized by rule
- `Symbol`: A non-numeric symbol or mark spoken as words (`&`, `@`, the copyright sign), including an `&` or `/` inside
  a word
- `Apostrophe`: A typographic apostrophe replaced by U+0027 inside a clitic or an elision
- `Dropped`: An invisible format character or a bullet spoken as nothing; the replacement is empty
- `Number`: A number spoken as words: a cardinal, ordinal, decimal, fraction, year, decade, percent sign, `#` number,
  range, or a group of digits read one by one; the spoken text has no digits
- `Currency`: A currency amount (`$5.50`), or the unit word of a currency code or of a magnitude after a symbol (`USD`,
  `million`)
- `DateTime`: A clock time, a meridiem, a numeric date, or a day or year next to a month name
- `Unit`: A unit of measure after a number (`kg`, `ft.`, `°F`), spoken in full, singular or plural by the number
- `Override`: A host pronunciation override replaced the word: a `Say` entry (words) or a `SpellOut` entry (letters);
  the replacement is exactly what the dictionary entry says

The four numeric members were added by the numeric normalization increment and `Override` by the pronunciation
override increment; the earlier members keep their
names and values, so each addition is backward compatible. `CliticKind` (public) is the grammatical role of a
clitic token, recorded for later increments and reported
only by the detailed analysis (no annotation is emitted for it): `None` (0, not a clitic, or classification
was not requested), `Possessive` (1), `Is` (2), `Has` (3), `Would` (4), `Had` (5), `Will` (6), `Have` (7),
`Are` (8), `Am` (9), `Not` (10) and `Us` (11). The numeric values are part of the contract and are not
reused.

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
- `TokenDirective(TokenIndex, Kind, Value)` with `TokenDirectiveKind` (`Skip`, `Replace`, `Spell`, `Pronounce`):
  one decision of the host's override dictionary about one token, passed in by the analyzer
- `SpelledGrammar.IsLetterSequence(value)`: the shared test for `[ ]?[A-Z]( [A-Z])*[ ]?` (single ASCII capitals
  separated by single spaces, at most one padding space per side), used by the builder and by the typed
  annotation readers so the two cannot drift apart
- `CliticMark(Start, Length, Kind)`: the role of one clitic token, never `None`
- `AlignedSegment(Start, Length, Spoken, Kind)`: one piece of the full partition; `Kind` is null for an
  untouched gap, whose `Spoken` is the source slice

`AlignedTextBuilder` is a sealed class holding the source text, the accepted spans and pronunciation marks, the
requested forced and suppressed break offsets, the accepted clitic marks, the end of the last accepted span, mark and
clitic mark, and the ambiguity counter, the numeric fallback counter and the two override counters (applied and
ignored). It is single-threaded and used for exactly one pass. `AlignedText` is a sealed immutable class with `Source`,
`Spans` (changed spans only, ascending and disjoint), `Pronunciations` (the marks, ascending, disjoint from each other
and from every span), `ForcedBreaks` and `SuppressedBreaks` (ascending, unique token-start offsets), `Clitics`
(ascending; empty unless classification was requested), `AmbiguousCount`, `OverridesApplied` and `OverridesIgnored` and
`FallbackCount` (the number of numeric-looking expressions left as written or read digit by digit because of a
documented limit or an unsupported numeric shape); the fallback count, the marks and the two override counts are
optional trailing constructor parameters that default to 0 or none.

**Alignment invariants and where each is enforced** (the normative statement is in *SpeechTextEn System
Design*, *Alignment Contract*):

- **A1**
  - Invariant: Spans ascending, disjoint, in bounds; **A1'** spans and pronunciation marks form one ascending, disjoint
    sequence
  - Enforced by: `AlignedTextBuilder.Add` and `AddPronunciation` throw `ArgumentException` for an empty, out-of-bounds,
    out-of-order or overlapping span or mark; both compare against one "end of the last covered text" value, so a span
    cannot overlap an earlier mark and a mark cannot overlap an earlier span (bounds are checked without computing
    `Start + Length`)
- **A2**
  - Invariant: Non-empty, non-whitespace edges, no placeholder, no cut surrogate pair, non-null replacement
  - Enforced by: `AlignedTextBuilder.Add`; `AddPronunciation` applies the same edge, placeholder and surrogate checks to
    a mark and also rejects an empty value or one that contains a placeholder
- **A3**
  - Invariant: Segments tile the text; identity segments equal the source
  - Enforced by: By construction in `Segments()`; applying every span as its own source text is the identity
- **A4**
  - Invariant: Nothing outside a span changes
  - Enforced by: By construction: only `Spans` carry replacements and no unit re-cases or re-punctuates text
- **A5**
  - Invariant: Chunk-safe spans (A5'): no whitespace, never `;`, `,` or `:` only between two ASCII digits; shapes S1 to
    S5
  - Enforced by: `AlignedTextBuilder.Add` rejects whitespace edges and placeholders; the shape (one token, a token and
    its adjacent period token, a currency symbol and its adjacent number token, an apostrophe and its adjacent decade
    token, one of the two halves of a glued meridiem token, or one character inside a token) is the design of
    `TextNormalizer` and `NumericExpressionRecognizer`, verified by the subsystem property tests against the real
    chunker
- **A6**
  - Invariant: Separating space where a letter or digit touches a letter-edged replacement, or the previous span
    directly touches the replacement and its spoken text ends with a letter
  - Enforced by: `TextNormalizer` when it emits a span (using `AlignedTextBuilder.TouchesPreviousSpokenLetter` for
    adjacency); never for `Dropped` or `Apostrophe`
- **A7**
  - Invariant: No silent drop; the characters of a recognized numeric expression are consumed into its spoken words
  - Enforced by: By construction (text outside spans is copied verbatim; `Add` accepts an empty replacement only for
    `Dropped`); the dropped character set belongs to `TextNormalizer`, the consumed numeric characters to
    `NumericExpressionRecognizer`
- **A8**
  - Invariant: Break edits on token starts, ascending and unique; no break strictly inside a span (A8b)
  - Enforced by: `ForceBreak` and `SuppressBreak` reject an offset outside the text after its first character, on
    whitespace or on a placeholder; `Build` sorts and de-duplicates; `ReconcileBreaks` merges without duplicates;
    `TextNormalizer` suppresses a tokenizer break inside a numeric span
- **A9**
  - Invariant: Deterministic and thread-safe
  - Enforced by: `AlignedText` and the values are immutable; a builder is never shared; no unit keeps per-request state
- **A10**
  - Invariant: Empty replacement only for `Dropped`, no placeholder, no stray edge space; numeric kinds have no ASCII
    digit and only letters, spaces and an apostrophe
  - Enforced by: `AlignedTextBuilder.Add` (empty exactly when `Dropped`, no placeholder); the A6 padding by
    `TextNormalizer`; the numeric vocabulary by `NumberVerbalizer`, verified by the closed-vocabulary property test
- **A11**
  - Invariant: A spelled span's value is a letter sequence (`[A-Z]( [A-Z])*` with at most one padding space per side)
  - Enforced by: `AlignedTextBuilder.Add` throws `ArgumentException` for a span marked `Spelled` whose value is not a
    letter sequence (`SpelledGrammar`); a span not marked spelled may hold any text
- **A12**
  - Invariant: Pronunciation marks are ascending, disjoint from each other and from every span, each one token with no
    whitespace edge, placeholder or cut surrogate pair, with a non-empty value
  - Enforced by: `AlignedTextBuilder.AddPronunciation` (see A1' and A2)

**Key Methods**:

- **AlignedTextBuilder(source)**: stores the text; a null source throws `ArgumentNullException`
- **Add(span)**: validates the span against A1, A2, A10 and A11 and appends it; an invalid span throws
  `ArgumentException`, and a span that is whitespace-edged, covers a placeholder or cuts a surrogate pair is
  always a defect of the calling rule, never of the input text
- **AddPronunciation(mark)**: validates a pronunciation mark against A1', A2 and A12 and appends it (an
  empty, out-of-bounds, overflowing, out-of-order, overlapping, whitespace-edged, placeholder-covering or
  pair-cutting mark, or one with an empty or placeholder-bearing value, throws `ArgumentException`)
- **ForceBreak(offset)** and **SuppressBreak(offset)**: record a break edit before the token starting at
  `offset`; an offset outside `[1, length - 1]`, on whitespace or on the placeholder character throws
  `ArgumentOutOfRangeException`. Requests may come in any order because a later rule can name an earlier
  token
- **MarkClitic(mark)**: records a clitic role; a non-ascending, empty, out-of-bounds or `None` mark throws
  `ArgumentException`
- **CountAmbiguous()**: adds one to the ambiguity counter
- **CountFallback()**: adds one to the numeric fallback counter
- **CountOverrideApplied()** and **CountOverrideIgnored()**: add one to the counter of user overrides that
  took effect on a token, or that matched a token an earlier multi-token match had consumed (or were
  defective); both are reported in the result and are zero without overrides
- **TouchesPreviousSpokenLetter(start)**: true when the last accepted span ends exactly at `start` and its
  spoken text ends with a letter (false for a `Dropped` span, a space-ended or apostrophe-ended value, and any
  gap); the builder keeps one boolean, updated by `Add`, so the query is constant time. It lets the normalizer
  separate two touching replacements with exactly one space
- **Build()**: returns the `AlignedText`, with the break requests sorted and de-duplicated
- **Segments()**: enumerates the spans and the untouched gaps between them, in order, contiguous and
  covering the whole text; an empty text yields nothing and a text without spans yields one gap
- **ApplySpokenForms()**: the source with each span replaced by its `Spoken` text, in one linear pass; the
  reference implementation of the consumer algorithm for tests and for the detailed analysis
- **ReconcileBreaks(tokenizerBreaks)**: merges the ascending tokenizer breaks with the forced breaks and
  removes duplicates and every suppressed offset (binary search over the suppressed list), returning an
  ascending, unique list; a null list throws `ArgumentNullException`

**Error Handling**: every violation of the contract by a calling rule is an exception at the point of the
defect (`ArgumentException`, `ArgumentOutOfRangeException` or `ArgumentNullException`), never a silently
corrected result, so a defect cannot reach a backend as a misaligned annotation. None of these is thrown for
any input text when the rules are correct, which the subsystem property tests check with random hostile
text. The builder does not catch anything.

**Dependencies**: the Speech library's `TextAnalysisConventions` (the placeholder character). No other
unit.

**Callers**: `TextNormalizer` (the builder), `EnglishTextAnalyzer` (the result, `ReconcileBreaks` and the
public enumerations through `AnalyzedToken` and `AnalyzedNormalization`), and the tests.
