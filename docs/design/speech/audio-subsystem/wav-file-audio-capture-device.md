### WavFileAudioCaptureDevice

**Purpose**: Read a mono, 16-bit PCM `.wav` file and deliver it as normalized capture frames
instead of capturing from real microphone hardware, so any host application - not only the future
`recognize --input` CLI command - can drive speech recognition from a pre-recorded file
deterministically.

**Data Model**: Holds the constructor-supplied file path and per-frame sample count. Resolved
`ChannelCount`/`SampleRate` are `0` until `Start()` has successfully parsed the file's `fmt`
chunk, after which `ChannelCount` is always `1` (only mono files are supported) and `SampleRate`
reflects the file's declared rate. A `volatile` stop-requested flag, set by `Stop()` and read by
`Start()`, allows `Stop()` to interrupt an in-progress `Start()` call in a reentrant manner from
within a
`FrameCaptured`/`EndOfFileReached` handler or from another thread.

**Key Methods**:

- **WavFileAudioCaptureDevice(path, frameSampleCount = 1600)**: Stores the path and per-frame
  sample count; does not open or validate the file until `Start()` is called.
- **Start()**: Opens the file, validates its RIFF/WAVE header (skipping unrecognized chunks),
  requires an uncompressed-PCM `fmt` chunk declaring 16-bit samples and exactly one channel, then
  synchronously reads and delivers every remaining sample through `FrameCaptured` in
  `frameSampleCount`-sized chunks before returning - running at full CPU speed rather than
  throttled to real time, since no live hardware backs this device. Raises `EndOfFileReached`
  exactly once, after the last frame, when the file is fully consumed without interruption.
- **Stop()**: Sets the stop-requested flag so an in-progress `Start()` call stops delivering
  further frames and returns without raising `EndOfFileReached`, since the file was not fully
  consumed.

**Error Handling**: The constructor throws `ArgumentException` for a null/empty path and
`ArgumentOutOfRangeException` for a non-positive frame sample count. `Start()` throws
`InvalidOperationException` - not `AudioDeviceUnavailableException` - when the file does not
exist, cannot be opened, is not a valid RIFF/WAVE file, has no `fmt`/`data` chunk, uses a
non-PCM format tag, a bit depth other than 16, or a channel count other than 1. This is a
deliberate design choice: an unsupported or malformed input file is an expected, handled
caller-configuration error to surface clearly, not a backend-availability failure, so
`IsAvailable` always reports `true` for this device rather than modeling file-format problems as
"no backend resolved".

**Dependencies**: The Base Class Library's `FileStream`/`BinaryReader` only; implements
`IAudioCaptureDevice`.

**Callers**: Any host composing `IAudioCaptureDevice`-consuming code (for example, a future
`recognize --input <wav-path>` CLI command, which subscribes to `EndOfFileReached` to know when
to call `Stop()` and stop waiting for further recognition results) that needs speech recognition
driven from a pre-recorded file instead of real microphone hardware.
