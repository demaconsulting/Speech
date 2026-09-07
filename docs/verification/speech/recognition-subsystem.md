## RecognitionSubsystem Verification

### Verification Approach

The RecognitionSubsystem is verified entirely through deterministic unit tests that substitute a
fake recognition engine behind the subsystem's internal engine seam and an NSubstitute
`IAudioCaptureDevice` in place of real hardware. This makes composition decisions, audio-format
conversion, the two-thread streaming pipeline, result ordering, fault containment, and honest
degradation fully testable without a downloaded speech model, a microphone, or the
platform-specific native speech-inference runtime.

Determinism is structural rather than timing-based: the recognizer's `Stop()` completes its
internal queue and joins its background consumer, so every result derived from a frame raised
before the call has been delivered by the time it returns. No test polls, sleeps, or waits on a
timeout.

Automated coverage **does not** include recognizing real speech. Proving that real audio from a
real microphone produces correct text through a real model requires both a downloaded production
model (which this phase deliberately does not ship) and audio hardware, so it remains a
manual/local verification activity.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, no downloaded model, no native speech-inference runtime,
  and no physical audio hardware
- **Test doubles**: A fake `IRecognitionEngine`/`IRecognitionEngineFactory` pair, NSubstitute
  capture devices and diagnostics sinks, and a fake recognition model
- **Isolation**: Composition tests create and delete their own scratch installed-model directory

### Acceptance Criteria

A RecognitionSubsystem test run passes when:

- Composition returns a real recognizer only when the model is installed, declares the
  recognition role, the capture device is available, and the engine loads
- Every other composition outcome returns the honest unavailable recognizer without throwing
- Captured audio is downmixed and resampled to the model's declared `AudioFormat`, with
  above-target-Nyquist energy attenuated before downsampling decimation
- Every recognition result is delivered, in order, with its provisional/final flag preserved
- Start/stop/dispose behave idempotently, drain queued audio, and release engine resources
- Engine faults and throwing host handlers are reported and contained rather than propagated
- The unavailable recognizer stays honest and safe to hold, subscribe to, and dispose
- The automated verification boundary remains honest about the absence of real-speech coverage

### Test Scenarios

#### Composition: Real Recognizer for an Installed Model and Available Device

**Tests**: `SpeechRecognizerFactory_Create_ModelInstalledAndDeviceAvailable_ReturnsRealRecognizer`,
`SpeechRecognizerFactory_Create_WithStoreModelInstalledAndDeviceAvailable_ReturnsRealRecognizer`

Verifies that an installed recognition model plus an available capture device composes a real
recognizer wired to the injected engine factory, with the installed-model directory passed
through unchanged, whether that directory is supplied directly as a `string` or resolved from a
`SpeechModelStore`.

#### Composition: Honest Fallback for Every Unavailable State

**Tests**: `SpeechRecognizerFactory_Create_ModelNotInstalled_ReturnsUnavailableRecognizer`,
`SpeechRecognizerFactory_Create_CaptureDeviceUnavailable_ReturnsUnavailableRecognizer`,
`SpeechRecognizerFactory_Create_ModelRoleIsNotRecognition_ReturnsUnavailableRecognizer`,
`SpeechRecognizerFactory_Create_EngineLoadFails_ReturnsUnavailableRecognizerAndDoesNotThrow`,
`SpeechRecognizerFactory_Create_WithStoreModelNotInstalled_ReturnsUnavailableRecognizer`

Verifies that a missing model, an unavailable device, a wrong-role model, and a failed engine
load all degrade to the shared unavailable recognizer without throwing, and that no engine is
loaded when an earlier check already failed.

#### Composition: Null Arguments Are Programming Errors

**Tests**: `SpeechRecognizerFactory_Create_NullModel_ThrowsArgumentNullException`,
`SpeechRecognizerFactory_Create_NullCaptureDevice_ThrowsArgumentNullException`,
`SpeechRecognizerFactory_Create_WithStoreNullModel_ThrowsArgumentNullException`,
`SpeechRecognizerFactory_Create_WithStoreNullStore_ThrowsArgumentNullException`,
`SpeechRecognizerFactory_Create_WithStoreNullCaptureDevice_ThrowsArgumentNullException`

Verifies that a null model, capture device, or store throws, distinguishing a programming error
from an ordinary machine state.

#### Pipeline: Capture Format Conversion

**Tests**: `SherpaOnnxSpeechRecognizer_FrameCaptured_StereoAtHigherRate_FeedsResampledMonoToEngine`,
`SherpaOnnxSpeechRecognizer_Constructor_DeviceReportsUnusableFormat_FallsBackToPassThrough`,
`SherpaOnnxSpeechRecognizer_FrameCaptured_EmptyBlock_IsIgnored`

Verifies that a stereo block at a higher rate reaches the engine as mono at the model's declared
rate, that a device reporting an unusable format degrades to pass-through with a warning rather
than throwing, and that an empty block is ignored.

#### Pipeline: Result Delivery and Ordering

**Tests**: `SherpaOnnxSpeechRecognizer_FrameCaptured_EngineDecodesResults_RaisesResultReceivedInOrder`

Verifies that every result the engine decodes is raised in order with its provisional/final flag
preserved.

#### Pipeline: Text Normalization

