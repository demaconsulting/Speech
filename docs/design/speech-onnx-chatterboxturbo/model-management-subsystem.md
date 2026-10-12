## ModelManagementSubsystem Design

![ModelManagementSubsystem Structure](ChatterboxTurboModelManagementSubsystemView.svg)

### Overview

The SpeechOnnxChatterboxTurbo ModelManagementSubsystem registers the package's single synthesis
model with a Speech `SpeechModelCatalog`, declares that model's identity and downloadable files,
and owns the model-specific input pipeline: the embedded tokenizer and the reference-voice
normalization. It contains the following units:

- **SpeechModelCatalogChatterboxTurboExtensions**: the `AddChatterboxTurboModels()` extension
  method (documented here as a subsystem unit)
- **OnnxChatterboxTurboEnglishSynthesisModel**: the `ISynthesisModel` implementation, with
  `ChatterboxTurboModelOptions`, the language-model and auxiliary-graph variant enums and the
  end-of-sentence tag policy enum
- **ByteLevelBpeTokenizer**: a model-family-neutral byte-level BPE tokenizer
- **ChatterboxTurboReferenceVoice**: a normalized reference clip, optionally with lazily decoded mood
  clips

### Interfaces

`AddChatterboxTurboModels(this SpeechModelCatalog, ChatterboxTurboModelOptions?)` returns the same
catalog and throws `ArgumentNullException` for a null catalog. The model implements Speech's public
`ISynthesisModel`. `ChatterboxTurboModelOptions` carries the voices (or a voice directory), the optional
`ReferenceVoiceLibrary` and `IncludeStoreVoices` switch, an optional `ISpeechDiagnostics` sink, the
precision variants, the end-of-sentence tag policy, the mood join silence, the intra-op thread count and
generation limits; every option has a safe default and validates on assignment.

### Design

The registration method constructs one `OnnxChatterboxTurboEnglishSynthesisModel` from the options
and passes it to `SpeechModelCatalog.AddModels`. When the options name no library, it uses
`options?.ReferenceVoiceLibrary ?? new ReferenceVoiceLibrary(catalog.Store)` (on a copy, so the caller's
options are not modified). The model has no built-in voices: its `voice`
choice parameter lists the available voices in order, with the first as default, and
`ResolveSpeakerId` maps a voice id to its index. When a voice directory is supplied every `.wav`
or `.flac` file in it becomes a voice named after the file.

Voices also come from the core `ReferenceVoiceLibrary` over the catalog's store (or the one set in
`ChatterboxTurboModelOptions.ReferenceVoiceLibrary`; `IncludeStoreVoices = false` opts out): the user
folder and the downloaded voice pack are appended after the explicit and directory voices, first id
wins (a skipped duplicate is reported at `Info` level to the options' diagnostics sink), a clashing display
name gets a `(user)` or `(pack)` suffix, and the clips are decoded lazily so listing many clips is cheap and
one corrupt clip fails only when it is chosen. Duplicate ids among the explicit and directory voices throw
`ArgumentException`. Library voices may carry mood clips (for example `sad` or `shout`) that the engine uses
when the text is spoken in that mood; a voice without the mood uses its base clip. See
_ReferenceVoiceLibrary_ in the core ModelManagementSubsystem design for the pack, layout and merge rules.

The model's catalog id is `chatterbox-turbo-onnx-en` for the default variants (fp16 language model,
q4 auxiliary graphs) and `chatterbox-turbo-onnx-en-lm-<variant>-aux-<variant>` otherwise, so
different precision combinations install side by side. The download descriptor lists the four
graphs, each an `.onnx` file plus its `.onnx_data` file (eight files by default), each with a
pinned SHA-256 checksum, so every file is verified before use. The model download contains no voice files;
voices come from the user folder or from the separately installed voice pack.

`CreateBackend` requires at least one voice, verifies the files exist, and constructs
`OnnxChatterboxTurboNetworks`, the tokenizer from the embedded resource, and the synthesis engine,
returning it as the backend. Any failure disposes already-created sessions before the exception
propagates.

`ByteLevelBpeTokenizer` is loaded from a gzip-compressed copy of the upstream `tokenizer.json`
embedded in the assembly. It implements GPT-2 style byte-to-unicode mapping, regex pre-tokenization,
merge ranking, atomic added tokens (the paralinguistic tags) and the post-processor's trailing
end-of-text ids, so its output matches the reference tokenizer exactly (verified against golden ids
generated from the real tokenizer). Its name is model-neutral so it can be reused by other
byte-level BPE models.

`ChatterboxTurboReferenceVoice` is created from samples, a WAV or FLAC file, or lazily from a file. It
down-mixes channels by averaging, resamples to 24 kHz with the core `AudioResampler`, rejects clips
shorter than a minimum useful length and trims clips longer than the maximum.

### Error Handling

Argument errors throw `ArgumentException`-family exceptions (duplicate explicit or directory voice ids and
undefined variants included). A null catalog throws `ArgumentNullException`; a missing voice directory throws
`DirectoryNotFoundException`. Unreadable files throw
`FileNotFoundException`; malformed or unsupported WAV or FLAC data throws `InvalidDataException` or
`NotSupportedException`, and a damaged library or directory voice fails only when first used, with an
`InvalidDataException` naming the file. A corrupt embedded tokenizer throws `InvalidDataException`.
`CreateBackend` throws `InvalidOperationException` when no voice exists and `FileNotFoundException` for a
missing model file.

### Dependencies

The Speech library's public model contracts, `Microsoft.ML.OnnxRuntime` (through the networks unit),
`System.Text.Json` and `System.IO.Compression`.
