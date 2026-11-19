### LetterToSound Verification

#### Verification Approach

`LetterToSound` is verified by unit tests in
`test/DemaConsulting.Speech.Phonemes.En.Tests/LetterToSoundSubsystemTests.cs` (the `LetterToSound_*` tests) over the
shared converter with the real model: its contract is about what comes out for odd input, which only needs inputs and
the letter names (the one table of another unit that the vowel-less test reads). The pinned words and the concurrency
are subsystem tests.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: no files or network; the cache only saves time

#### Acceptance Criteria

Eleven odd spellings (`zzzzzzzzzzzzzzzz`, `aeiouaeiouaeiou`, `'tis`, `rock'n'roll`, `o'brien's`, `'''x'''`, `x`,
`qwrtp`, `www`, `Straße`, `ABC123`) are converted and each has a vowel; `sq` is spoken as the letter names of `s` and
`q`; a word of 4,000 letters is converted, has more than 1,000 phones and exactly one primary stress; and an empty
string, an apostrophe, digits and a hyphen have no pronunciation and report `false`.

#### Test Scenarios

##### Odd spellings

**Test**: `LetterToSound_TryConvert_OddSpellings_GetANonEmptyPronunciation` (an eleven-row theory)

##### Vowel-less words

**Test**: `LetterToSound_TryConvert_VowelLessWord_IsSpokenByLetterNames`

##### Very long word

**Test**: `LetterToSound_TryConvert_VeryLongWord_IsPronouncedWithoutFailure`

##### Text without letters

**Test**: `LetterToSound_TryConvert_NoLetters_HasNoPronunciation` (a four-row theory)
