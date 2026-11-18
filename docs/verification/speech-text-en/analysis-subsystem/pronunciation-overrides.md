### PronunciationOverrides Verification

#### Verification Approach

`PronunciationOverrides` (with `PronunciationOverrideEntry`, the `OverrideAction` and `PronunciationScheme`
enumerations and the internal `OverrideSyntax` grammars documented with it) is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/PronunciationOverridesTests.cs`. The tests drive
`Create` and `FromTsv` directly, and drive the internal `Resolve` over the tokens of the real tokenizer (through
the `NormalizationHarness` helper of the test project) or, where the tokenizer would hide the case under test,
over tokens that the test cuts itself. No fake is needed: the grammars, the tokenizer and the normalizer are real.
Each validation theory keeps one row per distinct failure reason and the boundary values.

Four kinds of evidence are used:

- **Sample and golden files**: `TestData/overrides-dictionary.tsv` is the sample dictionary. Its first seven
  entries are the sample of the user guide and the remaining entries feed the golden rows.
  `TestData/overrides-cases.tsv` is the golden file; each row names an input text and the spoken text,
  pronunciation marks, spelled spans and override counts that the real normalizer must produce with the sample
  dictionary.
- **Property test against a naive reference**: a fixed-seed test builds 20,000 random valid dictionaries and random
  texts with random token boundaries (including boundaries inside words and surrogate pairs) from a pool of
  colliding words, and requires the dictionary's lookup to equal a linear scan written only with the runtime's
  string comparison.
- **Environment evidence**: tests that make the Turkish and Azeri cultures current prove that matching never
  consults the culture. The test project multi-targets several .NET versions, so the supplementary-letter test
  compares the lookup with the ordinal ignore-case comparison of the runtime that executes it and thereby
  produces per-runtime evidence for case folding of supplementary-plane letters.
- **Instrumented inputs**: a counting `TextReader` proves that the tab-separated reader stops reading an endless
  input at its limit, and a failing reader and a tracking reader prove how reader exceptions and disposal are handled.

Only the syntax of a pronunciation is verified here. Whether a phoneme belongs to the inventory of a backend, and
how a backend speaks it, is out of scope: the backends own their inventories.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Files**: the sample dictionary and the golden file are copied next to the test assembly under `TestData`; no
  test needs network access
- **Isolation**: every test builds its own dictionary; the culture tests restore the current culture; the random
  test uses a fixed seed so a failure is reproducible

#### Acceptance Criteria

- **Entry validation**: every invalid word, undefined action, payload that does not fit its action, malformed say or
  pronunciation payload, non-spellable spell-out word, duplicate (apostrophe styles unified) and invalid reading entry
  is
  rejected with an `ArgumentException` that holds the zero-based entry index and a fixed reason and never the word, the
  payload or the reading id; a sensitive and an insensitive entry for one word are accepted; every limit is accepted
  exactly at its value; a null list, element or factory argument throws `ArgumentNullException`
- **Tab-separated loading**: the file gives exactly the dictionary `Create` gives, accepts both line ends, a byte-order
  mark, a missing final line end and an empty fourth column, rejects every malformed row with the one-based line number
  and no content, stops an endless input at its limit and accepts the line and line-count limits exactly; reader
  exceptions propagate and the reader is neither disposed nor retained
- **Immutability**: a change to the supplied list has no effect, the entries are a read-only view in the order supplied
  and concurrent lookups agree
- **Matching**: each action maps to its directive with at most one directive per token in ascending order and none for a
  token no entry matches; only a whole token matches (never a piece next to a letter, a mark or a digit), with clitic
  stems, hyphen compounds, both apostrophe styles and words next to a placeholder as documented; case-sensitive matching
  is exact, insensitive matching is the ordinal ignore-case whatever the current culture, a sensitive entry beats an
  insensitive one and no normalization form is applied; the lookup agrees with the naive reference for 20,000 random
  cases; every row of the golden file normalizes as documented
- **Contract**: cancellation throws for a token canceled beforehand and when requested while a long text is looked up;
  a null text, an empty token and a token outside the text throw without overflowing arithmetic

#### Test Scenarios

##### Entry validation

**Tests**: `PronunciationOverrides_Create_InvalidWord_ThrowsWithEntryIndexAndNoContent`,
`PronunciationOverrides_Create_WordLengthBoundary_RejectsOnlyTooLong`,
`PronunciationOverrides_Create_ValidWord_IsAccepted`,
`PronunciationOverrides_Create_UndefinedAction_Throws`,
`PronunciationOverrides_Create_ActionPayloadMismatch_Throws`,
`PronunciationOverrides_Create_SpellOutNonAsciiOrTooLong_Throws`,
`PronunciationOverrides_Create_NullEntriesOrElement_Throws`

The invalid-word theory places the bad entry second, after a valid one, and requires an `ArgumentException` that names
`Entry 1:` and does not contain the word; its rows cover an empty word, a leading or trailing apostrophe, hyphen or
period, clause punctuation, control, format and unassigned characters, a lone surrogate, a dotted initialism ending in a
period, a combining mark in first position and mixed apostrophe styles. The length test accepts a word of exactly 64
code units and rejects one more. The valid-word theory accepts letters of several scripts, supplementary-plane letters,
marks inside a word, a trailing digit and the characters `' ’ - . & / _`. The undefined-action theory rejects values
outside 1 to 5. The mismatch test rejects a payload on a skip or spell entry and no payload on a say or pronunciation
entry, using a sentinel payload that must not appear in the message. The spell-out theory rejects non-ASCII, digit and
over-long words and accepts exactly 32 letters. The null test covers a null list, a null element, null arguments of
every
factory (including the reading factory), an undefined scheme value and a null word in the entry constructor.

