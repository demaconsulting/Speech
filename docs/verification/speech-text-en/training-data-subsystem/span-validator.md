### SpanValidator Verification

#### Verification Approach

`SpanValidator` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/SpanValidatorTests.cs`. It is a pure function,
so the tests pass it sentences and sets of forms directly with no file, no fake and no report. The surrogate pair and
non-BMP cases use explicit `\u` escapes because the committed data contains none, so only these tests can exercise them.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: pure function; no shared state

#### Acceptance Criteria

**Valid span** (`ValidSpan`). An exact whole-word span, also at a sentence edge or next to punctuation, is valid (the
result is null); a listed form is accepted in any letter case and an unlisted one is an error. A span whose neighbor
before or after is a letter, a digit or a superscript digit is an error, as is a span that is off by one, empty,
reversed or out of range. A start or end offset inside a surrogate pair is an error, a letter outside the Basic
Multilingual Plane next to the span is a letter, and a lone surrogate is not a letter.

#### Test Scenarios

##### Valid span

**Tests**: `SpanValidator_Validate_ExactWord_ReturnsNull`, `SpanValidator_Validate_EdgesAndPunctuation_AreWholeWords`,
`SpanValidator_Validate_CapitalizedWord_MatchesListedFormCaseInsensitively`,
`SpanValidator_Validate_UnlistedForm_ReturnsError`, `SpanValidator_Validate_LetterOrNumberBefore_ReturnsPrecededError`,
`SpanValidator_Validate_LetterOrNumberAfter_ReturnsFollowedError`, `SpanValidator_Validate_OffByOne_ReturnsError`,
`SpanValidator_Validate_OutOfRange_ReturnsError`, `SpanValidator_Validate_StartInsideSurrogatePair_ReturnsError`,
`SpanValidator_Validate_LoneSurrogateNeighbor_IsNotALetter`

The neighbor theories use an ASCII letter, a superscript digit and a letter outside the Basic Multilingual Plane before
the span, and an accented letter, a digit and a supplementary-plane letter after it. The whole-word theory includes a
supplementary-plane symbol on either or both sides, which is a boundary. The surrogate test checks the start and the
end of the span separately.
