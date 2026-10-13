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
- **Reflection**: the disposed flag of an ONNX Runtime session is read by reflection, since it has no public accessor
- **Known limits**: "no session leak" beyond the disposed flags of the observed sessions is not
  verified; the multi-assertion tests (thread count, use after dispose) are kept whole because each
  asserts one lifecycle contract over one expensive session set

#### Acceptance Criteria

Each of the four graph calls returns the documented data, including half-precision encoder, embedder,
decoder and language-model values; invalid step arguments are rejected; the intra-op thread count is
validated and exposed; a missing graph throws `FileNotFoundException`; sessions are disposed on dispose and
on construction failure, and use after dispose is rejected with `ObjectDisposedException`.

The thread count applied inside ONNX Runtime cannot be observed from outside, so the test checks the unit's
own `IntraOpThreads` property and the rejection of a non-positive count only.

#### Test Scenarios

##### Encode binds named outputs

**Test**: `OnnxChatterboxTurboNetworks_Encode_NamedOutputs_ReturnsConditioning`

##### Encode binds unnamed outputs positionally

**Test**: `OnnxChatterboxTurboNetworks_Encode_UnnamedFourOutputs_BindsPositionally`

##### Embed returns table rows

**Test**: `OnnxChatterboxTurboNetworks_Embed_TokenIds_ReturnsTableRows`

##### ReadFloats handles float32 and float16

**Test**: `OnnxChatterboxTurboNetworks_ReadFloats_HandlesFloat32AndFloat16`

##### Step reuses the cache and returns the last logits

**Test**: `OnnxChatterboxTurboNetworks_Step_TwoSteps_ReusesCacheAndReturnsLastLogits`

##### Step converts half-precision embeddings and logits

**Test**: `OnnxChatterboxTurboNetworks_Step_HalfEmbeddingsAndLogits_AreConverted`

##### Step rejects invalid arguments

**Tests**: `OnnxChatterboxTurboNetworks_Step_WrongEmbeddingLength_ThrowsArgumentException`,
`OnnxChatterboxTurboNetworks_Step_AttentionShorterThanSequence_ThrowsArgumentOutOfRangeException`,
`OnnxChatterboxTurboNetworks_Step_ZeroSequenceLength_ThrowsArgumentOutOfRangeException` and
`OnnxChatterboxTurboNetworks_Step_NullEmbeddings_ThrowsArgumentNullException`

##### Half-precision encoder, embedder and decoder outputs are converted

**Test**: `OnnxChatterboxTurboNetworks_HalfAuxiliaryOutputs_AreConvertedToFloats`

Graphs that emit float16 values (via Cast) yield the same float values as the float32 graphs, within half precision.

##### Null arguments are rejected

**Test**: `OnnxChatterboxTurboNetworks_NullArguments_Throw`

##### Decode returns the waveform

**Test**: `OnnxChatterboxTurboNetworks_Decode_SpeechTokens_ReturnsWaveform`

##### Thread count is applied and validated

**Test**: `OnnxChatterboxTurboNetworks_Constructor_ThreadCount_IsAppliedAndValidated`

##### An invalid language model disposes sessions

**Test**: `OnnxChatterboxTurboNetworks_Constructor_InvalidLanguageModel_DisposesSessionsAndThrows`

##### Unrecognized encoder outputs are rejected

**Test**: `OnnxChatterboxTurboNetworks_Constructor_UnrecognizedEncoderOutputs_ThrowsInvalidOperationException`

##### A missing graph throws and disposes earlier sessions

**Test**: `OnnxChatterboxTurboNetworks_Constructor_MissingGraph_DisposesEarlierSessionsAndThrows`
(asserts `FileNotFoundException`)

##### Dispose disposes all sessions

**Test**: `OnnxChatterboxTurboNetworks_Dispose_DisposesAllSessions`

##### Use after dispose is rejected

**Test**: `OnnxChatterboxTurboNetworks_AfterDispose_RejectsUse`

Encode, embed, decode and begin-generation throw `ObjectDisposedException` after dispose; a second dispose is harmless.

##### Step after state or networks dispose is rejected

**Tests**: `OnnxChatterboxTurboNetworks_Step_AfterStateDispose_ThrowsObjectDisposedException` and
`OnnxChatterboxTurboNetworks_Step_AfterNetworksDispose_ThrowsObjectDisposedException`

A language-model step throws `ObjectDisposedException` both after the state itself is disposed and after the
networks that began it are disposed, without reaching the native session.
