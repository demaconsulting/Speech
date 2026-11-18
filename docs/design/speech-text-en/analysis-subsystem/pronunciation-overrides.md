### PronunciationOverrides

**Purpose**: Hold the host's pronunciation overrides as an immutable, validated dictionary and tell the
analyzer which tokens of a text an entry applies to. An override says how one whole word is spoken
(as written, as other words, letter by letter, or with a phoneme string) or which heteronym reading it has, and it beats
every built-in rule for the spoken form (never for where a sentence ends).
The unit owns the entry model, the grammar of keys and payloads, the tab-separated file format, the bounded
reader, the case and whole-word matching rules and the lookup tables. It never rewrites text and never decides
what a span covers: it only produces token-indexed directives for the `TextNormalizer`. The normative rules for
users are in *SpeechTextEn System Design*, *Pronunciation Overrides*; this document is the design of the unit
that implements them.

**Data Model**:

`PronunciationOverrideEntry` (public) is a sealed record `(Word, CaseSensitive, Action, Payload)`: a plain
immutable value that is validated only when it is added to a dictionary. Five static factories build the five
kinds so a caller never writes a payload by hand: `Skip(word)`, `Say(word, spokenText)`, `SpellOut(word)`,
`Pronounce(word, scheme, phonemes)` (which writes the payload as `scheme:phonemes` with the scheme's
lower-case wire name) and `Reading(word, readingId)` (the payload is the reading id). Every factory takes
`caseSensitive = false` last. `OverrideAction` (public) is
`Skip` (1), `Say` (2), `SpellOut` (3), `Pronunciation` (4) and `Reading` (5); a value outside 1 to 5 is undefined and is
rejected. `PronunciationScheme` (public) is `Ipa` (1, wire name `ipa`) and `Arpabet` (2, wire
name `arpabet`); the set is closed.

`PronunciationOverrides` (public) is a sealed, immutable class. It keeps a private array copy of the validated entries
(exposed only as a read-only view, `Entries`, in the order supplied), two sorted row arrays (the case-sensitive rows
ordered by ordinal comparison, the case-insensitive rows by `OrdinalIgnoreCase`), and the shortest and longest key
length. A row is a canonical key, the directive kind and the directive value. Every key that contains an apostrophe adds
a second row with all apostrophes written as U+2019, so both spellings match. A canonical key holds only plain
apostrophes and an alias only typographic ones, so within one row array an alias never equals a canonical key, and two
aliases are equal only when their canonical keys are, which the duplicate rule already rejected; the arrays therefore
hold unique keys, the sort is total and the binary search is deterministic. The case-sensitive array is searched first,
so a case-sensitive entry (by its exact spelling or its alias) beats a case-insensitive one for the same token. `Empty`
is one shared instance; `Count` is the number of entries. The six public constants are the limits: `MaxEntries`
(10,000), `MaxWordLength` (64 UTF-16 code units), `MaxPayloadLength` (256), `MaxSpellOutLetters` (32),
`MaxTsvLineLength` (1,024) and `MaxTsvLines` (50,000).

`OverrideSyntax` (internal, static) holds the grammars so that the dictionary builders and the typed annotation
readers of `TextEnAnnotations` share one definition: the key grammar, the spell-out key rule, the `say` payload
grammar, the `scheme:phonemes` grammar (with the `ParsePronunciation` splitter) and the reading validation
(`ValidateReading`, which uses the reading id grammar and the inventory of the HeteronymSubsystem). Every validator
returns a fixed-text reason, never the offending text.

**Entry rules** (the same for `Create` and `FromTsv`):

- **Word**: 1 to 64 UTF-16 code units of well-formed text; the first scalar is a letter and the last is a letter or a
  decimal digit; only letters, combining marks, decimal digits and the characters `'`, `’` (U+2019), `-`, `.`, `&`, `/`
  and `_`; the plain and the typographic apostrophe may not be mixed in one word. No whitespace, control, format,
  surrogate or private-use characters and no clause punctuation (`,` `;` `:`) are possible, so a key can never contain a
  character that the core chunker cuts at
