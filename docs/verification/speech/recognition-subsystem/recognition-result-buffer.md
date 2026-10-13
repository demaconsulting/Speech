### RecognitionResultBuffer

#### Verification Approach

The buffer is verified through `RecognitionSession.GetResultsAsync` with a deterministic fake
backend that emits many provisional results and then a final result, and a deliberately slow
consumer. No sleeps are used for ordinary behavior; the consumer is gated by the test.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Dependencies**: No external services, no downloaded model, no native runtime, and no audio
  hardware
- **Test doubles**: Fake `IRecognitionBackend` plus NSubstitute capture devices

#### Acceptance Criteria

The unit is considered verified when a slow consumer receives coalesced provisional results
while every final result is still delivered under the byte cap, and the unit's own tests confirm
that a final result supersedes the provisional one for the same utterance while results for
different utterances are all delivered.

#### Test Scenarios

##### A final result supersedes the provisional result for the same utterance

**Test**: `RecognitionResultBuffer_AddResult_FinalAfterProvisional_SupersedesProvisional`

##### Provisional then final results for different utterances are both delivered

**Test**: `RecognitionResultBuffer_AddResult_ProvisionalThenFinalForDifferentUtterances_DeliversBoth`

##### Provisional results are coalesced for a slow consumer

**Test**: `RecognitionSession_GetResultsAsync_SlowConsumer_CoalescesProvisionalResults`

##### Final results are never dropped under the byte cap

**Test**: `RecognitionSession_GetResultsAsync_SlowConsumer_NeverDropsFinalResultsUnderByteCap`
