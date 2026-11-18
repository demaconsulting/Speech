### Phone Verification

#### Verification Approach

`Phone`, `Pronunciation` and `LetterNames` are verified by a unit test in
`test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.cs` (the `Pronunciation_*` test) that parses and
prints a pronunciation, with no dependency on any other unit. The types are small value types whose behavior is
exercised, in addition, by every other test of the package, since all of them are written in the dictionary's text form;
the letter names are exercised by the spelled-letter tests of the PhonemizerSubsystem and the vowel-less word test of
the LetterToSoundSubsystem.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: no files or network; the types are immutable

#### Acceptance Criteria

The text `R EH1 K ER0 D` parses to a pronunciation of five phones that prints back as the same text; the second phone
has stress 1 and base symbol `EH`, the first (a consonant) has stress -1; a text with the symbol `QQ1` throws a format
error.

#### Test Scenarios

##### Text form round trip

**Test**: `Pronunciation_Parse_TextForm_RoundTrips`
