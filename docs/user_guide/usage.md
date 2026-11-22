# Usage

## Current Capabilities

The library provides the following building blocks, starting with the `Diagnostics` and
`AudioSubsystem` foundations (recognition and synthesis are covered in later sections):

- **`ISpeechDiagnostics`** (`DemaConsulting.Speech.Diagnostics`): a contract for reporting
  structural diagnostic events to an optional host-supplied sink. `NullSpeechDiagnostics`
  provides the default no-op implementation via `NullSpeechDiagnostics.Instance`.
- **`AudioDeviceFactory`** (`DemaConsulting.Speech.AudioSubsystem`): the composition entry point
  for creating audio capture/playback devices and probes. When PortAudio initializes
  successfully, it returns real devices backed by PortAudio; otherwise it falls back to honest
  unavailable implementations. Its `RefreshDevices()` method forces PortAudio to re-scan its
  device table, so hardware attached or removed after start-up becomes visible to devices and
  probes created afterward; already-created device instances are not re-resolved by a refresh,
  so calling `Start()` on one afterward throws `AudioDeviceUnavailableException` and it must be
  replaced via `AudioDeviceFactory.CreateCaptureDevice()`/`CreatePlaybackDevice()` before starting
  again. This is the one deliberate exception to the library's "never throws at composition" policy: it throws
  `AudioDeviceInUseException` if any device created from the factory currently has an open or
  started stream, and the caller must stop that device before retrying.
- **`AudioDeviceDescription`** and **`AudioDeviceSelection`**: immutable, name-only
  representations of an audio device and a persistable user device preference.

The library also provides the storage and download machinery of the `ModelManagementSubsystem`
(`DemaConsulting.Speech.ModelManagementSubsystem`), used to fetch and install speech models on
a per-user basis:

- **`SpeechModelStore`**: per-user model storage rooted under `LocalApplicationData` by default
  (or a host-configured override via `SpeechModelStoreOptions`). Installs are atomic: a
  replacement is only swapped into `current/` after being fully staged and verified, so a
  failed repair never disturbs a working installation. Also provides honest `IsInstalled(...)`
  queries and `Uninstall(...)`.
- **`SpeechModelDownloader`**: allows unlimited concurrent downloads across different models,
  while serializing concurrent download attempts for the same model id so `SpeechModelStore`'s
  atomic install is never raced; fetches each file via an injected `IModelDownloadClient`
  (`HttpModelDownloadClient` by default), verifies each file's SHA-256 checksum, and only then
  hands the verified files to `SpeechModelStore` for atomic install. A corrupted, partial, or
  checksum-mismatched download is never marked installed.
- **`SpeechModelDownloaderOptions`** / **`DownloadMirror`**: optional configuration letting a host
  redirect every model download through an internal, possibly-authenticated HTTPS mirror instead
  of each model's own hardcoded source URI - see below.
- **`SpeechModelDownloadDescriptor`** / **`SpeechModelDownloadFile`**: describe the HTTPS URL(s)
  and expected SHA-256 checksum(s) for a model's downloadable files.
- **`SpeechModelDownloadProgress`**: reports per-file transfer progress, suitable for
  `IProgress<T>` consumption by a GUI progress bar.

Typical model download composition:

```csharp
using DemaConsulting.Speech.ModelManagementSubsystem;

var store = new SpeechModelStore();
var downloader = new SpeechModelDownloader(store);

var descriptor = new SpeechModelDownloadDescriptor(
[
    new SpeechModelDownloadFile(
        new Uri("https://example.com/model.bin"),
        "…64-character SHA-256 hex checksum…",
        "model.bin")
]);

await downloader.DownloadAsync("my-model", descriptor);
```

A host whose network policy blocks the public hosts a model's own declared URI points at (for
example a TLS-interception policy breaking `huggingface.co`) can redirect every model download
through an internal mirror it controls instead, with no change to any model's own declared
download descriptor:

```csharp
var mirror = new DownloadMirror(
    new Uri("https://models.internal.example.com/mirror"),
    bearerToken: "…token…"); // or: credentials: new NetworkCredential("user", "pass")

var options = new SpeechModelDownloaderOptions { Mirror = mirror };
var mirroredDownloader = new SpeechModelDownloader(store, client: null, diagnostics: null, options: options);

// Every file resolves to https://models.internal.example.com/mirror/my-model/model.bin
// and carries the configured bearer token/credentials, instead of the file's own declared URI.
await mirroredDownloader.DownloadAsync("my-model", descriptor);
```

