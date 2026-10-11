### ChatterboxTurboReferenceVoice

**Purpose**: Represent a caller-supplied reference clip in the form the speech encoder requires:
mono, 24 kHz, bounded in length. Chatterbox Turbo has no built-in voices, so this type is how a host
defines the voices a user can choose.

**Data Model**: An immutable id, display name and mono 24 kHz float sample array.
`SampleRate` is 24000, `MinimumDurationSeconds` is 1 and `MaximumDurationSeconds` is 30.

**Key Methods**:

- **FromSamples(id, name, samples, sampleRate, channelCount)**: down-mix by averaging channels
  (`AudioResampler.DownMix` from the core AudioSubsystem), resample to 24 kHz (`AudioResampler.Resample`,
  a windowed-sinc resampler), reject clips under the minimum and trim clips over the maximum
- **FromFile(id, name, path)**: decode a WAV or FLAC file of any channel count with the core
  `AudioFileReader`, then delegate to `FromSamples`

**Error Handling**: `ArgumentException`/`ArgumentNullException`/`ArgumentOutOfRangeException` for
invalid arguments; `FileNotFoundException` for a missing file; `InvalidDataException` for malformed
audio data; `NotSupportedException` for unsupported encodings; too-short clips throw `ArgumentException`.

**Dependencies**: `AudioFileReader` and `AudioResampler` from the core `DemaConsulting.Speech` AudioSubsystem.

**Callers**: `ChatterboxTurboModelOptions`, `OnnxChatterboxTurboEnglishSynthesisModel` and the
synthesis engine.
