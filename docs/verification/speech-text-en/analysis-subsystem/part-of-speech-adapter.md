### PartOfSpeechAdapter Verification

#### Verification Approach

`PartOfSpeechAdapter` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/PartOfSpeechAdapterTests.cs` over the real
part-of-speech package, which is the unit's one dependency and is verified separately as an OTS item. Tagger
creation is observed by giving each adapter a counting factory that still returns the real shared tagger, so
the process-wide tagger and the other tests are undisturbed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: each test creates its own adapter and counter

#### Acceptance Criteria

Token offsets index the given text and sentences come back in text order, including across a line break;
tagging reports the tag name, a confidence in range and the known-word flag; splitting without tagging and
splitting a text without tokens never create the tagger; repeated calls create it once and concurrent first
uses create it once; a factory that fails once is retried by the next call (the failure is neither cached nor
counted as a creation), and racing callers still create the tagger once successfully; an already-canceled
token throws before any splitting or creation, and cancellation requested during tagger initialization, or
right after the only or the final sentence was tagged, throws instead of returning a result; null arguments
throw `ArgumentNullException`; and the shared adapter is one instance.

#### Test Scenarios

##### Offsets and ordering

**Test**: `PartOfSpeechAdapter_Analyze_MultiSentenceText_TokenOffsetsIndexSourceInSentenceOrder`

##### Tag reporting

**Test**: `PartOfSpeechAdapter_Analyze_TagTrue_ReportsTagConfidenceAndKnownWord`

##### Splitting without the tagger

**Tests**: `PartOfSpeechAdapter_Analyze_TagFalse_ReportsUntaggedTokensAndCreatesNoTagger`,
`PartOfSpeechAdapter_Analyze_TextWithoutTokens_ReturnsNoSentencesAndCreatesNoTagger`

##### Lazy, single tagger

**Tests**: `PartOfSpeechAdapter_Analyze_RepeatedCalls_CreateTaggerLazilyAndOnce`,
`PartOfSpeechAdapter_Analyze_ConcurrentFirstUses_CreateTaggerOnce`,
`PartOfSpeechAdapter_Analyze_FactoryFailsOnce_NextCallRetriesAndSucceeds`,
`PartOfSpeechAdapter_Analyze_ConcurrentCallersAfterOneFailure_CreateTaggerOnceSuccessfully`,
`PartOfSpeechAdapter_Analyze_FactoryReturnsNull_ThrowsInvalidOperationException`,
`PartOfSpeechAdapter_Shared_ReturnsSameInstance`

The concurrent tests release thirty-two tasks through one gate so the first uses genuinely race. The
failure tests use a counting factory whose first invocation throws: the first call throws, the second
succeeds, the factory ran exactly twice in total, and with racing callers exactly one fails and the tagger
is created once successfully.

##### Cancellation

**Tests**: `PartOfSpeechAdapter_Analyze_CanceledToken_ThrowsWithoutCreatingTagger`,
`PartOfSpeechAdapter_Analyze_CanceledDuringTaggerInitialization_ThrowsBeforeTaggingAnySentence`,
`PartOfSpeechAdapter_Analyze_CanceledAfterOnlySentenceTagged_Throws`,
`PartOfSpeechAdapter_Analyze_CanceledAfterFinalSentenceTagged_Throws`

The factory cancels the token (initialization case), or the adapter's internal after-sentence observer
cancels it at an exact point (only and final sentence cases), so the checks are exercised
deterministically without timing. The periodic check while tokens are copied is a cheap backstop for
cancellation that arrives from another thread mid-copy; it is verified by review, not by a timing test.

##### Argument validation

**Test**: `PartOfSpeechAdapter_NullArguments_ThrowArgumentNullException`
