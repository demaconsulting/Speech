## SpeechDemo RecognitionPanelSubsystem Design

![RecognitionPanelSubsystem Structure](RecognitionPanelSubsystemView.svg)

### Overview

The RecognitionPanelSubsystem provides the demo's speech-to-text panel. It contains the
following units, each documented in its own file:

- **RecognizerSessionFactory** (with `IRecognizerSessionFactory`): the demo-owned
  recognizer-engine composition seam and its real implementation over the library's
  `SpeechModelStore` and `SpeechRecognizerFactory` — see _RecognizerSessionFactory Design_
  (`recognition-panel-subsystem/recognizer-session-factory.md`)
- **RecognitionPanelViewModel**: the panel's presentation state — the installed recognition
  models, the async Start/Stop streaming lifecycle over an `ISpeechRecognizerEngine` and an
  `IRecognitionSession`, and the progressive partial-then-final transcript — see
  _RecognitionPanelViewModel Design_ (`recognition-panel-subsystem/recognition-panel-view-model.md`)
- **CaptureDebugRecorder** / **WavFileWriter**: internal, opt-in diagnostic
  instrumentation (off by default) that records raw captured audio to a `.wav` file for offline
  investigation of a reported live-microphone bug; see "Capture Debug Recording" below

### Interfaces

The subsystem exposes one presentation surface, `RecognitionPanelViewModel`, consumed by
`RecognitionPanelView.axaml`; the ShellSubsystem (`MainWindowViewModel`) holds and hosts it, and
`App` constructs it. It consumes:

- `IModelCatalogService` and `IAudioDeviceService` — the shell-provided demo services for
  listing installed models and creating capture devices
- the shared `DeviceSelectionViewModel` — the capture-device picker's presentation state
- `IRecognizerSessionFactory` — the demo-owned recognizer-engine composition seam

### Design

`RecognitionPanelViewModel` drives the streaming lifecycle and obtains every engine through
`IRecognizerSessionFactory`, never through the library's recognition concretes directly. The
recognition panel does not embed the `ModelSettingsSubsystem` panel: `IRecognitionSession`'s
contract accepts no per-call parameter bag, so there is nothing for a settings panel to feed (see
_SpeechDemo ModelSettingsSubsystem Design_). Only the engine is composed through the seam; a
capture device is bound per run, through `ISpeechRecognizerEngine.CreateSessionAsync` directly
against the returned engine.

**Deterministic operation without a model or runtime** (`SpeechDemo-Recognition-SessionSeam`): the
observable guarantee is realized by the injectable `IRecognizerSessionFactory` seam. The library's
recognizer factory is a static method and so cannot be substituted in a panel unit test; the
demo-owned seam accepts the common `ISpeechModel` contract and returns an `ISpeechRecognizerEngine`,
so tests (and an installation with no downloaded model) can drive Start/Stop with a deterministic
engine or with the library's unavailable engine, without a native runtime.

### Interactions with Other Units

`RecognitionPanelViewModel` calls `IRecognizerSessionFactory.LoadAsync` (implemented by
`RecognizerSessionFactory`) once per selected model and reuses the returned engine across Start/Stop
cycles. A model of the wrong role yields the library's unavailable engine from the seam, which the
view model reports as an explanatory error state rather than an exception. The view model attaches
`CaptureDebugRecorder` to the capture device when diagnostic recording is enabled.

#### Capture Debug Recording (diagnostic instrumentation)

**`CaptureDebugRecorder`** and **`WavFileWriter`** are internal, opt-in diagnostic
instrumentation for investigating live-microphone issues such as a long mid-sentence pause
dropping the first word(s) spoken right after the pause. They are not part of this
subsystem's public presentation surface and are off by default. When the
`DEMASPEECH_CAPTURE_DEBUG_DIR` environment variable names a writable directory,
`RecognitionPanelViewModel`'s Start algorithm calls `CaptureDebugRecorder.TryStart(captureDevice)`
to attach a second, entirely independent subscriber to the same `IAudioCaptureDevice.FrameCaptured`
event the real recognizer subscribes to, writing every captured frame - in the capture device's
own native sample rate and channel count, before any resampling the recognizer applies - to a
session-scoped `.wav` file via `WavFileWriter`, on a dedicated writer thread fed by a
producer/consumer queue so a slow or failing disk can never block or throw into the audio capture
callback. The queue is bounded (2000 frames by default, tens of seconds of typical capture
frames): the capture callback never waits, and when the queue is full the newest frame is
dropped and counted, with the total reported to the console when the recording is finalized.
Memory use is therefore bounded and a stalled writer produces a gap in the recording instead of
affecting listening. The Stop algorithm finalizes (disposes) any active recording before clearing the
captured UI context. A recorder that fails to start (for example, an inaccessible directory)
logs the failure to the console and live recognition proceeds completely unaffected.

**Containment of diagnostic failures.** The recorder must never affect listening:

- _Coordinated disposal and callback admission_: disposal is claimed atomically (an interlocked
  flag), and the frame callback checks it and catches `InvalidOperationException` and
  `ObjectDisposedException` from the queue, so a frame that is already in flight when the recorder
  is disposed is silently discarded rather than throwing into the audio callback. Disposal
  unsubscribes from `FrameCaptured`, completes the queue, waits a bounded time for the writer
  thread, and finalizes under a writer lock shared with the writer thread so the file is finalized
  exactly once.
- _Writer failure containment_: the writer loop catches `IOException`,
  `UnauthorizedAccessException`, `ObjectDisposedException`, `NotSupportedException`,
  `InvalidOperationException`, and `ArgumentException`, logs the failure to the console, and stops
  accepting further frames (the queue is completed so producers are discarded) instead of retrying
  against a failed disk; recognition continues unaffected.
- _Finalization failure containment_: finalizing the WAV file (flush, header patch, close) can
  itself throw; that failure is caught and logged by both the writer thread and disposal, so it can
  neither escape the writer thread and terminate the process nor propagate out of `Dispose()`.
- _Backpressure_: the bounded queue never blocks the producer; overflow frames are dropped and
  counted rather than queued, so a stalled writer can neither delay capture nor grow memory.
- _Start failure_: an inaccessible directory or invalid path is caught in `TryStart`, logged, and
  results in no recorder.
