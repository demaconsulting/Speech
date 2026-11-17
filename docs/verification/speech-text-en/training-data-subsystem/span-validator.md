### SpanValidator Verification

#### Verification Approach

`SpanValidator` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/SpanValidatorTests.cs`. It is a pure function,
so the tests pass it sentences and sets of forms directly with no file, no fake and no report. The surrogate pair and
non-BMP cases use explicit `\u` escapes because the committed data contains none, so only these tests can exercise them.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: pure function; no shared state

#### Acceptance Criteria

An exact whole-word span is valid (the result is null); a span at a sentence edge or next to punctuation is valid; a
span whose neighbor before or after is a letter, a digit, a non-ASCII letter or a superscript digit is an error, as is a
span that is off by one, empty, reversed or out of range. A form is accepted in any letter case when it is listed and is
an error when it is not. A start or end offset inside a surrogate pair is an error, a letter outside the Basic
Multilingual Plane next to the span is one neighbor that is a letter, a symbol outside that plane is a word boundary,
and a lone surrogate is not a letter.

#### Test Scenarios

##### Whole word

**Tests**: `SpanValidator_Validate_ExactWord_ReturnsNull`, `SpanValidator_Validate_EdgesAndPunctuation_AreWholeWords`,
`SpanValidator_Validate_LetterOrNumberBefore_ReturnsPrecededError`,
`SpanValidator_Validate_LetterOrNumberAfter_ReturnsFollowedError`, `SpanValidator_Validate_OffByOne_ReturnsError`,
`SpanValidator_Validate_OutOfRange_ReturnsError`

The neighbor tests are groups of cases (a letter, a digit, an accented letter, a superscript digit).

##### Listed form

**Tests**: `SpanValidator_Validate_CapitalizedWord_MatchesListedFormCaseInsensitively`,
`SpanValidator_Validate_UnlistedForm_ReturnsError`

##### UTF-16 and scalar offsets

**Tests**: `SpanValidator_Validate_StartInsideSurrogatePair_ReturnsError`,
`SpanValidator_Validate_EndInsideSurrogatePair_ReturnsError`,
`SpanValidator_Validate_AstralLetterBefore_ReturnsPrecededError`,
`SpanValidator_Validate_AstralLetterAfter_ReturnsFollowedError`,
`SpanValidator_Validate_AstralSymbolNeighbors_AreWordBoundaries`,
`SpanValidator_Validate_LoneSurrogateNeighbor_IsNotALetter`
