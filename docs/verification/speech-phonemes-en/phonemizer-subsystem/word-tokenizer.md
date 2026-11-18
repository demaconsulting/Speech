### WordTokenizer Verification

#### Verification Approach

`WordTokenizer` (with `SpokenText` and `AnnotationIndex`) is verified by unit tests in
`test/DemaConsulting.Speech.Phonemes.En.Tests/PhonemizerSubsystemTests.cs` (the `WordTokenizer_*` tests) that build a
spoken text with `SpokenText.Build` and tokenize it with the real lexicon, asserting the token texts and kinds; no
pronouncer or phonemizer is involved. Annotations are made by small helpers in the test class.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: no files or network; the classes are immutable or stateless

#### Acceptance Criteria

`Wait... don't well-known 42 😀 <placeholder> go!` gives the tokens `Wait`, `...`, `don't`, `well-known`, `42`, the
emoji and `go`, `!` with the kinds word, punctuation, word, word, digits, unsupported, word, punctuation; `'tis` is one
word and `'hello'` is punctuation, word, punctuation; a replacement of `Dr.` by `Doctor` gives `Doctor Smith`, a
replacement running past the end of the text changes nothing, and a spelled `F B I` before `'s` gives three letter
tokens and a clitic token.

#### Test Scenarios

##### Token classes

**Test**: `WordTokenizer_Tokenize_WordsPunctuationDigitsAndUnsupportedRuns_AreClassified`

##### Leading apostrophe

**Test**: `WordTokenizer_Tokenize_LeadingApostrophe_IsPartOfAWordOnlyWhenTheDictionaryListsIt`

##### Replacements and spelled letters

**Test**: `WordTokenizer_Tokenize_ReplacementsAndSpelledLetters_AreAppliedAndBadAnnotationsIgnored`
