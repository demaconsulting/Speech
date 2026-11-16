### TextEnAnnotations Verification

#### Verification Approach

`TextEnAnnotations` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/TextEnAnnotationsTests.cs`, constructing
`SegmentAnnotation` values directly so the reader is tested independently of the analyzer.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: no test needs network access or files

#### Acceptance Criteria

The six kinds have their documented, distinct names; a part-of-speech annotation with a Universal
Dependencies value is read back; an annotation of another kind, with a lower-case, longer or empty value, or
with a differently cased kind is reported as not a part-of-speech annotation without throwing; a normalization
annotation is read back with its exact spoken text, including a padded value, the apostrophe and the empty
value that means "speak nothing"; an annotation of another kind or a differently cased or padded kind is
reported as not a normalization without throwing; and a null annotation throws `ArgumentNullException` from
either reader.

#### Test Scenarios

##### Kind names

**Test**: `TextEnAnnotations_Kinds_HaveDocumentedDistinctNames`

##### Reading a part of speech

**Test**: `TextEnAnnotations_TryReadPartOfSpeech_PartOfSpeechAnnotation_ReturnsTagName`

##### Not a part-of-speech annotation

**Test**: `TextEnAnnotations_TryReadPartOfSpeech_OtherKindOrUnknownValue_ReturnsFalse`

##### Null annotation

**Test**: `TextEnAnnotations_TryReadPartOfSpeech_NullAnnotation_ThrowsArgumentNullException`

##### Reading a normalization

**Test**: `TextEnAnnotations_TryReadNormalized_NormalizationAnnotation_ReturnsSpokenText` (a theory over
`Doctor`, a value with surrounding spaces, an apostrophe and the empty value)

##### Not a normalization

**Test**: `TextEnAnnotations_TryReadNormalized_OtherKind_ReturnsFalse` (a theory over `en.upos`, an upper-case
kind, a kind with a trailing space and `en.spell`)

##### Null annotation for the normalization reader

**Test**: `TextEnAnnotations_TryReadNormalized_NullAnnotation_ThrowsArgumentNullException`