##### Payload grammar and limits

**Tests**: `PronunciationOverrides_Create_SayPayload_GrammarEnforced`,
`PronunciationOverrides_Create_SayPayloadLength_BoundaryIsInclusive`,
`PronunciationOverrides_Create_PronunciationPayload_SchemeAndSyntaxEnforced`,
`PronunciationOverrides_Create_PhonemeLengthBounds_AreInclusive`,
`PronunciationOverrides_Create_EntryCountBoundary_IsInclusive`

The say theory accepts words with apostrophes, hyphens, accented letters and single inner spaces, and rejects an empty,
padded, double-spaced, digit, clause-punctuation or control-character payload. The length test accepts 256 code
units and
rejects 257. The pronunciation theory accepts `ipa:` and `arpabet:` payloads and rejects a missing, upper-case or
unknown
scheme, whitespace or control characters in IPA, and ARPAbet phones that are lower case, double-spaced, longer than
three
letters or carry a stress digit above 2. The bounds test accepts 128 IPA code units and 32 ARPAbet phones and
rejects one
more of each. The entry-count test accepts exactly 10,000 entries through `Create` and through `FromTsv` and rejects the
10,001st, naming the entry or, for a file, the line; an endless entry sequence is cut off rather than enumerated to its
end.

##### Duplicates and reading entries

**Tests**: `PronunciationOverrides_Create_DuplicateKeys_Throws`,
`PronunciationOverrides_Create_SensitiveAndInsensitiveSameWord_Allowed`,
`PronunciationOverrides_AliasCollisions_AreRejectedByCreateAndFromTsv`,
`PronunciationOverrides_Create_ReadingEntryInvalid_ThrowsFixedReason`,
`PronunciationOverrides_Create_ReadingEntryWordCase_IsFoldedAsciiOnly`

The duplicate theory requires the later entry to be rejected (`Entry 1:` and the word "duplicate") for equal sensitive
keys, equal or case-different insensitive keys and keys that differ only in the apostrophe style; the alias-collision
theory requires the same rejection from `Create` and, with `Line 2:`, from `FromTsv`, so no two lookup rows can
claim the
same spelling. A sensitive and an insensitive entry for `Polish`, sensitive entries that differ only in case and
insensitive keys that differ in a dotted capital I are accepted. The reading-entry theory rejects a missing or empty id,
an ill-formed id, an id that is not in the inventory and a word that is not a form of the reading, each with the fixed
reason and without the word or the id in the message; the case test accepts `RECORD` for `record.noun` and rejects a
word
with a non-ASCII look-alike letter.

