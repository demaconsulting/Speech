### PreRollAudioCaptureDevice

**Purpose**: Let a push-to-talk host keep the speech that begins just before a button press.
People start speaking as they press; a capture stream opened at the press clips the first
syllable, which for a short answer such as "yes" is often the whole answer. This decorator wraps
any `IAudioCaptureDevice`, keeps it capturing between `Stop()` and the next `Start()`, remembers
the most recent `PreRoll` of audio captured while stopped, and delivers it as the first
`FrameCaptured` block of the next run. It fits the library's single-use recognition sessions
without changing them: the host keeps one instance and binds a new session to it for every press.

The pre-roll belongs in the capture layer rather than in `RecognitionSession` because a session is
single-use and stops its device when it ends, so it cannot hold audio from before it was created;
the device is the only object that lives across presses.

**Data Model**:

- `_inner` (`IAudioCaptureDevice`): the wrapped device; its format is reported unchanged.
- `PreRoll` (`TimeSpan`): `DefaultPreRoll` (300 ms) unless configured; `0` disables the feature;
  at most `MaxPreRoll` (10 s), which bounds memory to about 4 MB of 48 kHz stereo audio.
- `_ring` (`float[]`), `_ringStart`, `_ringCount`: a circular buffer of interleaved samples, sized
  once the underlying format is known to `round(PreRoll × SampleRate)` whole frames. Invariant:
  it holds only audio that no subscriber received.
- `_innerRunning`, `_delivering`, `_disposed` (`bool`): whether the underlying device is
  subscribed and started, whether blocks are forwarded to subscribers (between `Start()` and
  `Stop()`), and whether `Dispose()` ran.
- Two locks: a lifecycle lock serializing `Start()`/`Stop()`/`Dispose()`, and a delivery lock
  guarding `_delivering` and the buffer, held while raising `FrameCaptured`.

**Key Methods**:

- **PreRollAudioCaptureDevice(inner, preRoll = null)**: validates arguments; rejects a
  `WavFileAudioCaptureDevice`, because a file has no audio from before a press and must reach a
  `RecognitionSession` unwrapped, which recognizes a file source in full with backpressure (the
  session identifies a file source by its type, which a decorator would hide); does not touch the
  underlying device.
- **Start()**: on the first run (or every run when the pre-roll is zero) resumes delivery and then
  subscribes to and starts the underlying device; nothing can be buffered at that point, so any
  block the underlying device raises from inside its own `Start()` - or before it has reported its
  format - is delivered rather than lost. On later runs, under the delivery lock, drains the
  buffer, sets `_delivering`, and raises the drained audio (if any) as one `FrameCaptured` block.
  Because live blocks are also forwarded under that lock, the pre-roll block always precedes every
  live block of the run. Starting while already started does nothing.
- **Stop()**: clears `_delivering`. With a non-zero pre-roll the underlying device keeps capturing
  into the buffer; with a zero pre-roll it is unsubscribed and stopped, making the decorator a
  pass-through.
- **Dispose()**: idempotent; clears the buffer and stops the underlying device if running.
- **OnInnerFrameCaptured(...)** (underlying capture thread): forwards the block when delivering
  and someone is subscribed; otherwise appends it to the buffer, overwriting the oldest samples.
  Audio arriving while a stopping session has already unsubscribed is therefore kept as pre-roll
  for the next run, and audio already delivered is never delivered again, so a repeated press
  cannot duplicate words.

**Error Handling**: The constructor throws `ArgumentNullException` for a null device,
`ArgumentException` for a `WavFileAudioCaptureDevice`, and
`ArgumentOutOfRangeException` for a negative pre-roll or one longer than `MaxPreRoll`. `Start()`
throws `ObjectDisposedException` after disposal and `AudioDeviceUnavailableException` when the
underlying device is unavailable; an exception from the underlying `Start()` is propagated after
the subscription and delivery state are rolled back. `Stop()` propagates the underlying `Stop()` failure only when the
pre-roll is zero. `Dispose()` never throws: an `AudioDeviceUnavailableException` from the
underlying `Stop()` is swallowed because the device is already being released. Exceptions from
subscriber handlers are not caught, exactly as for the underlying device.

**Dependencies**: `IAudioCaptureDevice`, `AudioCaptureFrameEventArgs`,
`AudioDeviceUnavailableException`, `WavFileAudioCaptureDevice` (only to reject it); implements
`IAudioCaptureDevice` and `IDisposable`.

**Callers**: Push-to-talk hosts, notably the demo's `RecognitionPanelViewModel`, which keeps one
instance per selected microphone and binds each Start's `RecognitionSession` to it.
