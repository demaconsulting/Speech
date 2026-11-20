### OnnxPiperSynthesisEngine Verification

#### Verification Approach

`OnnxPiperSynthesisEngine` is verified by unit tests in
`test/DemaConsulting.Speech.Onnx.Piper.Tests/SynthesisSubsystem/OnnxPiperSynthesisEngineTests.cs` against a tiny
stand-in ONNX graph (built by `PiperTestFixtures` with a minimal protobuf writer) that has the four inputs of the Piper
graph and whose output echoes what the engine fed it: the ids, the noise scale, the length scale, the noise width, the
speaker and the input length. The framing, the scales, the speed and the speaker are therefore observed exactly. The
disposal is observed through `DisposalTrackingInferenceSession`, a session subclass that records its disposal through
the public `Dispose(bool)` override. The voice configuration is the trimmed copy of the real one and the pipeline is the
real one.

One further test runs the real installed voice,
`OnnxPiperSynthesisEngine_Generate_RealInstalledVoice_ProducesAudibleAudio` (audible, 22,050 Hz, a faster speed clearly
shorter, no unknown symbol). It skips itself when the voice is not installed on the machine and is therefore not linked
to a requirement.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: each test class instance writes the stand-in graph to its own scratch directory; no network

#### Acceptance Criteria

For the first sentence of the renderer's Piper golden the graph is fed the begin id 1 and the pad id 0, each pinned id
followed by the pad id 0, the end id 2, the length equal to the id count, the scales 0.333, 1.0 and 0.333 and speaker 0;
the speeds 2, 0.5 and 1 give the length scales 0.5, 2.0 and 1.0 and the speeds 0, -1, NaN and infinity give 1.0; the
speakers 0, 5, 903, 5000 and -3 reach the graph as 0, 5, 903, 903 and 0; an empty, a blank and an emoji text give empty
audio at 22,050 Hz; null arguments are rejected; the annotated heteronym sentence feeds other ids than the plain one;
forty sentences give an audio of exactly the length that their utterances predict, with at least three utterances; with
the symbol `ɑ` removed from the table the count and the list report exactly that symbol for `hot` and nothing for
`Hello there.`; the probe does not throw on a runnable session; and a second disposal does not throw, the session was
disposed once and a synthesis after disposal throws `ObjectDisposedException`; a disposal that overlaps a running
synthesis waits for it, and concurrent disposals and syntheses give only audio or `ObjectDisposedException`.

#### Test Scenarios

##### Inputs to the graph

**Tests**: `OnnxPiperSynthesisEngine_Generate_Sentence_FeedsFramedIdsVoiceScalesAndSpeaker`,
`OnnxPiperSynthesisEngine_Generate_Speed_ScalesTheLengthScale`,
`OnnxPiperSynthesisEngine_Generate_Speaker_ReachesTheModelWithinRange`,
`OnnxPiperSynthesisEngine_GenerateAnnotated_HeteronymSentence_FeedsTheChosenReadings`,
`OnnxPiperSynthesisEngine_Constructor_NullArgument_Throws`

##### Text length

**Tests**: `OnnxPiperSynthesisEngine_Generate_VeryLongText_IsSpokenInPartsAndJoined`,
`OnnxPiperSynthesisEngine_Generate_TextWithNothingToSay_ReturnsEmptyAudio`

##### Unknown symbols

**Test**: `OnnxPiperSynthesisEngine_Generate_SymbolOutsideTheTable_IsCountedAndReported`

##### Probe and disposal

**Tests**: `OnnxPiperSynthesisEngine_RunProbeInference_RunnableSession_DoesNotThrow`,
`OnnxPiperSynthesisEngine_Dispose_Engine_ReleasesTheSessionOnceAndEndsUse`,
`OnnxPiperSynthesisEngine_Dispose_WhileSynthesisRuns_WaitsForTheRunAndReleasesTheSessionOnce`,
`OnnxPiperSynthesisEngine_Dispose_ConcurrentCallers_ReleaseTheSessionOnce`,
`OnnxPiperSynthesisEngine_Generate_ConcurrentWithDispose_CompletesOrThrowsObjectDisposed`

The overlap test holds one synthesis inside the engine (through an annotation list, a public input of the engine that
blocks until released), starts two disposals, waits until a new synthesis is refused (which shows the disposal has
begun), and checks that the session has not been disposed yet, that the held synthesis still returns audio once
released, and that the session was disposed exactly once. The race test runs eight workers of 25 syntheses against one
disposal and checks that every outcome is audio or `ObjectDisposedException`. The session is counted through
`DisposalTrackingInferenceSession.DisposeCount`.
