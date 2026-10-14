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

The library provides **seven production model classes** across both roles it defines, via four model
packages. `DemaConsulting.Speech.Sherpa` supplies four through `.AddSherpaModels()`:
`SherpaOnnxZipformerEnRecognitionModel`, `SherpaOnnxNemotronStreamingEnRecognitionModel`,
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel` and `SherpaOnnxKokoroEnglishSynthesisModel`.
`DemaConsulting.Speech.Onnx.Kokoro` supplies `OnnxKokoroEnglishSynthesisModel` through
`.AddKokoroModels()`, `DemaConsulting.Speech.Onnx.NemotronStt` supplies
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
