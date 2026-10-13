### DevicesTestCommand

**Purpose**: Implement the `devices test [--device <name>] [--direction input|output]` subcommand:
sanity-check a single audio device without needing any speech model, by playing a short synthesized
tone on a playback device or by recording a short clip from a capture device and reporting basic
capture statistics.

**Data Model**: The unit is a static class with no state. It defines a `DevicesTestOptions` record
holding the parsed device name (nullable; `null` means the platform default device) and the
direction (`Playback` by default). Fixed constants define the test: a 2-second, 440 Hz tone; a
2-second capture clip; and a 5-second playback drain margin.

**Key Methods**:

- `Run(Context)` / `Run(Context, AudioDeviceFactory)`: the dispatch entry point and the testable
  overload over an injected factory. The latter parses the arguments and runs the playback or the
  capture test.
- `ParseArguments`: recognizes exactly one sub-action, the literal first token `test`, leaving room
  for further `devices` sub-actions, then `--device <name>` and `--direction input|output`.
- `ResolveDeviceSelectionOrThrow`: validates a requested device name against the devices the probe
  currently enumerates and returns an `AudioDeviceSelection` (or `null` for the default device). An
  unknown name fails before any device is created, which keeps the failure path testable without a
  real device. `AskCommand` also calls this method for its device options.
- `RequireAvailable` (one overload per device type): returns the device when `IsAvailable` is true
  and otherwise throws, so tests can exercise the unavailable-device failure with a fake device.
- `GenerateToneSamples`: produces the interleaved test-tone samples.
- `PlayTestTone(Context, IAudioPlaybackDevice, TimeSpan?)` and `RecordTestClip`: take the resolved
  device directly so tests can drive them with fake devices.

**Default direction is `output`.** Playing a tone needs no consent beyond running the command;
recording real audio input is more privacy-sensitive, so an operator must opt in with
`--direction input` rather than have it happen as a side effect of a bare `devices test`.

**Output test.** After device resolution and the availability check, `GenerateToneSamples` builds
the tone in the command itself; no speech model is involved. The tone is a sine wave at 440 Hz for
2 seconds, generated at the device's own sample rate and channel count, with a fixed amplitude of
0.2 of full scale: clearly audible but never uncomfortably loud and never clipped, whatever the
device's volume. On a multi-channel device every channel of a frame carries the same value, so a
mono device and each channel of a stereo device play an identical signal. The sample array holds
`sampleRate * 2 * channelCount` interleaved samples.

`PlayTestTone` starts the device, writes the whole tone and then polls `PendingSampleCount` every
50 ms until the queue is empty, because `Write` is fire-and-forget. The wait is bounded by a total
limit measured from before the device is started, so a slow start counts against it: when the caller passes no limit it is the 2-second tone duration plus a fixed 5-second
margin, **7 seconds in all**. If samples are still queued when the limit passes, the command throws
`InvalidOperationException` (a clean message and a non-zero exit code) rather than hanging on a
stalled device. Tests pass a 1 ms limit so the stalled-device path completes instantly. ``Stop` runs
in a `finally` block that also covers `Start`, so a stalled device, or one whose`Start` or write throws, is
stopped before the exception reaches the caller. The
report line states the tone duration and frequency, and the device's sample rate and channel count.

**Input test.** The same name resolution and availability checks apply to the capture probe and
device. `RecordTestClip` subscribes to `FrameCaptured`, starts the device, waits for the duration
(2 seconds from `Run`), and then reports the recorded duration, the sample count and the peak
amplitude (the largest absolute sample value). The recording is neither saved nor played back.
Subscription, start and wait sit inside a `try` whose `finally` block calls `Stop` and unsubscribes
`FrameCaptured`, so the device is always stopped, even if `Start` partially succeeded and then
threw or the wait was interrupted. An empty capture is a failure: if the device delivered no
samples, capture has not been proven, so the command throws `InvalidOperationException` instead of
reporting `Samples captured: 0` with a zero peak as if the test had passed.

Durations in the report are formatted with the invariant culture, so the text is identical on
every machine regardless of the operator's culture.

**Error Handling**: A null `Context`, factory, device or known-device list throws
`ArgumentNullException`. A missing or unknown sub-action, an unrecognized `--direction` value, a
value-less `--device` or `--direction` (including one followed by another option token), and any unsupported argument throw `ArgumentException`. An
unknown device name throws `ArgumentException` naming the device and suggesting `list-devices`. A
device whose `IsAvailable` is false, a stalled playback queue and an empty capture throw
`InvalidOperationException`. `GenerateToneSamples` throws `ArgumentOutOfRangeException` when the
sample rate, channel count, duration or frequency is zero or negative. `Program.Main` reports every
one of these as a one-line message with a non-zero exit code. On a machine with no native audio
backend the probes enumerate nothing, so a named `--device` is reported as an unknown device; the
message does not say that the backend itself is missing. A `devices test` without `--device` on such
a machine reaches the availability check and fails with the "No audio ... device is available"
message.

**Dependencies**: The library's `AudioDeviceFactory`, `IAudioCaptureDeviceProbe`,
`IAudioPlaybackDeviceProbe`, `IAudioCaptureDevice`, `IAudioPlaybackDevice`,
`AudioDeviceDescription` and `AudioDeviceSelection` types; `System.Numerics.Tensors` for the peak
amplitude; the CLI's `Context`.

**Callers**: `CommandDispatch` calls `Run(Context)`. `AskCommand` (in `ConversationCommandSubsystem`)
calls `ResolveDeviceSelectionOrThrow`.
