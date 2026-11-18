### PartOfSpeechAdapter Verification

#### Verification Approach

`PartOfSpeechAdapter` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/PartOfSpeechAdapterTests.cs` over the real
part-of-speech package, which is the unit's one dependency and is verified separately as an OTS item. The description
of a sentence for the heteronym features is verified against the heteronym context builder, the independent definition
of what one token's description is, with the real tagger. Each
adapter is given a test factory that still returns the real shared tagger (so the process-wide tagger and the
other tests are undisturbed) and can run an action, such as a cancellation, when the tagger is requested or fail
its first request, so those points are reached deterministically. The tests assert results, exceptions and
recovery, never how many times a tagger is created or whether the shared adapter is one instance (those are
design choices recorded in the *PartOfSpeechAdapter Design*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: each test creates its own adapter and test factory

#### Acceptance Criteria

Token offsets index the given text and sentences come back in text order, including across a line break;
tagging reports the tag name, a confidence in range and the known-word flag; splitting without tagging reports
untagged tokens and splitting a text without tokens returns no sentences; repeated calls and concurrent first
uses on one adapter return the same tagged tokens; a factory that fails once fails only that call (the next call
succeeds with the right tags), and racing callers see at most one failure while every other caller and every later
call gets the complete result; a factory that returns no tagger is reported as `InvalidOperationException`; an
already-canceled token throws, and cancellation requested during tagger initialization, or right after the only
or the final sentence was tagged, throws instead of returning a result; and null arguments throw
`ArgumentNullException`; and the description of a tagged sentence for the heteronym features has one entry per token
in sentence order, each equal to what the heteronym context builder gives for that token, also when the description is
written into a buffer that is longer than the sentence.

#### Test Scenarios

##### Sentence description

**Test**: `PartOfSpeechAdapter_DescribeSentence_TaggedSentence_DescribesEveryTokenLikeContextCreate`

The text `The record was broken. She said so.` is tagged. The first sentence is described, which gives a buffer larger
than the second sentence, and the second is described into that buffer. The first entries, as many as the second
sentence has tokens, each equal the description that the context builder gives for the token's text, span, tag and
confidence, so the analyzer and the trainer, which both describe a sentence this way, see the same sentence. The entries
of the buffer past the sentence are not asserted.

##### Offsets and ordering

**Test**: `PartOfSpeechAdapter_Analyze_MultiSentenceText_TokenOffsetsIndexSourceInSentenceOrder`

##### Tag reporting

**Test**: `PartOfSpeechAdapter_Analyze_TagTrue_ReportsTagConfidenceAndKnownWord`

##### Splitting without tags

**Tests**: `PartOfSpeechAdapter_Analyze_TagFalse_ReportsUntaggedTokens`,
`PartOfSpeechAdapter_Analyze_TextWithoutTokens_ReturnsNoSentences`

##### Concurrent use and recovery

**Tests**: `PartOfSpeechAdapter_Analyze_RepeatedCalls_ReturnSameTaggedTokens`,
`PartOfSpeechAdapter_Analyze_ConcurrentFirstUses_ReturnIdenticalResults`,
`PartOfSpeechAdapter_Analyze_FactoryFailsOnce_NextCallSucceeds`,
`PartOfSpeechAdapter_Analyze_ConcurrentCallersAfterOneFailure_OnlyTheFailingCallSeesIt`,
`PartOfSpeechAdapter_Analyze_FactoryReturnsNull_ThrowsInvalidOperationException`

The concurrent tests release thirty-two tasks through one gate so the first uses genuinely race, and compare the
tagged tokens (`The/DET cat/NOUN sat/VERB ./PUNCT`) of every caller. The failure tests use a factory whose first
invocation throws: the first call throws, the second succeeds with the tags of a healthy tagger, and with racing
callers at most one fails, every other caller gets the complete result and a call after the race succeeds.

##### Cancellation

**Tests**: `PartOfSpeechAdapter_Analyze_CanceledToken_ThrowsOperationCanceledException`,
`PartOfSpeechAdapter_Analyze_CanceledDuringTaggerInitialization_ThrowsOperationCanceledException`,
`PartOfSpeechAdapter_Analyze_CanceledAfterOnlySentenceTagged_Throws`,
`PartOfSpeechAdapter_Analyze_CanceledAfterFinalSentenceTagged_Throws`

The factory cancels the token (initialization case), or the adapter's internal after-sentence observer
cancels it at an exact point (only and final sentence cases), so the checks are exercised
deterministically without timing. The periodic check while tokens are copied is a cheap backstop for
cancellation that arrives from another thread mid-copy; it is verified by review, not by a timing test.

##### Argument validation

**Test**: `PartOfSpeechAdapter_NullArguments_ThrowArgumentNullException`
