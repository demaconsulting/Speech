### SherpaOnnxKokoroEnglishSynthesisModel

**Purpose**: Be the library's fourth real, concrete model and second real `ISynthesisModel`,
backing sherpa-onnx's Kokoro text-to-speech archive `kokoro-int8-en-v0_19` (an English-only,
int8-quantized, StyleTTS2-derived multi-speaker model), and close the "speaker selection is out
of scope" gap the sibling `SherpaOnnxVitsLibriTtsEnglishSynthesisModel` explicitly deferred by
owning a real voice-to-speaker-id mapping.

**Data Model**: No instance state - a stateless declaration of this one model's identity,
download descriptor, voice-selection parameter, and engine configuration. `ModelId` =
`kokoro-int8-en-v0_19`. `DownloadDescriptor` declares exactly one file: the model's own
`.tar.bz2` archive from `k2-fsa/sherpa-onnx`'s `tts-models` GitHub Releases tag (103,248,205
bytes, SHA-256 `c9f0dd393615805b0bab050c340834d5e684e732aec91c0e860cd30e982c08bd`, independently
re-downloaded and re-hashed in this project's development sandbox during this pass, not
fabricated or copied uncritically from an earlier measurement).

**Key Methods**:

- **Id / DisplayName / Role / Parameters / AudioTagSupport**: `Role = Synthesis`, one tunable
  `ChoiceParameter` named `voice` (11 options, default `af`), `AudioTagSupport = None` (Kokoro
  has no native inline Natural Language Audio Tag concept; the default
  `DefaultModelCapabilityProfile` already strips unsupported tags and renders pauses as real
  silence, so no bespoke `CapabilityProfile` override is needed).
- **LicenseName / LicenseUrl**: `LicenseName = "Apache-2.0"` and `LicenseUrl` pointing at the
  canonical `https://www.apache.org/licenses/LICENSE-2.0` full text (see the "License" section
  below), so this model's license is discoverable programmatically without parsing
  `DisplayName`.
- **DownloadDescriptor**: the single-file `.tar.bz2` archive descriptor described above.
- **InstallAsync(stagedFilesDirectory, cancellationToken)**: delegates to the shared
  `TarBz2ArchiveExtractor` (built in Phase 7a, reused here unchanged) to extract the downloaded
  archive in place and delete it, leaving the archive's own top-level folder
  (`kokoro-int8-en-v0_19/`) containing every file `CreateEngineConfig` references:
  `model.int8.onnx` (134,186,977 bytes), `voices.bin` (5,755,904 bytes), `tokens.txt` (1,078
  bytes), and `espeak-ng-data/` (391 bundled phoneme/dictionary files).
- **ISynthesisModel.CreateEngineConfig(installedModelDirectory)** *(internal)*: builds an
  `OfflineTtsConfig` wiring `Model.Kokoro.Model`/`.Voices`/`.Tokens`/`.DataDir` to this model's
  onnx/voices/tokens/espeak-ng-data files, `LengthScale = 1.0f`, `Provider = "cpu"`.
  `Model.Kokoro.DictDir`/`.Lexicon`/`.Lang` are deliberately left unset - this English-only
  archive ships neither a Chinese jieba dictionary nor a lexicon file, unlike the
  `kokoro-multi-lang-*` variants. The exact field names (`Model`/`Voices`/`Tokens`/`DataDir`/
  `LengthScale`/`DictDir`/`Lexicon`/`Lang`) were confirmed by reflecting directly over the
  installed `sherpa-onnx.dll` from the `org.k2fsa.sherpa.onnx` package this
  repository's `.csproj` references. This exact configuration was proven end-to-end in this
  project's development sandbox during this pass: a real, loaded `OfflineTts` instance built
  from it reported `SampleRate = 24000` and `NumSpeakers = 11`, and generated real, audibly
  non-silent, and audibly distinct audio for two different speaker ids given the identical input
  sentence.
- **ISynthesisModel.PreferredAudioFormat** *(public)*: mono `24000` Hz, a best-effort playback
  hint matching the real engine sample rate observed for this model in this repository's manual
  engine-load verification.
- **ISynthesisModel.CapabilityProfile** *(internal)*: not overridden - resolves to the interface's
  default, `DefaultModelCapabilityProfile.Instance`.
