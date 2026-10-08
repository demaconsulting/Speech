### SherpaOnnxSynthesisEngine

#### Verification Approach

`SherpaOnnxSynthesisEngine` wraps sherpa-onnx's offline text-to-speech synthesizer as the real
implementation of the Speech library's public `ISynthesisBackend` seam: its constructor loads
the native synthesizer from an `OfflineTtsConfig`, `SampleRate` reports the synthesizer's declared
output rate, `Generate` produces one audio buffer for a given text, speed, and speaker id, and
`Dispose` releases the native synthesizer idempotently. Every one of those operations requires
the real native runtime and a real, installed synthesis model; a mocked native layer would prove
nothing about this unit's purpose. The verification approach is therefore direct, self-skipping
tests against the installed VITS/Piper model, mirroring `SherpaOnnxRecognitionEngineTests`.

`SherpaOnnxSynthesisEngineTests`
(`test/DemaConsulting.Speech.Sherpa.Tests/SynthesisSubsystem/SherpaOnnxSynthesisEngineTests.cs`)
loads the real, installed `SherpaOnnxVitsLibriTtsEnglishSynthesisModel` configuration and drives
the real engine with real native inference, skipping (rather than failing) every test when that
model is not installed in the running environment.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Sherpa.Tests`
- **Dependencies**: The real native sherpa-onnx runtime and the real, installed VITS/Piper
  synthesis model (self-skipping when absent); no network access and no physical audio hardware

#### Acceptance Criteria

The unit is considered verified when construction from a valid configuration reports the model's
declared `SampleRate`, `Generate` returns non-empty audio for non-empty text at the engine's
declared sample rate, `Generate` throws `ArgumentNullException` for null text and
`ObjectDisposedException` after disposal, and a second `Dispose` call is a no-op. Supporting
evidence beyond `SherpaOnnxSynthesisEngineTests`:

- All synthesis policy above the seam is verified in the Speech library against a fake backend
  (see _Speech SynthesisSubsystem Verification_)
- Each synthesis model's `OfflineTtsConfig` construction is verified by its `BuildEngineConfig`
  tests (see _SpeechSherpa ModelManagementSubsystem Verification_)
- Real, non-silent audio through this unit was additionally proven by one-time manual spikes
  recorded in the `SherpaOnnxVitsLibriTtsEnglishSynthesisModel` and `SherpaOnnxKokoroEnglishSynthesisModel`
  verification documents

#### Test Scenarios

##### Constructing the engine reports the model's declared sample rate

**Test**: `SherpaOnnxSynthesisEngine_Construct_ReportsModelSampleRate`

##### Generating non-empty text returns non-empty audio

**Test**: `SherpaOnnxSynthesisEngine_Generate_NonEmptyText_ReturnsNonEmptyAudio`

##### A null text argument throws ArgumentNullException

**Test**: `SherpaOnnxSynthesisEngine_Generate_NullText_ThrowsArgumentNullException`

##### Generating after disposal throws ObjectDisposedException

**Test**: `SherpaOnnxSynthesisEngine_Generate_AfterDispose_ThrowsObjectDisposedException`

##### Calling Dispose twice is idempotent

**Test**: `SherpaOnnxSynthesisEngine_Dispose_CalledTwice_IsIdempotent`
