### LtsModel Verification

#### Verification Approach

`LtsModel` is verified by unit tests in `test/DemaConsulting.Speech.Phonemes.En.Tests/LetterToSoundSubsystemTests.cs`
(the `LtsModel_*` tests) driving the shipped model directly, without the converter around it, over a deterministic
sample of the dictionary's spellings (every 40th word with a vowel letter, so the test needs no external data and covers
short and long words) and with hand-made damaged images for the loader. The pinned pronunciations of out-of-vocabulary
words are the subsystem golden (see *LetterToSoundSubsystem Verification*); the accuracy is the benchmark and the
identity of the shipped model with its training is `train-lts --check`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, on every target framework (the exactness of the integer arithmetic
  across them is what the repeated golden shows)
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: no files or network; the shared model is immutable

#### Acceptance Criteria

More than 3,000 sampled dictionary spellings each get a non-empty pronunciation with exactly one primary stress, and
converting a word twice gives identical text; an image of six arbitrary bytes and an image that is only the magic number
are rejected with an invalid-data error; so is the shipped image with a damaged chunk count or chunk length, a damaged
layer width, a cut at several points or a trailing byte (a theory, each message naming the damage); a non-gzip stream is
rejected the same way; a null image or stream is rejected with an argument error.

#### Test Scenarios

##### Stress and repeatability

**Test**: `LtsModel_Convert_DictionarySpellings_HaveOnePrimaryStressAndAreRepeatable`

##### Damaged image

**Tests**: `LtsModel_FromImage_DamagedImage_IsRejected`,
`LtsModel_FromImage_DamagedHeaderCountOrLength_ThrowsInvalidDataNamingTheDamage`,
`LtsModel_FromGZipStream_NotGZip_ThrowsInvalidData`
