### ChatterboxTurboReferenceVoice

**Purpose**: Represent a caller-supplied reference clip in the form the speech encoder requires:
mono, 24 kHz, bounded in length. Chatterbox Turbo has no built-in voices, so this type is how a host
(or the core `ReferenceVoiceLibrary`) defines the voices a user can choose.

**Data Model**: An immutable `Id`, `DisplayName` and lazily produced mono 24 kHz float `Samples`
(internal), plus an optional table of lazily decoded mood clips by lower-case name (`Moods`,
`TryGetMoodSamples`, `HasMood`). `SampleRate` is 24000, `MinimumDurationSeconds` is 1 and
`MaximumDurationSeconds` is 30.

**Key Methods**:

- **FromSamples(id, samples, sampleRate, channelCount = 1, displayName = null)**: down-mix by averaging channels
  (`AudioResampler.DownMix` from the core AudioSubsystem), resample to 24 kHz (`AudioResampler.Resample`,
  a windowed-sinc resampler), reject clips under the minimum and trim clips over the maximum
- **FromFile(id, path, displayName = null)**: decode a WAV or FLAC file of any channel count with the core
  `AudioFileReader`, then delegate to `FromSamples`
- **FromFileLazy(id, path, displayName, moodClips)** (internal): a file-backed voice whose samples are decoded and
  normalized on first use only (thread-safe; a failed load is not cached). A decoding failure is
  rethrown as an `InvalidDataException` naming the file, so one damaged clip among a hundred fails only
  when chosen. Used for voices that come from the reference-voice library. It also accepts the
  library voice's `MoodClips` (mood name to file path).
- **WithMoodFiles(moods)**: returns a copy with lazily decoded mood clips (names lower-cased and
  trimmed; blank names or paths throw; null or empty adds none). `Moods` lists the mood names; `TryGetMoodSamples`
  and `HasMood` match the mood name case-insensitively after trimming (null or blank never matches) and report
  an absent mood as missing so the engine falls back to the base clip. A short or
  damaged mood clip fails on first use with an `InvalidDataException` naming the voice, mood and file.
- **WithMoodSamples(mood, samples, sampleRate, channelCount)** (internal): the same with an already-decoded clip,
  used by tests.

**Error Handling**: `ArgumentException`/`ArgumentOutOfRangeException` for
invalid arguments; `FileNotFoundException` for a missing file; `InvalidDataException` for malformed
audio data; `NotSupportedException` for unsupported encodings; too-short clips throw `ArgumentException`
when decoded eagerly. For lazy base and mood clips every load failure (argument, I/O, format or
encoding) is wrapped in an `InvalidDataException` that names the file.

**Dependencies**: `AudioFileReader` and `AudioResampler` from the core `DemaConsulting.Speech` AudioSubsystem.

**Callers**: `ChatterboxTurboModelOptions`, the model's library-voice merge,
`OnnxChatterboxTurboEnglishSynthesisModel` and the
synthesis engine.
