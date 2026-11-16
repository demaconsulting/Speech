### EnglishTextAnalyzerOptions Verification

#### Verification Approach

`EnglishTextAnalyzerOptions` and `TextEnAnnotationSelection` are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerOptionsTests.cs`. The only
collaborator is a recording diagnostics sink used as an opaque value.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files

#### Acceptance Criteria

The defaults select part-of-speech annotations with no sink; initialization sets both values and a
with-expression copies without changing the original; an undefined flag throws
`ArgumentOutOfRangeException` at initialization and in a with-expression; and the selection enumeration is a
flags enumeration with `None` equal to zero and `PartOfSpeech` equal to one.

#### Test Scenarios

##### Defaults

**Test**: `EnglishTextAnalyzerOptions_Defaults_SelectPartOfSpeechAndNoSink`

##### Initialization and copying

**Test**: `EnglishTextAnalyzerOptions_Initialization_SetsValuesAndWithExpressionCopies`

##### Undefined flag

**Test**: `EnglishTextAnalyzerOptions_Annotations_UndefinedFlag_ThrowsArgumentOutOfRangeException`

##### Enumeration shape

**Test**: `TextEnAnnotationSelection_Members_AreFlagsWithNoneZero`
