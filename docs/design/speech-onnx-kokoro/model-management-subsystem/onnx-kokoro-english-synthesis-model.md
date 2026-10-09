### OnnxKokoroEnglishSynthesisModel

**Purpose**: Be this package's one real, concrete `ISynthesisModel`, backing the bare ONNX export
`onnx-community/Kokoro-82M-v1.0-ONNX` (Kokoro-82M v1.0, upstream `hexgrad/Kokoro-82M` lineage), run
directly through ONNX Runtime with no bundled native inference engine - unlike the sibling
`DemaConsulting.Speech.Sherpa` package's sherpa-onnx-backed `SherpaOnnxKokoroEnglishSynthesisModel`
(the earlier, StyleTTS2-derived v0.19 variant, bundling its own phonemizer). This class owns
text-to-phoneme conversion itself via the embedded `KokoroLexiconPhonemizer`, because this v1.0
ONNX export performs only the final waveform-generation forward pass.

**Data Model**: No persistent instance state beyond a constructor-supplied, optional ordered list
of preferred execution provider names (`_preferredExecutionProviderNames`), forwarded unchanged to
`OnnxExecutionProviderSelector.Create`. `ModelId = "kokoro-onnx-v1_0-en"`. `VoiceParameterId =
"voice"`. `DownloadDescriptor` declares 30 individual HTTPS files: the `fp16` ONNX graph variant
(`onnx/model_fp16.onnx`, 163,234,740 bytes) and 29 voice style-vector `.bin` files (one per
declared English voice, each an exact `(510, 256)` float32 array - 522,240 bytes - except the
bundled default blend voice `af`, which is `(512, 256)` - 524,288 bytes), each with an
independently-computed SHA-256 checksum recorded directly in this class's own source, fetched
through a locally configured download mirror because the development sandbox could not reach
Hugging Face directly.

**Key Methods**:

- **Id / DisplayName / Role / Parameters / AudioTagSupport**: `Role = Synthesis`, one tunable
  `ChoiceParameter` named `voice` (29 options spanning this model's American/British English
  voices only, default `af_heart`), `AudioTagSupport = None` (Kokoro has no native inline Natural
  Language Audio Tag concept; the default `DefaultModelCapabilityProfile` already strips
  unsupported tags and renders pauses as real silence, so no bespoke `CapabilityProfile` override
  is needed).
- **LicenseName / LicenseUrl**: `LicenseName = "Apache-2.0"` and `LicenseUrl` pointing at the
  canonical `https://www.apache.org/licenses/LICENSE-2.0` full text, matching the license family
  of the sherpa-onnx v0.19 Kokoro variant (both trace back to the same upstream
  `hexgrad/Kokoro-82M` model lineage).
- **DownloadDescriptor**: the 30-file descriptor described above; `ModelRelativeInstallPath =
  "onnx/model_fp16.onnx"`.
- **ISynthesisModel.CreateBackend(installedModelDirectory)** *(public)*: builds the ONNX Runtime
  session via `OnnxExecutionProviderSelector.Create(modelPath, _preferredExecutionProviderNames,
  OnnxKokoroSynthesisEngine.RunProbeInference)`, so every accelerated candidate the selector tries
  is also probed with a representative inference before being accepted - catching a provider that
  constructs successfully but fails on the actual graph at `Run()` time (observed with DirectML
  and the `ConvTranspose` operator), which construction alone cannot detect - then reads every
  declared voice's `.bin` file into a flattened `float[]` keyed by its index in `VoiceOrder` (the
  engine-specific integer speaker id), and constructs and returns a loaded
  `OnnxKokoroSynthesisEngine` from the session, a fresh `KokoroPhonemeVocabulary`, a fresh
  `KokoroLexiconPhonemizer`, and the loaded voice styles. This model's phoneme vocabulary is not
  downloaded at all - it is embedded directly in this package, since it is small, fixed, and
  versioned together with this class's own token-id handling code.
- **ISynthesisModel.PreferredAudioFormat** *(public)*: mono `24000` Hz, matching
  `OnnxKokoroSynthesisEngine.SampleRate`, this model's own fixed output sample rate confirmed from
  the upstream README and this package's own validation spike.
- **ISynthesisModel.ResolveSpeakerId(parameterValues)** *(public, explicit interface member)*:
  reads the selected `voice` parameter value (falling back to `DefaultVoice = "af_heart"` for a
  null bag, a missing key, or a non-string value), and resolves it to its ordinal index within
  `VoiceOrder` - the engine-specific integer speaker id - falling back to the default voice's
  index for an unrecognized value. Never throws.

**Error Handling**: `CreateBackend` throws `ArgumentException` for a null or empty installed-model
directory (`ArgumentException.ThrowIfNullOrEmpty`). A missing or unreadable voice `.bin` file, an
accelerated-provider load failure, or a probe failure (an accelerated candidate that loads but
cannot actually run this model) inside `OnnxExecutionProviderSelector.Create`, propagates
unchanged to the caller - handled identically to a download/install failure by Speech's
`SpeechSynthesizerFactory`, which degrades it to the honest unavailable synthesizer. CPU is
always the final, unprobed fallback, so `CreateBackend` only fails this way for a genuinely
malformed or unreadable model file.

**Voice Subset (Stage 2)**: this release ships 54 named voices across multiple languages; this
class declares only its 29 American/British English voices, because its embedded
`KokoroLexiconPhonemizer` only converts English text to phonemes - feeding the model's Japanese or
Mandarin voices through an English-only phonemizer would mispronounce their own language's text.
Adding a further English voice later is purely additive: download and verify its `.bin` file, add
one `VoiceOrder`/`VoiceLabels` entry and one `DownloadDescriptor` file entry; no other code in this
class needs to change.

**Style Vectors Indexed by Utterance Length**: each voice's `.bin` file is a flattened `(rows,
256)` float32 array, and the row selected for one utterance depends on that utterance's own
phoneme-token count - see `OnnxKokoroSynthesisEngine.Generate`'s row-selection logic, which
mirrors the proven Python reference pipeline exactly.

**Dependencies**: `ISynthesisModel`, `SpeechModelDownloadDescriptor`, `SpeechModelDownloadFile`,
`ChoiceParameter`, `ChoiceParameterOption`, `OnnxExecutionProviderSelector` (the sibling SpeechOnnx
system's shared helper), `OnnxKokoroSynthesisEngine`, `KokoroPhonemeVocabulary`,
`KokoroLexiconPhonemizer`, `ISynthesisBackend`, Microsoft.ML.OnnxRuntime's `InferenceSession`.

**Callers**: `SpeechModelCatalogKokoroExtensions.AddKokoroModels` (registers this instance through
`SpeechModelCatalog.AddModels`); `SpeechModelDownloader` (fetches and checksum-verifies every
declared file; this class needs no `InstallAsync` override); the Speech library's
`DefaultSynthesisBackendFactory` (invokes the public `CreateBackend` member); `SynthesisSession`
(consumes the public `ResolveSpeakerId` member once per synthesized segment); and hosts or
composition code that read `PreferredAudioFormat` before engine construction.
