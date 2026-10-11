### OnnxNemotronRecognitionEngine Verification

#### Verification Approach

Verified by deterministic unit tests that drive the engine with managed fakes of the encoder and
prediction network (`Fakes.cs`), so chunking, result reporting, endpointing, and lifetime
behavior are tested without ONNX Runtime or model files.

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`OnnxNemotronRecognitionEngineTests.cs`).

#### Acceptance Criteria

The engine buffers audio without decoding; reports provisional text only on change and only for
complete chunks; flushes to a final result with a zero-padded final chunk and tail chunk; pads a partial
chunk with dithered silence once the limiter's quiet run reaches 6400 samples, so text is reported
before the endpoint; endpoints
on input-clock quiet after speech (resetting model state) but never on silence alone; finalizes
after three empty chunks keeping state; and resets, disposes, and validates arguments as
documented. The ONNX-backed `NemotronEncoder` is verified separately in `NemotronEncoderTests.cs`
against a tiny synthetic ONNX model with the real encoder's tensor contract (built in
`NemotronEncoderFixture.cs`), covering tensor names, cache feedback across calls, output frame
count, reset, the probe inference, and disposal without the real model files.

#### Test Scenarios

##### Speech chunk reports provisional text

**Test**: `TryDecode_SpeechChunk_ReportsProvisionalText`

##### Unchanged text reports nothing

**Test**: `TryDecode_NoNewTokens_ReportsNothing`

##### Less than a chunk reports nothing

**Test**: `TryDecode_LessThanChunk_ReportsNothing`

##### Empty input is ignored

**Test**: `AcceptSamples_Empty_IsIgnored`

##### Flush returns final text

**Test**: `TryFlush_PartialAudio_ReturnsFinalText`

##### Flush without audio returns false

**Test**: `TryFlush_NoAudio_ReturnsFalse`

##### Flush with audio but no tokens returns false

**Test**: `TryFlush_AudioWithoutTokens_ReturnsFalse`

##### Flush without tail chunks decodes only the audio

**Test**: `TryFlush_NoTailChunks_DecodesOnlyAudio`

##### Quiet after speech endpoints and resets

**Test**: `TryDecode_QuietAfterSpeech_EndpointsAndResets`

##### Quiet after short speech reports before the endpoint

**Test**: `TryDecode_QuietAfterShortSpeech_ReportsBeforeEndpoint`

##### Padding follows the configured limiter cap

**Test**: `TryDecode_CustomLimiterCap_PadsAtThatCap`

##### Silence alone never endpoints

**Test**: `TryDecode_OnlySilence_NeverEndpoints`

##### Empty chunks after text finalize keeping state

**Test**: `TryDecode_EmptyChunksAfterText_FinalizesKeepingState`

##### Reset clears state

**Test**: `Reset_ClearsState`

##### Dispose disposes collaborators and blocks use

**Test**: `Dispose_DisposesCollaboratorsAndBlocksUse`

##### Null constructor arguments throw

**Test**: `Constructor_NullArguments_Throw`

##### Encoder first chunk returns frames with the language

**Test**: `Encode_FirstChunk_ReturnsFramesWithLanguage`

##### Encoder second chunk uses the previous caches

**Test**: `Encode_SecondChunk_UsesPreviousCaches`

##### Encoder reset restarts the caches

**Test**: `Reset_AfterEncoding_RestartsCaches`

##### Encoder probe inference leaves the session usable

**Test**: `RunProbeInference_ValidSession_LeavesSessionUsable`

##### Encoder rejects use after disposal

**Test**: `Encode_AfterDispose_Throws`

##### Encoder null session throws

**Test**: `Constructor_NullSession_Throws`
