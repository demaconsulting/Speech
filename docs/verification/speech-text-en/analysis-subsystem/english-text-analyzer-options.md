### EnglishTextAnalyzerOptions Verification

#### Verification Approach

`EnglishTextAnalyzerOptions` and `TextEnAnnotationSelection` are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerOptionsTests.cs`. The only
collaborators are a recording diagnostics sink, used as an opaque value, and a small real `PronunciationOverrides`
dictionary, used as an opaque reference; the analyzer's use of the selection and the threshold is verified in the
`EnglishTextAnalyzer` unit tests.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files

#### Acceptance Criteria

The defaults select part-of-speech, normalization and reading annotations with no sink and a low-confidence threshold of
0.60; initialization sets both values and a with-expression copies without changing the original; every combination of
the defined flags (none, each of the three families alone, and every combination of them) is accepted; an undefined flag
throws `ArgumentOutOfRangeException` at initialization and in a with-expression (including the value 8, which is the
first undefined bit now that 4 is the `Reading` flag, combined with a defined flag); and the selection enumeration is a
flags enumeration with `None` equal to zero, `PartOfSpeech` equal to one, `Normalization` equal to two and `Reading`
equal to four. The threshold accepts every finite value from 0 to 1, keeps it through a with-expression and re-validates
it there, and rejects a negative value, a value above 1, a NaN and both infinities at initialization and in a
with-expression. The pronunciation dictionary defaults to null, can be set at
initialization, is kept by reference through a with-expression of another property and can be replaced without
changing the original.

#### Test Scenarios

##### Defaults

**Test**: `EnglishTextAnalyzerOptions_Defaults_SelectAllFamiliesAndNoSink`

##### Initialization and copying

**Test**: `EnglishTextAnalyzerOptions_Initialization_SetsValuesAndWithExpressionCopies`

##### Defined flag combinations

**Test**: `EnglishTextAnalyzerOptions_Annotations_DefinedFlagCombinations_AreAccepted` (a theory over `None`, each of
the three families alone, the three pairs and all three)

##### Undefined flag

**Test**: `EnglishTextAnalyzerOptions_Annotations_UndefinedFlag_ThrowsArgumentOutOfRangeException`

##### Enumeration shape

**Test**: `TextEnAnnotationSelection_Members_AreFlagsWithNoneZero`

##### Low-confidence threshold

**Tests**: `EnglishTextAnalyzerOptions_LowConfidenceThreshold_ValuesFromZeroToOne_AreAcceptedAndCopied` (a theory over
0, 0.6, 0.999 and 1),
`EnglishTextAnalyzerOptions_LowConfidenceThreshold_OutOfRange_ThrowsArgumentOutOfRangeException` (a theory over -0.001,
1.001, NaN and both infinities)

An accepted value is returned unchanged and is kept by a with-expression that changes another property; a rejected value
throws in an object initializer and in a with-expression.

##### Pronunciation dictionary

**Test**: `EnglishTextAnalyzerOptions_Overrides_DefaultsToNullAndCopiesWithWith`

The default options hold no dictionary. Options built with a dictionary return the same instance, a with-expression
that changes the annotation selection returns the same dictionary instance, and a with-expression that sets the
dictionary to null gives null while the original keeps its dictionary. The dictionary is validated when it is
built, not here, and the combination with the selection is validated by the analyzer constructor (see the
`EnglishTextAnalyzer` verification).
