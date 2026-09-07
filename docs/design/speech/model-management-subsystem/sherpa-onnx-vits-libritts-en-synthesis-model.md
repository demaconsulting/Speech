### SherpaOnnxVitsLibriTtsEnglishSynthesisModel

**Purpose**: Be the library's first real, concrete `ISynthesisModel`, backing sherpa-onnx's
VITS/Piper text-to-speech voice `vits-piper-en_US-libritts_r-medium` (converted from the Piper
project's LibriTTS-R-trained English (US) checkpoint), completing the catalog with a shippable
model for every role the library defines.

**Data Model**: No instance state - a stateless declaration of this one model's identity,
download descriptor, numeric speaker parameter, and engine configuration. `ModelId` =
`vits-piper-en_US-libritts_r-medium`. `DownloadDescriptor` declares exactly one file: the model's
own `.tar.bz2` archive from `k2-fsa/sherpa-onnx`'s `tts-models` GitHub Releases tag (82,038,311
bytes, SHA-256 `10dc268f3e371696d721486123e2705a9fc1faa113491979fde4d88dba1f1b1c`, independently
confirmed against a real download in this project's development sandbox, not fabricated).

**Key Methods**:

- **Id / DisplayName / Role / Parameters / AudioTagSupport**: fixed values - `Role = Synthesis`,
  one tunable `NumericParameter` named `speaker` (range `0`-`903`, default `0`, `IsInteger = true`
  - a plain numeric index, since LibriTTS-R's speaker embeddings have no published
  human-readable name mapping; `IsInteger = true` makes a host render this as a numeric up-down
  rather than a continuous slider, so a fractional speaker index can never be selected in the
  first place), `AudioTagSupport = None` (a plain VITS/Piper model has no native inline Natural
  Language Audio Tag concept; the default `DefaultModelCapabilityProfile` already strips
  unsupported tags and renders pauses as real silence, so no bespoke `CapabilityProfile`
  override is needed).
- **LicenseName / LicenseUrl**: `LicenseName = "CC BY 4.0"` and `LicenseUrl` pointing at the
  canonical `https://creativecommons.org/licenses/by/4.0/` full text (see the "License" section
  below), so this model's attribution requirement is discoverable programmatically without
  parsing `DisplayName`.
- **DownloadDescriptor**: the single-file `.tar.bz2` archive descriptor described above.
- **InstallAsync(stagedFilesDirectory, cancellationToken)**: delegates to the shared
  `TarBz2ArchiveExtractor` (built in Phase 7a, reused here unchanged) to extract the downloaded
  archive in place and delete it, leaving the archive's own top-level folder
  (`vits-piper-en_US-libritts_r-medium/`) containing every file `CreateEngineConfig` references.
- **ISynthesisModel.CreateEngineConfig(installedModelDirectory)** *(internal)*: builds an
  `OfflineTtsConfig` wiring `Model.Vits.Model`/`.Tokens`/`.DataDir` to this model's onnx/tokens/
  espeak-ng-data files, `NoiseScale = 0.333f`, `NoiseScaleW = 0.333f`, `LengthScale = 1.0f` (this
  model's own published `en_US-libritts_r-medium.onnx.json` recommended defaults, read directly
  in this session - deliberately different from another sibling VITS/Piper voice's proven
  `0.667`/`0.8` defaults), `Provider = "cpu"`. `Lexicon` is deliberately left unset - this archive
  ships no `lexicon.txt`. This exact configuration was proven end-to-end in this project's
  development sandbox: a real, loaded `OfflineTts` instance built from it reported
  `SampleRate = 22050` and `NumSpeakers = 904`, and generated real, audibly non-silent audio for
  an English sentence.
- **ISynthesisModel.CapabilityProfile** *(internal)*: not overridden - resolves to the interface's
  default, `DefaultModelCapabilityProfile.Instance`.
- **ISynthesisModel.ResolveSpeakerId(parameterValues)** *(internal)*: reads the `speaker`
  `NumericParameter` value straight out of the supplied bag and returns it as the sherpa-onnx
  speaker id when it is a finite number within `[0, 903]`; falls back to the default speaker id
  (`0`) for a `null` bag, a missing key, a non-numeric value, or a number outside that range.
  Accepts both a boxed `double` (the value type a host's generic numeric-parameter UI, such as
  `NumericParameterViewModel.BoxedValue`, supplies) and a boxed `int` (for a caller building the
  bag programmatically). Never throws.

**Error Handling**: `InstallAsync` and `CreateEngineConfig` throw `ArgumentException` for a null
or empty directory argument, matching every other unit in this subsystem's validation
convention. Any extraction failure inside `TarBz2ArchiveExtractor` propagates unchanged and is
handled by `SpeechModelDownloader`, identically to a download failure.

**License**: CC BY 4.0 - confirmed directly from this model's own `MODEL_CARD` (downloaded and
read in this session: dataset `http://www.openslr.org/141/` (LibriTTS-R), `License: CC BY 4.0`).
This is an attribution license, materially different from both the Apache-2.0 and NVIDIA Open
Model License recognition models registered alongside it: any redistribution of audio generated
by this model, or of the model itself, must credit the LibriTTS-R dataset and the Piper
text-to-speech project that converted it. Unlike CC BY-SA (ShareAlike), downstream relicensing
under different terms is not restricted. This fact is now also exposed structurally, not only in
this prose, via `LicenseName = "CC BY 4.0"` and
`LicenseUrl = https://creativecommons.org/licenses/by/4.0/`.

**Multi-Speaker Selection Resolved**: this model's `MODEL_CARD` declares 904 distinct speakers,
identified only by plain numeric `sid` 0-903 - the LibriTTS-R speaker embeddings this voice was
fine-tuned on have no published human-readable name mapping, unlike the sibling
`SherpaOnnxKokoroEnglishSynthesisModel`'s small, named voice set. Rather than fabricate names
this model owns no confirmed mapping for, this class exposes the full range directly as a
numeric `speaker` parameter and overrides `ResolveSpeakerId(parameterValues)` to read that value
straight out of the bag, degrading gracefully to the default (`0`) rather than throwing. This
was previously a known, accepted, out-of-scope limitation (only speaker `0` was exposed); that
gap is closed here, mirroring the sibling `SherpaOnnxKokoroEnglishSynthesisModel`'s own resolved
voice-selection gap, but with a numeric parameter rather than a named `ChoiceParameter` since no
name mapping exists for this model's speakers.

**Dependencies**: `ISynthesisModel`, `NumericParameter`, `SpeechModelDownloadDescriptor`,
`SpeechModelDownloadFile`, `TarBz2ArchiveExtractor`, `org.k2fsa.sherpa.onnx`'s `OfflineTtsConfig`,
`DefaultModelCapabilityProfile`.

**Callers**: `SpeechModelCatalog.KnownModels` (registers this instance);
`SpeechModelDownloader` (invokes `InstallAsync` after checksum verification);
`SherpaOnnxSynthesisEngine` (consumes the internal `CreateEngineConfig` member through the
SynthesisSubsystem); `SherpaOnnxSpeechSynthesizer.GenerateSegment` (consumes the internal
`ResolveSpeakerId` member once per synthesized segment).
