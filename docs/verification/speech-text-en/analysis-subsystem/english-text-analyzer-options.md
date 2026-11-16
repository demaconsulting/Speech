### EnglishTextAnalyzerOptions Verification

#### Verification Approach

`EnglishTextAnalyzerOptions` and `TextEnAnnotationSelection` are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerOptionsTests.cs`. The only
collaborators are a recording diagnostics sink, used as an opaque value, and a small real `PronunciationOverrides`
dictionary, used as an opaque reference.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files

#### Acceptance Criteria

The defaults select part-of-speech and normalization annotations with no sink; initialization sets both
values and a with-expression copies without changing the original; every combination of the defined flags
(none, each family alone and both) is accepted; an undefined flag throws `ArgumentOutOfRangeException` at
initialization and in a with-expression (including the formerly undefined value 4 combined with a defined
flag); and the selection enumeration is a flags enumeration with `None` equal to zero, `PartOfSpeech` equal
to one and `Normalization` equal to two. The pronunciation dictionary defaults to null, can be set at
initialization, is kept by reference through a with-expression of another property and can be replaced without
changing the original.

#### Test Scenarios

##### Defaults

**Test**: `EnglishTextAnalyzerOptions_Defaults_SelectPartOfSpeechAndNormalizationAndNoSink`

##### Initialization and copying

**Test**: `EnglishTextAnalyzerOptions_Initialization_SetsValuesAndWithExpressionCopies`

##### Defined flag combinations

**Test**: `EnglishTextAnalyzerOptions_Annotations_DefinedFlagCombinations_AreAccepted` (a theory over the four
combinations of the two defined flags)

##### Undefined flag

**Test**: `EnglishTextAnalyzerOptions_Annotations_UndefinedFlag_ThrowsArgumentOutOfRangeException`

##### Enumeration shape

**Test**: `TextEnAnnotationSelection_Members_AreFlagsWithNoneZero`

##### Pronunciation dictionary

**Test**: `EnglishTextAnalyzerOptions_Overrides_DefaultsToNullAndCopiesWithWith`

The default options hold no dictionary. Options built with a dictionary return the same instance, a with-expression
that changes the annotation selection returns the same dictionary instance, and a with-expression that sets the
dictionary to null gives null while the original keeps its dictionary. The dictionary is validated when it is
built, not here, and the combination with the selection is validated by the analyzer constructor (see the
`EnglishTextAnalyzer` verification).