##### Tab-separated loading

**Tests**: `PronunciationOverrides_FromTsv_SampleDictionary_ParsesEntries`,
`PronunciationOverrides_SampleDictionaryKeys_AreSingleTokensOfTheRealTokenizer`,
`PronunciationOverrides_FromTsv_LineEndingsAndBom_AreAccepted`,
`PronunciationOverrides_FromTsv_LineLengthBoundary_IsInclusive`,
`PronunciationOverrides_FromTsv_LineCountBoundary_IsInclusive`,
`PronunciationOverrides_FromTsv_EndlessInputWithoutNewline_StopsAtLimit`,
`PronunciationOverrides_FromTsv_MalformedRows_ThrowWithLineNumberAndNoContent`,
`PronunciationOverrides_FromTsv_DuplicateAndNullReader_Throw`,
`PronunciationOverrides_FromTsv_ReadingAction_IsValidatedWithLineNumbers`,
`PronunciationOverrides_FromTsv_ReaderFailure_Propagates`,
`PronunciationOverrides_FromTsv_DoesNotDisposeReader`

The sample file loads to 23 entries in file order, the first seven being exactly the entries of the user guide, and
`Create` over the loaded entries gives the same entries and the same directives for a text. Each key of the sample (the
contraction key excepted) is found as one token when the real tokenizer sees it in a sentence, so a tokenizer change
cannot silently make a documented key inert. The line-ending test accepts LF, CRLF, mixed line ends, a leading
byte-order
mark (also through a `StreamReader` over real bytes), a missing final line end and an empty fourth column, all yielding
the same entries. The line-length test accepts a 1,024-character line and rejects one more, naming the line; the
line-count test accepts 50,000 lines of blank text and rejects the next; the endless-input test gives the loader a
reader
that never produces a line break and the loader must have read at most one character beyond a line's limit. The
malformed-row theory puts the bad row on line 3, after a comment and a blank line, and requires an
`InvalidDataException`
that begins with `Line 3:` and never contains the sentinel word of the row; its rows cover a wrong number of columns, a
payload where none is allowed or none where one is required, an unknown or wrongly cased case or action word, a bad
say or
pronunciation payload, lines of whitespace only, leading or trailing whitespace in a column, a control character
(also in
a comment) and a lone carriage return. The duplicate test requires the later line (line 4) to be reported and a null
reader
to throw. The reading theory accepts the `reading` action and rejects a row without a payload, an unknown or
ill-formed id,
a word that is not a form of the id and an unknown action word, with the one-based line number and the fixed reason (for
example `Line 1: the reading id is not in the reading inventory.`). An `IOException` from the reader reaches the caller
unchanged, and the loader reads to the end and leaves the reader undisposed and usable.

##### Immutability

**Tests**: `PronunciationOverrides_Create_SourceMutatedAfterwards_DoesNotAffectDictionary`,
`PronunciationOverrides_Resolve_ConcurrentCalls_ReturnIdenticalDirectives`

Changing, extending and clearing the supplied list after `Create` leaves the entries and the matches unchanged, and the
entries are exposed as a read-only list that throws `NotSupportedException` when written. Sixteen parallel loops, each
resolving the same text 200 times against one shared dictionary, all return directives equal to a single-threaded
lookup.

##### Matching

