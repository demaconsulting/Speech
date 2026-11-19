## LexiconSubsystem Verification

### Verification Approach

The SpeechPhonemesEn LexiconSubsystem is verified by deterministic tests in
`test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.cs`, `LexiconSubsystemTests.Morphology.cs`,
`LexiconSubsystemTests.Readings.cs` and `LexiconSubsystemTests.Images.cs` (one partial test class), with the real
embedded lexicon, the real reading tables and the real reading inventory of SpeechTextEn; nothing is mocked. The
subsystem-level tests (`LexiconSubsystem_*`) check what only the combination can show: that the lexicon returns every
entry it enumerates, that the reading table and the rules resolve every pair of the inventory, and that the morphology
rules agree with the lexicon's own inflected entries. The units are verified by their own `CmuLexicon_*`,
`Pronunciation_*`, `Morphology_*` and `ReadingPronunciations_*` tests in the same files (see the unit verification
documents).

The pinned content of the dictionary (that the embedded image is the pinned CMUdict filtered as documented) is verified
by the tools command `build-lexicon --check`, which needs the pinned download, and not by a test of the package; the
tests prove that the image is internally consistent and that what is derived from it behaves as designed.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework of the library
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Phonemes.En.Tests`
- **Files**: the committed form table of the reading inventory (`forms.tsv`) and the gap list (`reading-gaps.tsv`),
  copied next to the test assembly; the morphology audit of the tools project is compiled into the test assembly
- **Isolation**: no network, no model; the shared lexicon and tables are immutable

### Acceptance Criteria

The subsystem tests pass when: every entry the lexicon enumerates (more than 120,000 words, strictly sorted and unique)
is found again by lookup with identical variants; the reading table has a row for exactly the 395 reading ids of the
inventory and each `cmu#N` row equals that variant of the lexicon; all 853 spelling and reading pairs resolve to a
non-empty pronunciation, the embedded form table equals the committed one and the committed gap list is empty; and for
the plural, past, `-ing` and possessive endings, with more than 1,000 comparable dictionary entries each, the class
agreement of the derived ending with the dictionary's is at least 98 percent.

### Test Scenarios

#### Every dictionary entry is found again

**Test**: `LexiconSubsystem_Entries_EveryEntryIsFoundByLookup`

The lexicon enumerates its words in ordinal order; for each, `Lookup` returns exactly the enumerated variants; the count
equals `WordCount`.

#### Reading table covers the inventory

**Tests**: `LexiconSubsystem_Readings_Table_CoversTheInventoryAndMatchesTheLexicon`,
`LexiconSubsystem_Readings_Resolve_EveryInventoryPair_ResolvesWithNoSilentGap`

The set of reading ids of the table equals the set of the inventory (395); every row whose source is `cmu#N` is the Nth
variant of its lemma in the lexicon; the 853 pairs of the form table resolve through the table, the rules and the
overrides, none is unlisted or stale in the gap list, and the gap list is empty.

#### Morphology agrees with the dictionary

**Test**: `LexiconSubsystem_Morphology_Audit_RegularEndings_AgreeWithLexiconAtLeast98Percent`

The audit derives every dictionary entry that is a known stem plus a regular ending from the stem and compares the
result with the entry; the gate is a class agreement of at least 98 percent for each of the four endings.