`DownloadMirror.Credentials` and `DownloadMirror.BearerToken` are mutually exclusive - supply at
most one. A `DownloadAsync` call that fails (beyond a checksum mismatch) reports one of
several honest outcomes rather than one generic `Failed`, so a host can react to *why* it failed:
`NetworkBlocked` (a TLS/certificate failure, a DNS/connection failure, or a timeout - the shape a
blocked or intercepted network path typically produces, and a strong signal to try configuring a
`DownloadMirror`), `HttpError` (a real non-success HTTP response, e.g. 404/403/5xx), `IoFailure`
(a local disk/permission failure), or the final generic `Failed` fallback for anything else - with
the underlying exception always available in `SpeechModelDownloadResult.Error`.

The library also provides the model catalog/contract seam of the `ModelManagementSubsystem`,
describing *which* models are available and their tunable parameters:

- **`ISpeechModel`** (with **`IRecognitionModel`**/**`ISynthesisModel`** role markers): the
  common per-model contract — identity, `Role`, declared `Parameters`, declared
  `AudioTagSupport`, and `DownloadDescriptor`.
- **`ISpeechModelParameter`** (with **`NumericParameter`**, **`ChoiceParameter`**, and
  **`BooleanParameter`**): typed, self-validating tunable-parameter descriptors a host can use
  to render an appropriate generic control (slider, dropdown, checkbox) for any model.
- **`SpeechModelDescriptor`**: pairs one `ISpeechModel` with its current `SpeechModelState`
  (`NotDownloaded`, `Downloading`, `Downloaded`, or `FailedOrCorrupt`).
- **`SpeechModelCatalog`**: enumerates the library's known/compiled-in models alongside each
  one's current install state, and orchestrates downloading a known model by id.
  `DownloadAsync` is safe to call unconditionally on every launch - it's a cheap no-op once a
  model is installed, and otherwise returns `ChecksumMismatch`, `NetworkBlocked`, `HttpError`,
  `IoFailure`, or `Failed` on a real download problem (with the underlying exception in
  `SpeechModelDownloadResult.Error`), or throws `ArgumentException` for an unrecognized model id.

The library provides **eight production model classes** across both roles it defines, via five model
packages. `DemaConsulting.Speech.Sherpa` supplies four through `.AddSherpaModels()`:
`SherpaOnnxZipformerEnRecognitionModel`, `SherpaOnnxNemotronStreamingEnRecognitionModel`,
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel` and `SherpaOnnxKokoroEnglishSynthesisModel`.
`DemaConsulting.Speech.Onnx.Kokoro` supplies `OnnxKokoroEnglishSynthesisModel` through
`.AddKokoroModels()`, `DemaConsulting.Speech.Onnx.Piper` supplies `OnnxPiperLibriTtsEnSynthesisModel` through
`.AddPiperModels()`, `DemaConsulting.Speech.Onnx.NemotronStt` supplies
`OnnxNemotronMultilingualRecognitionModel` through `.AddNemotronSttModels()`, and
`DemaConsulting.Speech.Onnx.ChatterboxTurbo` supplies `OnnxChatterboxTurboEnglishSynthesisModel`
through `.AddChatterboxTurboModels(options)`. The catalog/contract seam, together with each model's
own `CreateBackend(...)` and (for synthesis) `CapabilityProfile` members, lets a host build a
model-settings-style UI and compose a real recognizer/synthesizer against any of them. See
"Recognizing Speech" and "Synthesizing Speech" below for each model's identity and license.

A model may also unpack its own downloaded archive before it becomes usable: `ISpeechModel`
declares an `InstallAsync(stagedFilesDirectory, cancellationToken)` hook, defaulting to a no-op
for the common case of a single directly-usable downloaded file, but overridable by a model
whose declared payload is a zip/tar archive. `SpeechModelDownloader`/`SpeechModelCatalog` invoke
this hook on the verified staged files before they are atomically installed, so this unpacking
never touches an existing installation until it succeeds.

Typical catalog usage:

```csharp
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Sherpa;

var catalog = new SpeechModelCatalog().AddSherpaModels();

foreach (var descriptor in catalog.Enumerate())
{
    Console.WriteLine($"{descriptor.DisplayName}: {descriptor.State}");
}
```

Typical capture composition:

```csharp
using DemaConsulting.Speech.AudioSubsystem;

var factory = new AudioDeviceFactory();
var device = factory.CreateCaptureDevice(AudioDeviceSelection.SystemDefault);
// Optionally pass a preferred AudioFormat when you already know the target format.

if (device.IsAvailable)
{
    device.FrameCaptured += (_, args) =>
    {
        // args.Samples contains normalized float audio samples.
    };

    device.Start();
}
```

### Model downloads, mirrors and firewalled environments

**Where the files come from.** The DEMA Consulting ONNX models are downloaded as single verified bundles
from the project's `models` release on GitHub
(<https://github.com/demaconsulting/Speech/releases/tag/models>). Each bundle is one zip that holds the
model's unmodified vendor files together with `LICENSE`, `NOTICE`, `README.txt` and `model.json` (which
records each file's original source and SHA-256). The library checks the zip against its pinned SHA-256,
extracts it, checks every extracted file against its own compiled pin, and only then installs the model. The
license files are kept with the installed model, under `current/licenses/<bundle>/` in the model's folder.

| Model id | Bundle(s) on the `models` release |
| --- | --- |
| `kokoro-onnx-v1_0-en` | `kokoro-en-v1.0-int8-b1.zip` |
| `nemotron-3.5-asr-streaming-0.6b-onnx-int4` | `nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1.zip` |
| `chatterbox-turbo-onnx-en` | `chatterbox-turbo-lm-20261014-fp16-b1.zip` (language model) |
| `chatterbox-turbo-onnx-en` | `chatterbox-turbo-aux-20261014-int4-b1.zip` (auxiliary graphs) |

The other Chatterbox Turbo variants (`chatterbox-turbo-onnx-en-lm-…-aux-…`) still download their eight files
individually from Hugging Face until their bundles are published. The Piper model
(`piper-onnx-libritts-r-en-us-medium`) and the four Sherpa models keep downloading their archives from the
sherpa-onnx GitHub releases, and the optional reference-voice pack comes from its own GitHub release.

**Interrupted downloads resume.** If a download is interrupted (a dropped connection, a timeout, or the
process being stopped), the bytes received so far are kept in a `.partial` folder inside the model's folder in
the store (next to `current/`). The next `DownloadAsync` call (or `speech-cli download`) for the same model
continues from those bytes when the server supports ranged requests, as the GitHub release does, and reports a
"Resuming" diagnostic. Resuming happens across calls; a single call does not retry on its own. The `.partial` folder is
removed after a successful install and when the model is uninstalled. Resuming applies to the built-in
`HttpModelDownloadClient`; a host-supplied `IModelDownloadClient` downloads each file from the start.

**Disk space during install.** While a bundle is being installed, the zip and the extracted files exist
side by side, so the install needs about twice the model's size (for example about 1.6 GB for the Nemotron
model). The zip is deleted as soon as its files are extracted.

**Mirror layout for bundles.** A `DownloadMirror` serves each declared file at
`{mirror}/{model-id}/{file}`. For a bundled model the declared file is the bundle zip, so the mirror path is
`{mirror}/{model-id}/{bundle}.zip`, for example
`{mirror}/kokoro-onnx-v1_0-en/kokoro-en-v1.0-int8-b1.zip`. This is a **breaking change for existing
mirrors** of these three models, which used to hold the individual vendor files: copy the bundle zips from
the `models` release into each model's folder (the default Chatterbox Turbo model needs both of its zips).
Mirrors for the Piper and Sherpa models, and for the non-default Chatterbox Turbo variants
(`{mirror}/{model-id}/onnx/{file}`), are unchanged.

**Firewalled environments.** Allow outbound HTTPS to `github.com`, `release-assets.githubusercontent.com`
and `objects.githubusercontent.com` (GitHub redirects release downloads to these hosts). That covers every
bundled model, the Piper and Sherpa archives (sherpa-onnx GitHub releases) and the reference-voice pack. No
Hugging Face access is needed, except for the non-default Chatterbox Turbo variants, which still download from
`huggingface.co` until their bundles are published. Where GitHub itself is blocked, use a `DownloadMirror`
laid out as described above.

### Text analysis for model authors

A model author whose engine needs sentence-level context - for example a phoneme-driven engine that
chooses a word's pronunciation from its part of speech - can opt in to a language-neutral text
analysis seam. It is entirely optional: a model that declares nothing, and a backend that does not
accept annotations, behave exactly as they always have.

- **`ISynthesisModel.TextAnalyzer`** is an optional `ITextAnalyzer` that defaults to `null` (no
  analysis). The library calls `Analyze(text, cancellationToken)` **once per `SpeakAsync` or
  `SynthesizeAsync` request**, before the text is split into chunks, with the whole request text
  after `ISpeechModel.NormalizeText`. Return a shared, cheap instance, because the member is read
  once per request; the analyzer runs on a thread-pool thread (never the thread that called
  `SpeakAsync`/`SynthesizeAsync`, so a slow analyzer cannot freeze a UI thread), so it must be
  thread-safe or stateless per call, must not assume thread affinity (no `ThreadStatic` state, no UI
  or synchronization-context access), must honor the token (an analyzer that ignores it delays stop
  and dispose until it returns), and must not retain `text`.
- **`TextAnalysisConventions.TagPlaceholder`** (the Unicode object replacement character) stands for
  each recognized audio tag in
  the text you analyze, one placeholder per tag; bracket text that is not a recognized tag stays
  literal. Treat the placeholder as an opaque, non-letter, non-word object that is neither a
  sentence terminator nor part of any word, avoid covering it with an annotation, and feel free to
  place a sentence break before or after it.
- **`SegmentAnnotation(Start, Length, Kind, Value)`** marks a range of the text. Offsets are UTF-16
  code unit indices (ordinary C# string indices) and `Length` must be positive. `Kind` and `Value`
  are opaque to the library - you choose them, for example `"reading"` or `"pos"` - and a consumer
  must ignore kinds it does not recognize. An annotation should begin and end on a non-whitespace
  character. The library keeps an annotation only when it lies wholly inside one stretch of text
  between tags and, later, wholly inside one chunk; otherwise it is **dropped, never clipped or
  moved**. A chunk that consists only of punctuation or symbols (no letter or digit) is normally
  merged into the previous chunk, or dropped at the start of the text, but it is kept as its own
  chunk, with its annotation, when an annotation with a non-empty `Value` lies wholly on it (for
  example a symbol your analyzer maps to a word); an annotation with an empty `Value` does not keep
  it, and text without any annotations is chunked exactly as before.
- **`TextAnalysisResult(annotations, sentenceBreaks)`** carries the annotations and optional
  sentence boundaries. A `null` `SentenceBreaks` means "no opinion", so the default punctuation
  heuristic splits sentences; any non-null list, even an empty one, is authoritative and replaces
  that heuristic. A break offset `k` means a sentence ends immediately before index `k`. Returning
  `null` or `TextAnalysisResult.Empty` means no analysis.
- **`IAnnotatedSynthesisBackend`** extends `ISynthesisBackend` with
  `GenerateAnnotated(text, annotations, speed, speakerId)`. The session calls it, instead of
  `Generate`, only for a chunk that has at least one annotation. The offsets are relative to the
  `text` argument, and a backend must still produce audio for `text` if it ignores every annotation.
  A backend that only implements `ISynthesisBackend` never sees annotations.

A failure in your analyzer never faults a session: the request continues without annotations and a
single warning that names only the exception type (never the message or the text) is reported
through the diagnostics sink. A cancellation requested while the analyzer runs is honored.

```csharp
using DemaConsulting.Speech.SynthesisSubsystem;

public sealed class ReadingAnalyzer : ITextAnalyzer
{
    public TextAnalysisResult Analyze(string text, CancellationToken cancellationToken)
    {
        var annotations = new List<SegmentAnnotation>();
        var index = text.IndexOf("read", StringComparison.Ordinal);
        if (index >= 0)
        {
            // Opaque kind and value: this model's backend decides what "reading" means.
            annotations.Add(new SegmentAnnotation(index, "read".Length, "reading", "red"));
        }

        return new TextAnalysisResult(annotations, null);
    }
}

// In your ISynthesisModel implementation:
//     private static readonly ITextAnalyzer Analyzer = new ReadingAnalyzer();
//     public ITextAnalyzer? TextAnalyzer => Analyzer;

public sealed class MyBackend : IAnnotatedSynthesisBackend
{
    public int SampleRate => 24000;

    public BackendAudio Generate(string text, float speed, int speakerId) =>
        GenerateAnnotated(text, [], speed, speakerId);

    public BackendAudio GenerateAnnotated(
        string text, IReadOnlyList<SegmentAnnotation> annotations, float speed, int speakerId)
    {
        foreach (var annotation in annotations)
        {
            if (annotation.Kind != "reading")
            {
                continue;
            }

            var word = text.Substring(annotation.Start, annotation.Length);
            // Use annotation.Value to pick the pronunciation of `word`.
        }

        // Synthesize `text` with your engine and return its samples at SampleRate.
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        // Release the loaded engine.
    }
}
```

**English text analysis (preview).** Instead of writing your own analyzer, a model for English can return the
`EnglishTextAnalyzer` of the optional, pre-release `DemaConsulting.Speech.Text.En` package. It annotates the part of
speech, normalizes abbreviations, symbols, initialisms and numbers, supplies sentence boundaries, applies a host's
pronunciation overrides and marks the reading of heteronyms such as "record". See "English Text Analysis" for the
annotations a backend reads, the options, the accuracy and its limits.

#### Further public building blocks for model authors

Two further building blocks are public so a model with its own tag syntax does not have to copy
them. **`DefaultModelCapabilityProfile`** exposes its `Instance` singleton, a
`RenderText(text, annotations, sentenceBreaks, parameterOverrides)` chunk-then-render step, and the
`ShortPauseMilliseconds`, `LongPauseMilliseconds`, and `EllipsisPauseMilliseconds` constants, so a
custom `IModelCapabilityProfile` can wrap or delegate to it. **`SentenceChunker`** exposes `Chunk`,
`ChunkWithMetadata`, and `ChunkAligned`, which re-bases annotations onto each chunk and accepts
analyzer-supplied sentence breaks.
