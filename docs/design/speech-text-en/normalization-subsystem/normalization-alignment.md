### NormalizationAlignment

**Purpose**: Own the alignment model of the normalization: the values that describe a changed span, a token,
a clitic role and a partition piece, the builder that refuses any decision that would break the alignment
contract, and the immutable result that reports the spans, the sentence-break edits and the clitic roles of
one pass. The two public enumerations of the subsystem, `NormalizationKind` and `CliticKind`, are declared in
this unit's file, `NormalizationAlignment.cs`, and are documented here.

**Data Model**:

`NormalizationKind` (public) says what produced one `en.norm` replacement:

| Member | Value | Produced by |
| --- | --- | --- |
| `Abbreviation` | 1 | A lexicon abbreviation (`Dr.`, `etc.`), a listed slash form (`w/o`, `N/A`) or a single-letter initial (`J.`) |
| `Initialism` | 2 | An acronym or initialism spoken letter by letter: listed (`FBI`), dotted (`U.S.`), ampersand (`AT&T`) or recognized by rule |
| `Symbol` | 3 | A non-numeric symbol or mark spoken as words (`&`, `@`, the copyright sign), including an `&` or `/` inside a word |
| `Apostrophe` | 4 | A typographic apostrophe replaced by U+0027 inside a clitic or an elision |
| `Dropped` | 5 | An invisible format character or a bullet spoken as nothing; the replacement is empty |
| `Number` | 6 | A number spoken as words: a cardinal, ordinal, decimal, fraction, year, decade, percent sign, `#` number, range, or a group of digits read one by one; the spoken text has no digits |
| `Currency` | 7 | A currency amount (`$5.50`), or the unit word of a currency code or of a magnitude after a symbol (`USD`, `million`) |
| `DateTime` | 8 | A clock time, a meridiem, a numeric date, or a day or year next to a month name |
| `Unit` | 9 | A unit of measure after a number (`kg`, `ft.`, `°F`), spoken in full, singular or plural by the number |

The four numeric members were added by the numeric normalization increment; the earlier members keep their
names and values, so the addition is backward compatible. `CliticKind` (public) is the grammatical role of a
clitic token, recorded for later increments and reported
only by the detailed analysis (no annotation is emitted for it): `None` (0, not a clitic, or classification
was not requested), `Possessive` (1), `Is` (2), `Has` (3), `Would` (4), `Had` (5), `Will` (6), `Have` (7),
`Are` (8), `Am` (9), `Not` (10) and `Us` (11). The numeric values are part of the contract and are not
reused.

The internal values are read-only record structs:

- `NormalizationToken(Start, Length, StartsSentence)`: one token as the normalizer sees it; `End` is the
  exclusive end and `In(text)` returns the token's characters as a span without copying. `StartsSentence`
  is the tokenizer's opinion, before any reconciliation
- `NormalizedSpan(Start, Length, Spoken, Kind)`: one replacement; `Spoken` is the exact spoken text, already
  padded where A6 requires, empty only for `Dropped`
- `CliticMark(Start, Length, Kind)`: the role of one clitic token, never `None`
- `AlignedSegment(Start, Length, Spoken, Kind)`: one piece of the full partition; `Kind` is null for an
  untouched gap, whose `Spoken` is the source slice

`AlignedTextBuilder` is a sealed class holding the source text, the accepted spans, the requested forced and
suppressed break offsets, the accepted clitic marks, the end of the last accepted span and clitic mark, and
the ambiguity counter and the numeric fallback counter. It is single-threaded and used for exactly one pass. `AlignedText` is a sealed
immutable class with `Source`, `Spans` (changed spans only, ascending and disjoint), `ForcedBreaks` and
`SuppressedBreaks` (ascending, unique token-start offsets), `Clitics` (ascending; empty unless
classification was requested), `AmbiguousCount` and `FallbackCount` (the number of numeric-looking
expressions left as written or read digit by digit because of a documented limit or an unsupported numeric
shape; it is the last constructor parameter and defaults to 0).

**Alignment invariants and where each is enforced** (the normative statement is in _SpeechTextEn System
Design_, _Alignment Contract_):

| # | Invariant | Enforced by |
| --- | --- | --- |
| A1 | Spans ascending, disjoint, in bounds | `AlignedTextBuilder.Add` throws `ArgumentException` for an empty, out-of-bounds, out-of-order or overlapping span |
| A2 | Non-empty, non-whitespace edges, no placeholder, no cut surrogate pair, non-null replacement | `AlignedTextBuilder.Add` |
| A3 | Segments tile the text; identity segments equal the source | By construction in `Segments()`; applying every span as its own source text is the identity |
| A4 | Nothing outside a span changes | By construction: only `Spans` carry replacements and no unit re-cases or re-punctuates text |
| A5 | Chunk-safe spans (A5'): no whitespace, never `;`, `,` or `:` only between two ASCII digits; shapes S1 to S4 | `AlignedTextBuilder.Add` rejects whitespace edges and placeholders; the shape (one token, a token and its adjacent period token, a currency symbol and its adjacent number token, an apostrophe and its adjacent decade token, or one character inside a token) is the design of `TextNormalizer` and `NumericExpressionRecognizer`, verified by the subsystem property tests against the real chunker |
| A6 | Separating space where a letter or digit touches a letter-edged replacement, or the previous span directly touches the replacement and its spoken text ends with a letter | `TextNormalizer` when it emits a span (using `AlignedTextBuilder.TouchesPreviousSpokenLetter` for adjacency); never for `Dropped` or `Apostrophe` |
| A7 | No silent drop; the characters of a recognized numeric expression are consumed into its spoken words | By construction (text outside spans is copied verbatim; `Add` accepts an empty replacement only for `Dropped`); the dropped character set belongs to `TextNormalizer`, the consumed numeric characters to `NumericExpressionRecognizer` |
| A8 | Break edits on token starts, ascending and unique; no break strictly inside a span (A8b) | `ForceBreak` and `SuppressBreak` reject an offset outside the text after its first character, on whitespace or on a placeholder; `Build` sorts and de-duplicates; `ReconcileBreaks` merges without duplicates; `TextNormalizer` suppresses a tokenizer break inside a numeric span |
| A9 | Deterministic and thread-safe | `AlignedText` and the values are immutable; a builder is never shared; no unit keeps per-request state |
| A10 | Empty replacement only for `Dropped`, no placeholder, no stray edge space; numeric kinds have no ASCII digit and only letters, spaces and an apostrophe | `AlignedTextBuilder.Add` (empty exactly when `Dropped`, no placeholder); the A6 padding by `TextNormalizer`; the numeric vocabulary by `NumberVerbalizer`, verified by the closed-vocabulary property test |

**Key Methods**:

- **AlignedTextBuilder(source)**: stores the text; a null source throws `ArgumentNullException`
- **Add(span)**: validates the span against A1, A2 and A10 and appends it; an invalid span throws
  `ArgumentException`, and a span that is whitespace-edged, covers a placeholder or cuts a surrogate pair is
  always a defect of the calling rule, never of the input text
- **ForceBreak(offset)** and **SuppressBreak(offset)**: record a break edit before the token starting at
  `offset`; an offset outside `[1, length - 1]`, on whitespace or on the placeholder character throws
  `ArgumentOutOfRangeException`. Requests may come in any order because a later rule can name an earlier
  token
- **MarkClitic(mark)**: records a clitic role; a non-ascending, empty, out-of-bounds or `None` mark throws
  `ArgumentException`
- **CountAmbiguous()**: adds one to the ambiguity counter
- **CountFallback()**: adds one to the numeric fallback counter
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
