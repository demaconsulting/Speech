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
  normalized on first use only. Decode-once semantics: a private `LazyClip` serializes the first decode with a
  lock, so concurrent first users wait for the single decode and share its clip (the file is decoded and
  resampled exactly once); once a clip exists later reads never touch the file again. A failed load is not
  cached, so the next use retries and a repaired or replaced file takes effect without a restart. A decoding
  failure is rethrown as an `InvalidDataException` whose message has the form
  `Reference voice '{id}' ('{path}') could not be loaded: {reason}`, so one damaged clip among a hundred fails
  only when chosen. Used for voices that come from the reference-voice library. It also accepts the
  library voice's `MoodClips` (mood name to file path).
- **FromLoader(id, load, displayName)** (internal): the same lazy semantics over a caller-supplied producer;
  the seam tests use to count decodes.
- **Moods**: the mood names this voice has clips for, lower case, in ordinal ascending order; empty for a
  plain voice.
- **WithMoodFiles(moods)**: returns a copy with lazily decoded mood clips (names lower-cased and
  trimmed; a blank name or path throws; null or empty adds none; a later entry of the same name replaces an
  earlier one). `TryGetMoodSamples` and `HasMood` match the mood name case-insensitively after trimming
  (null or blank never matches) and report an absent mood as missing so the engine falls back to the base clip.
  A short or damaged mood clip fails on first use with an `InvalidDataException` in the same message format
  with `{id}` replaced by `voice/mood` (for example `Reference voice 'v/calm' ('C:\clips\short.wav') could
  not be loaded: ...`), naming the voice, the mood and the file.
- **WithMoodSamples(mood, samples, sampleRate, channelCount)** (internal): the same with an already-decoded clip,
  used by tests; a null or blank mood is rejected like `WithMoodFiles`.

**Error Handling**: `ArgumentException`/`ArgumentOutOfRangeException` for
invalid arguments; `FileNotFoundException` for a missing file; `InvalidDataException` for malformed
audio data; `NotSupportedException` for unsupported encodings; too-short clips throw `ArgumentException`
when decoded eagerly. For lazy base and mood clips every load failure (argument, I/O, format or
encoding) is wrapped in an `InvalidDataException` that names the file.

**Dependencies**: `AudioFileReader` and `AudioResampler` from the core `DemaConsulting.Speech` AudioSubsystem.

**Callers**: `ChatterboxTurboModelOptions`, the model's library-voice merge,
`OnnxChatterboxTurboEnglishSynthesisModel` and the
synthesis engine.
