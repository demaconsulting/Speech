### PronunciationOverrides Verification

#### Verification Approach

`PronunciationOverrides` (with `PronunciationOverrideEntry`, the `OverrideAction` and `PronunciationScheme`
enumerations and the internal `OverrideSyntax` grammars documented with it) is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/PronunciationOverridesTests.cs`. The tests drive
`Create` and `FromTsv` directly, and drive the internal `Resolve` over the tokens of the real tokenizer (through
the `NormalizationHarness` helper of the test project) or, where the tokenizer would hide the case under test,
over tokens that the test cuts itself. No fake is needed: the grammars, the tokenizer and the normalizer are real.

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
  input at its limit, a failing reader and a tracking reader prove how reader exceptions and disposal are
  handled.

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

Every invalid word (empty, too long, malformed UTF-16, a wrong first or last character, a character outside the allowed
set, or mixed apostrophe styles), an undefined action (outside the five defined values, 1 to 5), a payload that does not
fit its action and a spelled word that is not 1 to 32 ASCII letters is rejected with an `ArgumentException` that holds
the zero-based entry index and never the word or payload, while a null list or element throws `ArgumentNullException`;
say payloads and the `ipa:` and `arpabet:` pronunciation payloads accept exactly the documented grammar and bounds and
reject any other scheme; a reading entry is accepted only when its payload is a well-formed reading id that exists and
is active in the reading inventory and the entry's word, folded with ASCII case folding, is a form of that reading, and
every other case is rejected with a fixed reason that never echoes the word or the id; duplicates (same word, same case
flag, apostrophe styles unified) are rejected while a sensitive and an insensitive entry for one word are accepted;
every limit is accepted exactly at its value and rejected one beyond it, and an endless input is abandoned at its limit;
a tab-separated file gives exactly the dictionary that `Create` gives, accepts both line ends, a byte-order mark and a
missing final line end, and rejects every malformed row with the one-based line number and no content, while reader
exceptions propagate and the reader is neither disposed nor retained; the dictionary is immutable and shares one empty
instance; case-sensitive matching is exact, insensitive matching is the ordinal ignore-case of the runtime whatever the
current culture, a sensitive entry beats an insensitive one, and no normalization form is applied; only a whole token
matches (never a piece next to a letter, a mark or a digit), clitic stems, hyphen compounds, both apostrophe styles and
words next to a tag placeholder behave as documented; each action maps to its directive, with at most one directive per
token in ascending order; the lookup agrees with the naive reference for 20,000 random cases; cancellation is checked
before any work and every 256 visited tokens; non-matching tokens yield no directive and a large text resolves in linear
time; invalid arguments to the lookup throw; and every row of the golden file normalizes as documented.

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
`Entry 1:` and does not contain the word; its rows cover empty and blank words, a leading or trailing apostrophe, hyphen
or period, digits at the start, clause punctuation, control, format, private-use and unassigned characters, lone
surrogates, an emoji, a dotted initialism ending in a period (`U.S.A.`), a combining mark in first position and mixed
apostrophe styles. The length test accepts a word of exactly 64 code units and rejects one more. The valid-word theory
accepts letters of several scripts, supplementary-plane letters, marks inside a word, a trailing digit and the
characters `' ’ - . & / _`. The undefined-action theory rejects the values 0, 6, 9 and -1 (the value 5 is the reading
action, so the undefined values are those outside 1 to 5). The mismatch test rejects a payload on a skip or spell entry
and no payload on a say or pronunciation entry, using a sentinel payload that must not appear in the message. The
spell-out theory rejects non-ASCII, digit, hyphen, apostrophe and over-long words and accepts exactly 32 letters. The
null test covers a null list, a null element, null arguments of every factory and a null word in the entry constructor.

##### Payload grammar

**Tests**: `PronunciationOverrides_Create_SayPayload_GrammarEnforced`,
`PronunciationOverrides_Create_SayPayloadLength_BoundaryIsInclusive`,
`PronunciationOverrides_Create_PronunciationPayload_SchemeAndSyntaxEnforced`,
`PronunciationOverrides_Create_PhonemeLengthBounds_AreInclusive`,
`PronunciationOverrides_Factories_ProduceValidatedEntries`

