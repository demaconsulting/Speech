### PartOfSpeechAdapter Verification

#### Verification Approach

`PartOfSpeechAdapter` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/PartOfSpeechAdapterTests.cs` over the real
part-of-speech package, which is the unit's one dependency and is verified separately as an OTS item. The description
of a sentence for the heteronym features is verified against the heteronym context builder, the independent definition
of what one token's description is, with the real tagger. Each adapter is given a test factory that still returns the
real shared tagger (so the process-wide tagger and the other tests are undisturbed) and can run an action, such as a
cancellation, when the tagger is requested or fail its first request, so those points are reached deterministically. The
tests assert results, exceptions and recovery, never how many times a tagger is created or whether the shared adapter is
one instance (design choices recorded in the *PartOfSpeechAdapter Design*).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Isolation**: each test creates its own adapter and test factory

#### Acceptance Criteria

- **Tagged split**: token offsets index the given text and sentences come back in text order; tagging reports the tag
  name, a confidence in range and the known-word flag; splitting without tagging reports untagged tokens, and a text
  without tokens returns no sentences; the description of a tagged sentence has one entry per token in sentence order,
  each equal to what the heteronym context builder gives for that token, also when written into a buffer longer than the
  sentence
- **Resilience**: threads that start at the same moment on a fresh adapter get the same tagged tokens; a factory that
  fails
  once fails only that call and the next call succeeds with the right tags; a factory that returns no tagger is reported
  as `InvalidOperationException`; an already-canceled token throws, and so does cancellation requested during tagger
  initialization or right after the final sentence was tagged; null arguments throw `ArgumentNullException`

#### Test Scenarios

##### Tagged split

**Tests**: `PartOfSpeechAdapter_Analyze_MultiSentenceText_TokenOffsetsIndexSourceInSentenceOrder`,
`PartOfSpeechAdapter_Analyze_TagTrue_ReportsTagConfidenceAndKnownWord`,
`PartOfSpeechAdapter_DescribeSentence_TaggedSentence_DescribesEveryTokenLikeContextCreate`,
`PartOfSpeechAdapter_Analyze_TagFalse_ReportsUntaggedTokens`,
`PartOfSpeechAdapter_Analyze_TextWithoutTokens_ReturnsNoSentences`

A multi-sentence text gives sentences in order whose token offsets index the source text. Tagging reports a tag name, a
confidence in range and the known-word flag; splitting without tagging reports the spans only, none tagged; a text
without tokens returns no sentences even when tagging is requested. The text `The record was broken. She said so.` is
tagged and its first sentence described, which gives a buffer larger than the second sentence, which is then described
into that buffer: the first entries, as many as the second sentence has tokens, each equal the description the context
builder gives for the token's text, span, tag and confidence, so the analyzer and the trainer, which both describe a
sentence this way, see the same sentence. The entries past the sentence are not asserted.

##### Resilience

**Tests**: `PartOfSpeechAdapter_Analyze_ConcurrentFirstUses_ReturnIdenticalResults`,
`PartOfSpeechAdapter_Analyze_FactoryFailsOnce_NextCallSucceeds`,
`PartOfSpeechAdapter_Analyze_FactoryReturnsNull_ThrowsInvalidOperationException`,
`PartOfSpeechAdapter_Analyze_CanceledToken_ThrowsOperationCanceledException`,
`PartOfSpeechAdapter_Analyze_CanceledDuringTaggerInitialization_ThrowsOperationCanceledException`,
`PartOfSpeechAdapter_Analyze_CanceledAfterFinalSentenceTagged_Throws`,
`PartOfSpeechAdapter_NullArguments_ThrowArgumentNullException`

Thirty-two tasks released through one gate so the first uses genuinely race all return the same tagged tokens
(`The/DET cat/NOUN sat/VERB ./PUNCT`). A factory whose first invocation throws makes the first call throw and the second
succeed with the tags of a healthy tagger; a factory that returns null is reported as an invalid operation. The factory
cancels the token (initialization case), or the adapter's internal after-sentence observer cancels it right after the
final of several sentences was tagged, so the checks are exercised deterministically without timing; the periodic check
while tokens are copied is a cheap backstop verified by review. A null factory or a null text throws.
