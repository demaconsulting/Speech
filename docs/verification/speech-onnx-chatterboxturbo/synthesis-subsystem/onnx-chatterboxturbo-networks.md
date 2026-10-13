### OnnxChatterboxTurboNetworks Verification

#### Verification Approach

Verified by `OnnxChatterboxTurboNetworksTests.cs` against synthetic ONNX graphs written by the test
project's `OnnxModelWriter` and `ChatterboxTurboGraphFixture`. A static session hook lets tests
observe the sessions created, for the disposal checks. The disposed state of an ONNX Runtime session has no
public accessor and a call on a disposed session is unsafe, so the tests read its private disposed flag.
These tests run in a non-parallel collection.

#### Test Environment

- **Framework**: xUnit v3; project `test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests`
- **Isolation**: scratch directories; no network I/O; no real model
- **Collection**: the tests belong to a non-parallel xUnit collection because the session hook is
  process-wide
- **Reflection**: the disposed flag of an ONNX Runtime session is read by reflection (the private
  `InferenceSession._disposed` field), since it has no public accessor. This read must be revisited
  whenever ONNX Runtime is upgraded, because the field may be renamed or removed (the test fails loudly if
  it disappears)
- **Known limits**: the dispose-all behavior is verified through the internal `DisposeAll` helper with
  recording disposables (one throws, the others must still be disposed) because a real `InferenceSession`
  cannot be made to throw on dispose; "no session leak" beyond the disposed flags of the observed sessions is
  not verified; the multi-assertion test for use after dispose is kept whole because it asserts one
  lifecycle contract over one expensive session set

#### Acceptance Criteria

Each of the four graph calls returns the documented data, including half-precision encoder, embedder,
decoder and language-model values; invalid step arguments are rejected; the intra-op thread count is
validated and exposed; a missing graph throws `FileNotFoundException`; sessions are disposed on dispose and
on construction failure (and every session is disposed even when one dispose throws), and use after dispose
is rejected with `ObjectDisposedException`.

The thread count applied inside ONNX Runtime cannot be observed from outside, so the test checks the unit's
own `IntraOpThreads` property and the rejection of a non-positive count only.

#### Test Scenarios

##### Encode binds named outputs

**Test**: `OnnxChatterboxTurboNetworks_Encode_NamedOutputs_ReturnsConditioning`

Asserts the element values of the conditioning embeddings, prompt tokens, speaker embeddings and speaker
features.

##### Encode binds unnamed outputs positionally

**Test**: `OnnxChatterboxTurboNetworks_Encode_UnnamedFourOutputs_BindsPositionally`

##### Embed returns table rows

**Test**: `OnnxChatterboxTurboNetworks_Embed_TokenIds_ReturnsTableRows`

##### Embed of zero ids returns empty

**Test**: `OnnxChatterboxTurboNetworks_Embed_EmptyTokenIds_ReturnsEmpty`

##### ReadFloats handles float32 and float16

**Test**: `OnnxChatterboxTurboNetworks_ReadFloats_HandlesFloat32AndFloat16`

##### Step reuses the cache and returns the last logits

**Test**: `OnnxChatterboxTurboNetworks_Step_TwoSteps_ReusesCacheAndReturnsLastLogits`

##### Step converts half-precision embeddings and logits

**Test**: `OnnxChatterboxTurboNetworks_Step_HalfEmbeddingsAndLogits_AreConverted`

##### Step rejects invalid arguments

**Tests**: `OnnxChatterboxTurboNetworks_Step_WrongEmbeddingLength_ThrowsArgumentException`,
`OnnxChatterboxTurboNetworks_Step_AttentionShorterThanSequence_ThrowsArgumentOutOfRangeException`,
`OnnxChatterboxTurboNetworks_Step_NonPositiveSequenceLength_ThrowsArgumentOutOfRangeException` (0 and -1),
`OnnxChatterboxTurboNetworks_Step_NullEmbeddings_ThrowsArgumentNullException` and
`OnnxChatterboxTurboNetworks_Step_NegativeFirstPosition_ThrowsArgumentOutOfRangeException`

##### Half-precision encoder, embedder and decoder outputs are converted

**Test**: `OnnxChatterboxTurboNetworks_HalfAuxiliaryOutputs_AreConvertedToFloats`

Graphs that emit float16 values (via Cast) yield the same float values as the float32 graphs, within half precision.

##### Null arguments are rejected

**Test**: `OnnxChatterboxTurboNetworks_NullArguments_Throw`

##### Decode returns the waveform

**Tests**: `OnnxChatterboxTurboNetworks_Decode_SpeechTokens_ReturnsWaveform` (the synthetic decoder adds the
speaker sums to each token, giving `[36, 37, 38]`),
`OnnxChatterboxTurboNetworks_Decode_ChangedConditioning_ChangesWaveform` (a different conditioning changes
the waveform, proving the speaker inputs reach the decoder) and
`OnnxChatterboxTurboNetworks_Decode_EmptyTokens_ThrowsArgumentException`

##### Thread count is applied and validated

**Tests**: `OnnxChatterboxTurboNetworks_Constructor_ThreadCount_IsApplied` (the `IntraOpThreads` property) and
`OnnxChatterboxTurboNetworks_Constructor_NonPositiveThreadCount_ThrowsArgumentOutOfRangeException` (0 and -1)

##### An invalid language model disposes sessions

**Test**: `OnnxChatterboxTurboNetworks_Constructor_InvalidLanguageModel_DisposesSessionsAndThrows`

##### Unrecognized encoder outputs are rejected

**Test**: `OnnxChatterboxTurboNetworks_Constructor_UnrecognizedEncoderOutputs_ThrowsInvalidOperationException`

##### A missing graph throws and disposes earlier sessions

**Test**: `OnnxChatterboxTurboNetworks_Constructor_MissingGraph_DisposesEarlierSessionsAndThrows`
(asserts `FileNotFoundException`)

##### Dispose disposes all sessions

**Tests**: `OnnxChatterboxTurboNetworks_Dispose_DisposesAllSessions` and
`OnnxChatterboxTurboNetworks_Dispose_Twice_IsNoOp`

##### Dispose continues past a failing dispose

**Tests**: `OnnxChatterboxTurboNetworks_DisposeAll_FirstDisposeThrows_StillDisposesLaterItems`,
`OnnxChatterboxTurboNetworks_DisposeAll_SeveralThrow_ReportsAllFailures` and
`OnnxChatterboxTurboNetworks_DisposeAll_NoFailures_DisposesEveryItem`

The first item's dispose throws and the later items are still disposed; several failures are reported
together; with no failure every item is disposed.

##### Use after dispose is rejected

**Test**: `OnnxChatterboxTurboNetworks_AfterDispose_RejectsUse`

Encode, embed, decode and begin-generation throw `ObjectDisposedException` after dispose.

##### Step after state or networks dispose is rejected

**Tests**: `OnnxChatterboxTurboNetworks_Step_AfterStateDispose_ThrowsObjectDisposedException`,
`OnnxChatterboxTurboNetworks_Step_AfterNetworksDispose_ThrowsObjectDisposedException` and
`OnnxChatterboxTurboNetworks_StateDispose_AfterNetworksDispose_IsSafe`

A language-model step throws `ObjectDisposedException` both after the state itself is disposed and after the
networks that began it are disposed, without reaching the native session; disposing a state after its
networks does not throw.