The say theory accepts words with apostrophes, hyphens, accented letters and single inner spaces, and rejects
empty, padded, double-spaced, digit, clause-punctuation, control, no-break-space, format and lone-surrogate
payloads. The length test accepts 256 code units and rejects 257. The pronunciation theory accepts `ipa:` and
`arpabet:` payloads (including a colon inside the IPA phonemes) and rejects a missing or upper-case scheme, an
unknown scheme, whitespace or control characters in IPA, and ARPAbet phones that are lower case, double-spaced,
padded, longer than three letters or carry a stress digit above 2. The bounds test accepts 128 IPA code units and
32 ARPAbet phones and rejects one more of each and a payload over the total limit. The factory test checks that
each factory produces the equal entry (with the lower-case scheme name in the pronunciation payload), that an
undefined scheme value throws `ArgumentOutOfRangeException`, and pins the numeric values of the two enumerations.

##### Reading action

**Tests**: `PronunciationOverrideEntry_Reading_CreatesReadingEntryWithIdPayload`,
`PronunciationOverrides_Create_ReadingEntry_AcceptedAndResolvesToReadingDirective`,
`PronunciationOverrides_Create_ReadingEntryInvalid_ThrowsFixedReason`,
`PronunciationOverrides_Create_ReadingEntryWordCase_IsFoldedAsciiOnly`,
`PronunciationOverrides_FromTsv_ReadingAction_IsValidatedWithLineNumbers`

The factory test pins the numeric value 5 of `OverrideAction.Reading`, the id as payload, the case flag (false by
default) and the null arguments. A valid `record` / `record.noun` entry is accepted by `Create`, and a `Record` /
`record.verb` row is accepted by `FromTsv`; each resolves to one reading directive that carries the id (at token 1 of
`A record.`), the insensitive entry matches `RECORD` and the sensitive one matches only `record`. The invalid-entry
theory rejects a missing or empty id, ill-formed ids (upper case, no dot, a trailing space), ids that are not in the
inventory (an invented lemma and `record.ghost`) and words that are not a form of the reading (`read.past` for `record`,
an unknown word and the compound `record-label`), each with the fixed reason and without the word or the id in the
message. The case test accepts `RECORD` and rejects a word with a non-ASCII look-alike letter. The TSV theory accepts
the `reading` action (also with a comment and mixed case in the word), and rejects a row without a payload, an unknown
id, an ill-formed id, a word that is not a form of the id and an unknown action word `reads`, with the one-based line
number (a bad row on line 2 reports `Line 2:`) and the fixed reason, for example:

```text
Line 1: the reading id is not in the reading inventory.
```

##### Duplicates

**Tests**: `PronunciationOverrides_Create_DuplicateKeys_Throws`,
`PronunciationOverrides_Create_SensitiveAndInsensitiveSameWord_Allowed`,
`PronunciationOverrides_AliasCollisions_AreRejectedByCreateAndFromTsv`

The duplicate theory requires the later entry to be rejected (`Entry 1:` and the word "duplicate") for equal
sensitive keys, equal or case-different insensitive keys, and keys that differ only in the apostrophe style. The
alias-collision theory requires the same rejection from `Create` and, with `Line 2:`, from `FromTsv` for plain
versus typographic spellings and for case-different insensitive keys with mixed apostrophe styles, so no two
lookup rows can claim the same spelling. The
second test accepts a sensitive and an insensitive entry for `Polish`, sensitive entries that differ only in
case, and insensitive keys that differ in a dotted capital I.

##### Limits

**Tests**: `PronunciationOverrides_Create_EntryCountBoundary_IsInclusive`,
`PronunciationOverrides_FromTsv_LineLengthBoundary_IsInclusive`,
`PronunciationOverrides_FromTsv_LineCountBoundary_IsInclusive`,
`PronunciationOverrides_FromTsv_EntryCountBoundary_NamesTheLine`,
`PronunciationOverrides_FromTsv_EndlessInputWithoutNewline_StopsAtLimit`,
`PronunciationOverrides_FromTsv_EndlessShortLines_StopsAtLineLimit`

The entry-count test accepts exactly 10,000 entries and rejects the 10,001st (naming the entry), and an endless
entry sequence is cut off rather than enumerated to its end. The line-length test accepts a 1,024-character line
and rejects one more character, naming the line. The line-count test accepts 50,000 lines of blank text and
rejects the next one. The file entry-count test names the line of the first entry beyond 10,000. The two endless
tests give the loader a reader that never ends: the first never produces a line break and the loader must have
read at most one character beyond a line's limit; the second produces only empty lines and the loader must stop
at the line limit.

