### CmuLexicon Verification

#### Verification Approach

`CmuLexicon` is verified by unit tests in `test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.cs` (the
`CmuLexicon_*` tests) against the real embedded lexicon: its contract is a lookup over data, and a lookup is only
meaningful over the real image. The expected values are a handful of well-known dictionary entries (`cat`, `record` and
`don't`). The exhaustive consistency check (every word the lexicon enumerates is found again) is the subsystem test, and
the identity of the image with the pinned dictionary is the tools command `build-lexicon --check`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: no files or network; the shared lexicon is immutable

#### Acceptance Criteria

`cat` has the single variant `K AE1 T`; `record` has the three variants `R AH0 K AO1 R D`, `R EH1 K ER0 D`,
`R IH0 K AO1 R D` in this order; `don't`, `DON'T` and `don’t` give the same non-empty answer; an absent word (`zzxqvk`),
an empty string, a word with digits (`route66`) and a hyphenated word (`well-known`) give an empty answer and are not
contained; and 64 parallel lookups of five words give the single-threaded answers.

#### Test Scenarios

##### Variants in dictionary order

**Test**: `CmuLexicon_Lookup_KnownWords_ReturnsAllVariantsInOrder`

##### Folding of case and apostrophe

**Test**: `CmuLexicon_Lookup_CaseAndApostrophe_AreFolded`

##### Absent and invalid words

**Test**: `CmuLexicon_Lookup_UnknownOrInvalidWord_ReturnsEmpty` (a four-row theory)

##### Concurrent use

**Test**: `CmuLexicon_Lookup_ConcurrentUse_GivesTheSingleThreadedAnswers`
