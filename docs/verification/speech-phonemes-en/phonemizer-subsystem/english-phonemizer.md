### EnglishPhonemizer Verification

#### Verification Approach

`EnglishPhonemizer` is verified by unit tests in
`test/DemaConsulting.Speech.Phonemes.En.Tests/PhonemizerSubsystemTests.cs` (the `EnglishPhonemizer_*` tests) that call
the public methods directly. The unit depends on the tokenizer, the pronouncer and the lexicon, which its design
documents as dependencies, so those are real; the behaviors of the whole phonemization are the subsystem and system
tests.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: no files or network; the phonemizer is immutable and shared

#### Acceptance Criteria

A null text throws an argument error with and without annotations; the text `The record was 5.` gives the same items
(text, phones, source) with an empty annotation list as without annotations.

#### Test Scenarios

##### Argument contract

**Test**: `EnglishPhonemizer_Phonemize_NullText_Throws`

##### Empty annotations equal no annotations

**Test**: `EnglishPhonemizer_Phonemize_EmptyAnnotations_EqualsNoAnnotations`