##### Tab-separated loading

**Tests**: `PronunciationOverrides_FromTsv_SampleDictionary_ParsesEntries`,
`PronunciationOverrides_SampleDictionaryKeys_AreSingleTokensOfTheRealTokenizer`,
`PronunciationOverrides_FromTsv_LineEndingsAndBom_AreAccepted`,
`PronunciationOverrides_FromTsv_EquivalentToCreate`,
`PronunciationOverrides_Empty_IsSharedAndEmpty`

The sample file loads to 23 entries in file order, the first seven being exactly the entries of the user guide.
Each key of the sample (the contraction key excepted) is found as one token when the real tokenizer sees it in a
sentence, so a tokenizer change cannot silently make a documented key inert; the contraction key is pinned as the
documented limitation that never matches. The line-ending test accepts LF, CRLF, mixed line ends, a leading
byte-order mark (also through a `StreamReader` over real bytes), a missing final line end and an empty fourth
column, all yielding the same entries. The equivalence test shows that `Create` over the loaded entries gives the
same entries and the same directives for a text. The empty test shows that no entries, an empty file and a file of
only comments all give the one shared empty dictionary, which yields no directives.

##### Tab-separated validation

**Tests**: `PronunciationOverrides_FromTsv_MalformedRows_ThrowWithLineNumberAndNoContent`,
`PronunciationOverrides_FromTsv_DuplicateAndNullReader_Throw`,
`PronunciationOverrides_FromTsv_ReaderFailure_Propagates`,
`PronunciationOverrides_FromTsv_DoesNotDisposeReader`

The malformed-row theory puts the bad row on line 3, after a comment and a blank line, and requires an
`InvalidDataException` that begins with `Line 3:` and never contains the sentinel word of the row. The rows cover a
wrong number of columns, a payload where none is allowed, no payload where one is required, an unknown or wrongly
cased case or action word, a bad say or pronunciation payload, the header row, lines of whitespace or tabs only,
leading or trailing whitespace in a column (including a no-break space), control characters (also in a
comment), a lone carriage return, and invalid or non-spellable words. The duplicate test requires the later line
(line 4) to be reported and a null reader to throw `ArgumentNullException`. The failure test checks that an
`IOException` from the reader reaches the caller unchanged. The disposal test checks that the loader reads to the end
and leaves the reader undisposed and usable.

##### Immutability

**Tests**: `PronunciationOverrides_Create_SourceMutatedAfterwards_DoesNotAffectDictionary`,
`PronunciationOverrides_Entries_IsReadOnlyCopy`,
`PronunciationOverrides_Resolve_ConcurrentCalls_ReturnIdenticalDirectives`

Changing, extending and clearing the supplied list after `Create` leaves the entries and the matches unchanged.
The entries are exposed as a read-only list that is neither a `List` nor an array, and writing to it throws
`NotSupportedException`. Sixteen parallel loops, each resolving the same text 200 times against one shared
dictionary, all return directives equal to a single-threaded lookup.

##### Case rules

**Tests**: `PronunciationOverrides_Resolve_CaseSensitiveBeatsInsensitive`,
`PronunciationOverrides_Resolve_InsensitiveEntry_UsesOrdinalIgnoreCase`,
`PronunciationOverrides_Resolve_TurkishAndAzeriCurrentCulture_DoNotChangeMatching`,
`PronunciationOverrides_Resolve_SupplementaryLetters_MatchWithoutSplittingPairs`,
`PronunciationOverrides_Resolve_NoNormalizationFolding`,
`NormalizationSubsystem_Overrides_UnderForeignCultures_GiveIdenticalResults` (in
`NormalizationSubsystemTests.cs`; see the NormalizationSubsystem verification)

With a sensitive entry `Polish` and an insensitive entry `polish`, the exact spelling gets the sensitive directive and
every other spelling gets the insensitive one. The ordinal ignore-case theory states each expectation twice: as the
dictionary's result and as the runtime's own comparison, covering the dotted and dotless I, the Kelvin sign, the sharp s
and a precomposed against a decomposed accent. The culture theory (Turkish, Azeri, US English, German and the invariant
culture) requires identical matches for `ISTANBUL`, `Istanbul`, `istanbul`, `TITLE` and a second Turkish name with a
dotted capital I, and none for the dotted or dotless forms that do not fold. The supplementary-letter test uses Deseret
capital and small letters: the exact case always matches, a different case matches exactly when the runtime's ordinal
ignore-case says so for the runtime executing the test (the per-runtime evidence) and never for a sensitive entry, and
half a pair or a lone surrogate never matches and never throws. The normalization test shows that the precomposed and
the decomposed spelling of a key are different.