- **Word, normalization**: Keys are compared as written: no Unicode normalization form is applied, so write keys in
  normalization form C. A key that ends in a combining mark (typical of text in form D) is rejected by the last-scalar
  rule
- **Action**: One of the five defined values
- **Payload**: `Skip` and `SpellOut`: none. `Say`: 1 to 256 UTF-16 code units of letters, combining marks, `'`, `-` and
  single spaces between words, at least one letter, no digit and no punctuation. `Pronunciation`: `ipa:` followed by 1
  to 128 UTF-16 code units without whitespace, control, format, private-use or unassigned characters, or `arpabet:`
  followed by 1 to 32 phones separated by single spaces, each one to three capital ASCII letters and an optional stress
  digit 0 to 2; the whole value at most 256 code units. `Reading`: a reading id that satisfies the reading id grammar
  (two or three dot-separated segments of `a` to `z`, `0` to `9` and `-`, at most 64 characters)
- **Reading entry**: the validation runs in this order and gives the first fixed reason that applies: the payload is
  missing (`a reading id is required for this action`); the id is ill-formed (`the reading id is not well-formed`); the
  id is not in the reading inventory (`the reading id is not in the reading inventory`); the id is not active in it
  (`the reading id is not active in the reading inventory`); the word, folded with ASCII case folding, is not a form of
  the inventory that has this reading among its candidates (`the word is not a form of this reading`). The inventory
  is read only after the id is well-formed, so a dictionary without `Reading` entries never touches it. A word that
  differs from a form only by a non-ASCII look-alike letter (or the Kelvin sign) is not a form. The reasons never echo
  the word or the id
- **Spell-out word**: 1 to 32 ASCII letters only (letter names exist only for A to Z)
- **Duplicates**: The same canonical key (the typographic apostrophe folded to the plain one) with the same case flag,
  the keys compared with `OrdinalIgnoreCase` for an insensitive entry, is a duplicate and an error; one sensitive and
  one insensitive entry for the same word, and sensitive entries that differ only in case, are allowed

**Key Methods**:

- **Create(entries)**: enumerates the entries once through an internal accumulator that validates each entry,
  detects duplicates and enforces `MaxEntries`; the first failure throws `ArgumentException` whose message is
  exactly `Entry {zero-based index}: {reason}.` with a fixed reason and never the word or the payload. A null
  list or element throws `ArgumentNullException`. It returns `Empty` when there are no entries
- **FromTsv(reader)**: reads the format `word TAB case TAB action [TAB payload]` through a bounded reader, one character
  at a time, into a buffer of `MaxTsvLineLength` characters. It stops with an error at the first character that does not
  fit, so an endless input without line breaks costs at most 1,025 characters, and at line `MaxTsvLines + 1`, so an
  endless input of short lines is bounded too. LF and CRLF end a line; a lone CR is an error; a U+FEFF as the very first
  character is skipped. An empty line and a line whose first character is `#` are ignored; a line of only whitespace is
  an error; there is no header row. Every character of every line, comment lines included, is checked: a control
  character (`char.IsControl`) other than the tab separator is an error. A data row has three or four columns (a fourth
  empty column is tolerated for `skip` and `spell`); no column may begin or end with whitespace; the `case` column is
  exactly `sensitive` or `insensitive` and the `action` column exactly `skip`, `say`, `spell`, `pron` or `reading` (any
  other word gives the reason `the action column must be 'skip', 'say', 'spell', 'pron' or 'reading'`). Each row is
  turned into an entry and given to the same accumulator as `Create`, so a row is rejected for exactly the reasons an
  entry is (a `reading` row is validated like a reading entry above). Failure throws `InvalidDataException` whose
  message is exactly `Line {one-based number}: {reason}.`; the reasons are fixed text and never echo the line. The
  reader is read to its end, neither disposed nor retained, and its own exceptions propagate unchanged
