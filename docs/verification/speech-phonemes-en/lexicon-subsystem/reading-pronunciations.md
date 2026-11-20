### ReadingPronunciations Verification

#### Verification Approach

`ReadingPronunciations` is verified by unit tests in
`test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.Readings.cs` (the `ReadingPronunciations_*` tests)
against the real embedded tables for the resolution and with tables written in the test for the parser. The exhaustive
contract (every reading id has a row, every pair of the inventory resolves, the gap list is empty) is the subsystem test
(see *LexiconSubsystem Verification*); the reviewed sentence per reading id is the system test.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: no files or network; parser tests use strings

#### Acceptance Criteria

`record` under `record.noun` is `R EH1 K ER0 D` and under `record.verb` is `R AH0 K AO1 R D`; `records` under
`record.noun` is `R EH1 K ER0 D Z` by the ending rule; `does` under `do.verb` is the listed `D AH1 Z`; an unknown
reading id and a form unrelated to the lemma do not resolve and give a null pronunciation, and a null form throws; a
good two-line table parses, and a table with a source other than `cmu#N`, `rule:<name>` or `authored`, with a phone that
is not ARPAbet, or with a repeated reading id is rejected with a format error.

#### Test Scenarios

##### Resolution

**Tests**: `ReadingPronunciations_TryResolve_HeadwordInflectionAndOverride_ResolveFromTheTables`,
`ReadingPronunciations_TryResolve_UnknownReadingOrUnrelatedForm_IsAGap`

##### Table parsing

**Test**: `ReadingPronunciations_Parse_MalformedTables_AreRejected`

##### Lexicon corrections

**Test**: `ReadingPronunciations_LexiconCorrections_ParseAndShippedTable_AreWellFormedAndEveryRowCorrectsAnEntry`

A good table parses and looks a word up; a row with two columns, an upper-case word, a phone that is not ARPAbet or a
repeated word is rejected with a format error, a null table with `ArgumentNullException`; every row of the shipped table
(`nobly`, `novelly`) names a word the dictionary lists with a pronunciation that differs from the correction.