##### Whole-word matching

**Tests**: `PronunciationOverrides_Resolve_PartialToken_DoesNotMatch`,
`PronunciationOverrides_Resolve_NeighborLetterMarkOrDigit_Blocks`,
`PronunciationOverrides_Resolve_CliticStem_Matches`,
`PronunciationOverrides_Resolve_HyphenCompound_OnlyWhole`,
`PronunciationOverrides_Resolve_ApostropheStyles_MatchBothSpellings`,
`PronunciationOverrides_Resolve_ApostropheCaseVariants_SelectOwnEntry`,
`PronunciationOverrides_FromTsv_ApostropheCaseVariants_SelectOwnEntry`,
`PronunciationOverrides_Resolve_SensitiveAliasBeatsInsensitive`,
`PronunciationOverrides_Resolve_PlaceholderSeparators_AreBoundaries`,
`PronunciationOverrides_Resolve_AgainstNaiveReference_RandomKeysAndTexts`

A key `cat` never matches inside `concatenate`, `cats`, `category` or `scat`. For a token cut out of a longer
text, a letter, a combining mark, a decimal digit or a supplementary-plane letter on either side blocks the match,
while punctuation, a space, a placeholder or a malformed neighbor does not. The stem `NASA` of `NASA's` matches,
the pieces of the split `Don't` never do. In `Nginx-based` neither part matches but the whole compound key does.
A key with an apostrophe matches the plain and the typographic spelling (in either case rule) but not other
apostrophe-like characters, and a key that mixes both is invalid. Sensitive `O'Brien` and `O’BRIEN` select their own
entries for all four spellings in either entry order, including from TSV, and a sensitive entry (or its alias)
beats an insensitive one for the same token. Words on both sides of a tag placeholder both
match. The random test runs 20,000 iterations of the property described under the verification approach.

##### Directives

**Tests**: `PronunciationOverrides_Resolve_Actions_MapToDirectives`,
`PronunciationOverrides_Resolve_OneDirectivePerToken_AscendingUnique`,
`PronunciationOverrides_GoldenCases_NormalizeAsDocumented`

Skip, say, spell and pronunciation entries become the skip, replace, spell and pronounce directives with the value
of the entry (the pronunciation value carrying the scheme and the phonemes), and an unmatched token gets none. A
repetitive 2,000-token text with a sensitive and an insensitive entry for one word gives exactly 1,500 directives,
unique and ascending. The golden test loads the sample dictionary, runs every row of the golden file through the
real tokenizer and normalizer, collects every mismatch so one run reports every wrong row, and compares the
spoken text, the pronunciation marks, the spelled spans and the applied and ignored counts. It also requires at
least 45 rows with unique names. The rows pin the documented limitations: a token that carries its period is not
overridable, the stem of a split contraction never matches, and an override on a unit, magnitude or meridiem that a
number rule already consumed is ignored and counted (multi-word keys are not matched because a key cannot contain a
space, which the entry validation scenario covers).

##### Cancellation and size

**Tests**: `PronunciationOverrides_Resolve_Cancellation_CheckedEveryIntervalCountingEveryToken`,
`PronunciationOverrides_Resolve_NoMatches_ReturnsNoDirectives`,
`PronunciationOverrides_Resolve_LargeText_CompletesWithinBudget`

The cancellation test resolves 1,000 tokens that never match with an observer that cancels while token 300 is
visited and requires that exactly the tokens 0 to 510 were visited (the next periodic check is at the 512th token)
and then that an already canceled token visits nothing. The no-match test resolves 5,000 tokens against a
2,000-entry dictionary, some of which pass the matching gates and some of which do not, and requires no directive.
The size test
resolves a text of about one million characters and a single 100,000-character token against a 10,000-entry
dictionary within a generous time budget, which detects a super-linear lookup.

##### Argument validation

**Tests**: `PronunciationOverrides_Resolve_NullText_Throws`,
`PronunciationOverrides_Resolve_TokenOutsideText_Throws`

A null text throws `ArgumentNullException`. A theory of tokens that start before the text, are empty or negative,
or end beyond it (including `int.MaxValue` starts and lengths that would overflow a sum) throws
`ArgumentException`.
