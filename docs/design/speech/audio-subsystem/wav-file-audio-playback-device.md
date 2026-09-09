### WavFileAudioPlaybackDevice

**Purpose**: Write synthesized speech to a `.wav` file as 16-bit PCM instead of rendering it to
real playback hardware, so any host application - not only the future `speak --output-audio` CLI
command - can capture synthesized audio deterministically.

**Data Model**: Holds the constructor-supplied `sampleRate`/`channelCount`, an open `FileStream`
and `BinaryWriter` created immediately at construction, and a running total of sample data bytes
written so far (needed to patch the RIFF/`data` chunk sizes once the total is known). Because
writing to a file never depends on real audio hardware, `IsAvailable` is always `true`.

**Key Methods**:

- **WavFileAudioPlaybackDevice(path, sampleRate, channelCount)**: Creates the file and writes a
  44-byte canonical RIFF/WAVE header immediately, with placeholder RIFF/`data` chunk sizes left
  to be patched by `Dispose()` - so a file truncated by a crash before `Dispose()` runs is still a
  recognizable, if silent, WAVE file rather than raw garbage.
- **Start()** / **Stop()**: No-ops beyond state tracking; there is no real playback stream to
  open or stop, since every `Write` call is fully synchronous.
- **Write(IReadOnlyList&lt;float&gt;)**: Converts and appends one block of normalized samples as
  16-bit PCM data. Each sample is clamped to `[-1.0, 1.0]` before scaling to a 16-bit signed
  value, matching the clamp-before-scale rounding behavior of the informal, temporary
  `WavFileWriter` this unit promotes to a permanent, tested, public capability, so a sample at or
  beyond the clamp boundary maps to the nearest valid 16-bit value rather than overflowing into
  an unrelated sample on the wire.
- **Dispose()**: Patches the RIFF and `data` chunk sizes with the now-known total byte count and
  closes the file. Idempotent.

**Error Handling**: The constructor throws `ArgumentException` for a null/empty path and
`ArgumentOutOfRangeException` for a non-positive sample rate or channel count. `Write` throws
`ArgumentNullException` for a null sample buffer and `ObjectDisposedException` once the device has
been disposed. Because file writes never depend on real audio hardware, `Start`/`Stop`/`Write`
never throw `AudioDeviceUnavailableException` the way a real device's operational members can.

**Dependencies**: The Base Class Library's `FileStream`/`BinaryWriter` only; implements
`IAudioPlaybackDevice` and `IDisposable`.

**Callers**: Any host composing `IAudioPlaybackDevice`-consuming code (for example, a future
`speak --output-audio <wav-path>` CLI command) that needs synthesized speech captured to a file instead
of played through real hardware.