- **Resolve(text, tokens, cancellationToken)** (internal): walks every token once. It checks the cancellation token
  before any work and after every 256th visited token, counting every token whether it matches or not. A token is a
  candidate only when its length lies between the shortest and the longest key, its first scalar is a letter and the
  scalar before and after it (decoded as whole Unicode scalars, so a supplementary letter counts as one) is not a
  letter, a combining mark or a decimal digit. A candidate is looked up by binary search in the sensitive rows (ordinal
  comparison of spans) and, only when that finds nothing, in the insensitive rows (`OrdinalIgnoreCase` comparison of
  spans), which is the precedence rule. A hit appends one `TokenDirective(tokenIndex, kind, value)` to a list (the kinds
  are skip, replace, spell, pronounce and reading; the value is the spoken words, the `scheme:phonemes` text or the
  reading id, and null for skip and spell); the result is an array of directives in ascending token order, one per
  matching token, empty when nothing matches. A non-candidate costs no lookup and no allocation and a miss allocates
  nothing. The token ranges are validated without computing `Start + Length`. An optional per-token callback is a test
  seam that production never passes

**Matching rules** (design decisions and their reasons):

- **A key is compared with one whole token, never with a substring or several tokens.** An annotation over
  several tokens could be cut by the core chunker (its 200-character whitespace budget fallback) and silently
  lost, so v1 keeps the rule that no span contains whitespace; multi-word entries are rejected because a key
  cannot contain whitespace
- **Whole word.** The neighbor test makes the stem of a split word (the `Do` of `Do` + `n't`) never match on
  its own. The tokenizer is inconsistent by shape (it splits `NASA's` into `NASA` and `'s` but keeps
  `AT&T's`, `Wi-Fi's` and `Nginx-based` whole), so the rule is stated on the tokens it delivers; a key must be a
  word that survives tokenization as one token. A test checks that every key of the sample dictionary is a
  single token of the real tokenizer, so a tokenizer update cannot silently make documented keys inert
- **Case.** `OrdinalIgnoreCase` is culture-invariant: `i` and `I` match, the Turkish `İ` (U+0130) and `ı` (U+0131)
  match neither, the Kelvin sign does not fold to `k` and `ß` does not fold to `SS`; supplementary letters fold
  as the runtime does and the tests pin that on every target framework. The current culture is never consulted
- **Trailing period.** A word that ends with an abbreviation period (`Dr.`, `U.S.`) cannot be a key: the
  tokenizer keeps that period inside the token, and the built-in abbreviation rules own it. A token followed by
  a split-off period token (`Ph.D` and `.`) matches its key and leaves the period token to the normal rules
- **Tag placeholders** are replaced by spaces before tokenization, so they separate words and are never part of
  a key match or covered

**Error Handling**: `Create` and `FromTsv` throw only the documented exceptions with fixed-text reasons
(`ArgumentNullException`, `ArgumentException`, `InvalidDataException`; the reader's own exceptions pass
through). A defective embedded reading inventory (a broken build) would propagate its `InvalidDataException` from
the validation of a `Reading` entry. A dictionary that was built successfully never throws during a lookup, except
`OperationCanceledException` for a canceled token and `ArgumentNullException` or `ArgumentException` for a
malformed argument of the internal `Resolve` (a defect of the caller). No message ever contains a word,
payload, line or entry count, so a dictionary of names cannot leak through an exception or a log.

**Immutability and concurrency**: the constructor is private and takes an array that is owned by the new
instance; all fields are read-only; a built dictionary is safe to share across analyzers, requests and threads
without locks. The accumulator is local to one build call.

**Dependencies**: the NormalizationSubsystem's internal `NormalizationToken`, `TokenDirective` and
`TokenDirectiveKind` values (the directive hand-over), `OverrideSyntax`, the HeteronymSubsystem's `ReadingInventory`
and `ReadingIdSyntax` (the validation of a `Reading` entry), and the framework. No OTS item
and no tokenizer type is named by this unit.

**Callers**: `EnglishTextAnalyzer` (builds nothing, resolves the dictionary of its options for each request),
`TextEnAnnotations` (shares `OverrideSyntax` for the pronunciation reader), the host application or model
package (builds the dictionary with `Create` or `FromTsv`), and the tests.
