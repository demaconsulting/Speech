### ChatterboxTurboReferenceVoice

**Purpose**: Represent a caller-supplied reference clip in the form the speech encoder requires:
mono, 24 kHz, bounded in length. Chatterbox Turbo has no built-in voices, so this type is how a host
defines the voices a user can choose.

**Data Model**: An immutable id, display name and mono 24 kHz float sample array.
`SampleRate` is 24000, `MinimumDurationSeconds` is 1 and `MaximumDurationSeconds` is 30.

**Key Methods**:

- **FromSamples(id, name, samples, sampleRate, channelCount)**: down-mix by averaging channels
  (`ReferenceAudioResampler.DownMix`), resample to 24 kHz (`ReferenceAudioResampler.Resample`, a
  windowed-sinc resampler), reject clips under the minimum and trim clips over the maximum
- **FromFile(id, name, path)**: parse a RIFF/WAVE 16-bit PCM file of any channel count with a small
  in-house reader, then delegate to `FromSamples`

**Error Handling**: `ArgumentException`/`ArgumentNullException`/`ArgumentOutOfRangeException` for
invalid arguments; `FileNotFoundException` for a missing file; `InvalidDataException` for malformed
RIFF data; `NotSupportedException` for non-PCM16 formats; too-short clips throw `ArgumentException`.

**Dependencies**: none beyond the base class library.

**Callers**: `ChatterboxTurboModelOptions`, `OnnxChatterboxTurboEnglishSynthesisModel` and the
synthesis engine.