**Tests**: `PronunciationOverrides_Resolve_Actions_MapToDirectives`,
`PronunciationOverrides_Resolve_OneDirectivePerToken_AscendingUnique`,
`PronunciationOverrides_Create_ReadingEntry_AcceptedAndResolvesToReadingDirective`,
`PronunciationOverrides_Resolve_PartialToken_DoesNotMatch`,
`PronunciationOverrides_Resolve_NeighborLetterMarkOrDigit_Blocks`,
`PronunciationOverrides_Resolve_CliticStem_Matches`,
`PronunciationOverrides_Resolve_HyphenCompound_OnlyWhole`,
`PronunciationOverrides_Resolve_ApostropheStyles_MatchBothSpellings`,
`PronunciationOverrides_Resolve_ApostropheCaseVariants_SelectOwnEntry`,
`PronunciationOverrides_Resolve_SensitiveAliasBeatsInsensitive`,
`PronunciationOverrides_Resolve_PlaceholderSeparators_AreBoundaries`,
`PronunciationOverrides_Resolve_AgainstNaiveReference_RandomKeysAndTexts`,
`PronunciationOverrides_Resolve_CaseSensitiveBeatsInsensitive`,
`PronunciationOverrides_Resolve_InsensitiveEntry_UsesOrdinalIgnoreCase`,
`PronunciationOverrides_Resolve_TurkishAndAzeriCurrentCulture_DoNotChangeMatching`,
`PronunciationOverrides_Resolve_SupplementaryLetters_MatchWithoutSplittingPairs`,
`PronunciationOverrides_Resolve_NoNormalizationFolding`,
`PronunciationOverrides_GoldenCases_NormalizeAsDocumented`

Skip, say, spell, pronunciation and reading entries become the skip, replace, spell, pronounce and reading directives
with
the value of the entry, and a repetitive 2,000-token text with a sensitive and an insensitive entry for one word gives
exactly 1,500 directives, unique and ascending. A reading entry resolves to one reading directive that carries the
id, the
insensitive entry matching `RECORD` and the sensitive one only `record`. A key `cat` never matches inside `concatenate`,
`cats`, `category` or `scat`; for a token cut out of a longer text, a letter, a combining mark, a decimal digit or a
supplementary-plane letter on either side blocks the match, while punctuation, a space or a placeholder does not.
The stem
`NASA` of `NASA's` matches, the pieces of the split `Don't` never do, and in `Nginx-based` only the whole compound key
matches. A key with an apostrophe matches the plain and the typographic spelling but not other apostrophe-like
characters; sensitive `O'Brien` and `O’BRIEN` select their own entries for all four spellings in either entry order,
and a
sensitive entry (or its alias) beats an insensitive one. Words on both sides of a tag placeholder both match. The random
test runs 20,000 iterations of the property described under the verification approach. With a sensitive entry
`Polish` and
an insensitive entry `polish`, the exact spelling gets the sensitive directive and every other spelling the
insensitive one.
The ordinal ignore-case theory states each expectation twice, as the dictionary's result and as the runtime's own
comparison, covering the dotted and dotless I, the Kelvin sign, the sharp s and a precomposed against a decomposed
accent.
The culture theory (Turkish, Azeri, US English, German and the invariant culture) requires identical matches for
`ISTANBUL`, `Istanbul`, `istanbul` and `TITLE`. The supplementary-letter test uses Deseret capital and small
letters: the
exact case always matches, a different case matches exactly when the runtime's ordinal ignore-case says so (the
per-runtime evidence) and never for a sensitive entry, and half a pair or a lone surrogate never matches and never
throws.
The precomposed and the decomposed spelling of a key are different keys. The golden test loads the sample dictionary,
runs
every row of the golden file through the real tokenizer and normalizer, collects every mismatch so one run reports every
wrong row, and compares the spoken text, the pronunciation marks, the spelled spans and the applied and ignored
counts; it
requires at least 45 rows with unique names. The rows pin the documented limitations: a token that carries its period is
not overridable, the stem of a split contraction never matches, and an override on a unit, magnitude or meridiem that a
number rule already consumed is ignored and counted.

##### Contract: cancellation and arguments

**Tests**: `PronunciationOverrides_Resolve_Cancellation_ThrowsOnPreCanceledTokenAndWhenCanceledWhileProcessing`,
`PronunciationOverrides_Resolve_TokenOutsideText_Throws`

The cancellation test resolves 5,000 tokens that never match with an observer that cancels while an early token is
visited and requires an `OperationCanceledException`, and then requires that an already canceled token visits nothing
and
throws; it asserts neither how many tokens were visited nor the polling cadence (design choices). A theory of tokens
that
start before the text, are empty, or end beyond it (including an `int.MaxValue` start that would overflow a sum) throws
`ArgumentException`, and a null text throws `ArgumentNullException`. How the lookup stays linear on a huge text is
recorded
in the *PronunciationOverrides Design*; no elapsed time is asserted.
