### EnglishTextAnalyzerOptions Verification

#### Verification Approach

`EnglishTextAnalyzerOptions` and `TextEnAnnotationSelection` are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/EnglishTextAnalyzerOptionsTests.cs`. The unit has no
collaborators the tests must control; the analyzer's use of the selection and the threshold is verified in the
`EnglishTextAnalyzer` unit tests.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files

#### Acceptance Criteria

- **Annotation selection**: the defaults select part-of-speech, normalization and reading annotations with no
  diagnostics sink; the extreme combinations of the defined flags (none, and a pair of families) are accepted; a
  selection with an undefined flag throws `ArgumentOutOfRangeException` at initialization and in a with-expression
- **Settings**: the defaults carry no dictionary and a low-confidence threshold of 0.60; the threshold accepts the
  boundary values 0 and 1 and keeps them in a copy; a negative value, a value above 1 and NaN are rejected at
  initialization and in a with-expression

#### Test Scenarios

##### Annotation selection

**Tests**: `EnglishTextAnalyzerOptions_Defaults_SelectAllFamiliesAndNoSink`,
`EnglishTextAnalyzerOptions_Annotations_DefinedFlagCombinations_AreAccepted`,
`EnglishTextAnalyzerOptions_Annotations_UndefinedFlag_ThrowsArgumentOutOfRangeException`

The default options select the three families, have no sink and no dictionary and a threshold of 0.60. The theory
accepts `None` and the pair of normalization and reading. The value `0x80`, and `8` combined with a defined flag (8 is
the
first undefined bit now that 4 is the `Reading` flag), throw in an object initializer and in a with-expression.

##### Settings

**Tests**: `EnglishTextAnalyzerOptions_LowConfidenceThreshold_ValuesFromZeroToOne_AreAcceptedAndCopied`,
`EnglishTextAnalyzerOptions_LowConfidenceThreshold_OutOfRange_ThrowsArgumentOutOfRangeException`

The values 0 and 1 are returned unchanged and kept by a with-expression that changes another property; -0.001, 1.001 and
NaN throw in an object initializer and in a with-expression. The dictionary is validated when it is built, not here, and
its combination with the selection is validated by the analyzer constructor (see the `EnglishTextAnalyzer`
verification).