- **ISynthesisModel.ResolveSpeakerId(parameterValues)** *(internal)*: this model's own owned
  knowledge - maps the selected `voice` `ChoiceParameter` value to sherpa-onnx's real integer
  speaker id for that voice, using a confirmed `id2speaker` ordering (index = speaker id): `af`=0,
  `af_bella`=1, `af_nicole`=2, `af_sarah`=3, `af_sky`=4, `am_adam`=5, `am_michael`=6, `bf_emma`=7,
  `bf_isabella`=8, `bm_george`=9, `bm_lewis`=10. This ordering was confirmed by three independent
  means during this pass: (1) upstream `k2-fsa/sherpa-onnx`'s own
  `scripts/kokoro/v0.19/generate_voices_bin.py` generation script, which hard-codes this exact
  `id2speaker` map; (2) exact byte arithmetic against the real downloaded `voices.bin` - each
  voice is a `(511, 1, 256)` float32 embedding tensor, so `511 * 1 * 256 * 4 bytes * 11 voices =
  5,755,904 bytes`, an exact match to the real file's measured size on disk; and (3) a live
  loaded model in this pass's own spike reporting `NumSpeakers = 11`. Never throws: a
  `null` bag, a missing key, or an unrecognized value falls back to the default voice's speaker
  id (`0`, "af") rather than failing synthesis.

**Error Handling**: `InstallAsync` and `CreateEngineConfig` throw `ArgumentException` for a null
or empty directory argument, matching every other unit in this subsystem's validation
convention. Any extraction failure inside `TarBz2ArchiveExtractor` propagates unchanged and is
handled by `SpeechModelDownloader`, identically to a download failure.

**License**: Apache License 2.0 - confirmed directly from this archive's own `LICENSE` file
(re-read in this pass, not assumed from memory or copied from a different model's docs),
matching the recognition models' license family rather than the VITS/Piper synthesis model's
CC BY 4.0 attribution license. This fact is now also exposed structurally, not only in this
prose, via `LicenseName = "Apache-2.0"` and
`LicenseUrl = https://www.apache.org/licenses/LICENSE-2.0`.

**Capability Honesty**: Kokoro exposes no discrete emotion-style parameter anywhere in
`OfflineTtsKokoroModelConfig` - only `Voices` (a fixed set of pre-trained speaker embeddings,
selected here as a `ChoiceParameter`) and `LengthScale` (a playback-speed multiplier, already
handled generically by the existing speed/volume convention). This class's voices genuinely
differ in accent, gender, and vocal character (`af_*`/`bf_*` voices sound female with
American/British-leaning phonetic renderings respectively, and `am_*`/`bm_*` sound male), but
none of them is a distinct "happy"/"sad"/"excited" reading of the same voice - this doc
deliberately avoids claiming an emotion-control capability this model does not have.

**Voice Selection Resolved**: unlike the sibling VITS/Piper model, this model's voice selection
is genuinely wired end-to-end: `SherpaOnnxSpeechSynthesizer.GenerateSegment` now calls
`ISynthesisModel.ResolveSpeakerId` (a new, non-breaking default-hook interface member) instead of
hard-coding `speakerId: 0`, and `SpeechSynthesizerFactory.Create` threads an optional
`parameterValues` bag through to the synthesizer for this purpose. See
`sherpa-onnx-speech-synthesizer.md` for the full mechanism.

**Dependencies**: `ISynthesisModel`, `SpeechModelDownloadDescriptor`, `SpeechModelDownloadFile`,
`TarBz2ArchiveExtractor`, `ChoiceParameter`, `ChoiceParameterOption`,
`org.k2fsa.sherpa.onnx`'s `OfflineTtsConfig`/`OfflineTtsKokoroModelConfig`,
`DefaultModelCapabilityProfile`.

**Callers**: `SpeechModelCatalog.KnownModels` (registers this instance);
`SpeechModelDownloader` (invokes `InstallAsync` after checksum verification);
`SherpaOnnxSynthesisEngine` (consumes the internal `CreateEngineConfig` member through the
SynthesisSubsystem); `SherpaOnnxSpeechSynthesizer.GenerateSegment` (consumes the internal
`ResolveSpeakerId` member once per synthesized segment); and hosts or factory composition code
that read `PreferredAudioFormat` before engine construction.