**Tests**: `SherpaOnnxSpeechRecognizer_Constructor_NullModel_ThrowsArgumentNullException`,
`SherpaOnnxSpeechRecognizer_FrameCaptured_FinalResult_AppliesModelNormalizeTextWithIsFinalTrue`,
`SherpaOnnxSpeechRecognizer_FrameCaptured_ProvisionalResult_AppliesModelNormalizeTextWithIsFinalFalse`

Verifies that a null `model` constructor argument throws `ArgumentNullException`, and that every
raised result's text has passed through the owning model's `IRecognitionModel.NormalizeText`,
called with `isFinal: true` for final results and `isFinal: false` for provisional results - the
model's returned text, not the engine's raw text, is what `ResultReceived` carries.

#### Pipeline: Lifecycle and Draining

**Tests**: `SherpaOnnxSpeechRecognizer_Start_Always_SubscribesAndStartsCaptureDevice`,
`SherpaOnnxSpeechRecognizer_Start_AlreadyRunning_IsNoOp`,
`SherpaOnnxSpeechRecognizer_Stop_WhileRunning_UnsubscribesAndStopsCaptureDevice`,
`SherpaOnnxSpeechRecognizer_Stop_NotRunning_IsNoOp`,
`SherpaOnnxSpeechRecognizer_Dispose_CalledTwice_StopsAndDisposesEngineOnce`,
`SherpaOnnxSpeechRecognizer_Start_AfterDispose_ThrowsObjectDisposedException`

Verifies that starting subscribes and starts capture, repeated starts and idle stops are no-ops,
stopping unsubscribes and stops the device so post-stop frames never reach the engine, disposal
stops once and releases the engine once, and starting after disposal is rejected.

#### Pipeline: Fault Containment

**Tests**: `SherpaOnnxSpeechRecognizer_FrameCaptured_EngineThrows_ReportsFaultAndKeepsRunning`,
`SherpaOnnxSpeechRecognizer_ResultReceived_HandlerThrows_ReportsFaultAndDoesNotRethrow`,
`SherpaOnnxSpeechRecognizer_Start_CaptureDeviceFails_ThrowsSpeechRecognizerUnavailableException`

Verifies that engine faults and throwing host handlers are reported through the diagnostics sink
and never escape into the capture path, while a capture device that fails on first use surfaces
the documented recognizer exception with the device's failure as its inner exception.

#### Audio Conversion: Downmix, Rate Conversion, and Boundaries

**Tests**: `AudioFrameResampler_DownmixToMono_StereoInput_AveragesChannelsPerFrame`,
`AudioFrameResampler_DownmixToMono_TrailingPartialFrame_DiscardsPartialFrame`,
`AudioFrameResampler_DownmixToMono_SingleChannel_ReturnsSamplesUnchanged`,
`AudioFrameResampler_DownmixToMono_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException`,
`AudioFrameResampler_Resample_Downsampling_ProducesProportionallyFewerSamples`,
`AudioFrameResampler_Resample_Upsampling_LinearlyInterpolatesBetweenSamples`,
`AudioFrameResampler_Resample_SingleSample_ClampsToThatSample`,
`AudioFrameResampler_Resample_OutputRoundsToZeroSamples_ReturnsEmptyResult`,
`AudioFrameResampler_Resample_AboveTargetNyquistTone_IsAttenuated`,
`AudioFrameResampler_Resample_ShortInputDuringDownsampling_DoesNotThrow`,
`AudioFrameResampler_Convert_MonoAtTargetRate_ReturnsSamplesUnchanged`,
`AudioFrameResampler_Convert_EmptyInput_ReturnsEmptyResult`,
`AudioFrameResampler_Convert_StereoAtHigherRate_DownmixesAndResamples`,
`AudioFrameResampler_Constructor_NonPositiveSourceRate_ThrowsArgumentOutOfRangeException`,
`AudioFrameResampler_Constructor_NonPositiveTargetRate_ThrowsArgumentOutOfRangeException`,
`AudioFrameResampler_Constructor_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException`

Verifies channel averaging, partial-frame discard, exact identity pass-through, proportional
up/down conversion with linear interpolation, anti-aliased downsampling, short-input safety,
empty/single-sample/rounds-to-empty boundaries, and rejection of non-positive rates and channel
counts.

#### Unavailable Fallback: Honest Degradation

**Tests**: `UnavailableSpeechRecognizer_IsAvailable_Read_ReturnsFalse`,
`UnavailableSpeechRecognizer_Start_Always_ThrowsSpeechRecognizerUnavailableException`,
`UnavailableSpeechRecognizer_Stop_Always_ThrowsSpeechRecognizerUnavailableException`,
`UnavailableSpeechRecognizer_SubscriptionAndDispose_Always_AreSafeNoOps`,
`SpeechRecognizerUnavailableException_Constructor_WithMessage_ExposesMessage`,
`SpeechRecognizerUnavailableException_Constructor_WithInnerException_ExposesBoth`,
`SpeechRecognizerUnavailableException_Constructor_Default_HasNonEmptyMessage`

Verifies that the shared fallback stays honest and safe to hold, subscribe to, and dispose, that
operational misuse throws the documented exception, and that the exception conforms to the
standard three-constructor pattern.
