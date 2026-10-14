### ICliModelCatalog

**Purpose**: Give every command that needs models or reference voices one narrow, CLI-owned seam over the
library's `SpeechModelCatalog`, `SpeechModelStore` and `ReferenceVoiceLibrary`. The library's catalog and
store are sealed, and its test-friendly constructor is `internal` to the library, so without this seam no
command's parsing and formatting could be tested against controlled catalog data. Widening the library's
internal access to make the CLI testable would weaken the evidence the library's own tests provide, so the
CLI owns the seam instead, mirroring the pattern the demo application uses for the same reason
(`IModelCatalogService`).

**Data Model**: The interface has no state. Its members fall into four groups:

| Group | Members |
| --- | --- |
| Model management | `Enumerate`, `DownloadAsync`, `UninstallAsync`, `CleanUpLeftoversAsync` |
| Format and engine | `GetPreferredAudioFormat`, `CreateSynthesizerEngineAsync`, `CreateRecognizerEngineAsync` |
| Reference voices: browse | `VoicePacks`, `UserVoicesDirectory`, `EnumerateVoices`, `GetVoicePackState` |
| Reference voices: manage | `VoicePackHasEmotionsAsync`, `InstallVoicePackAsync`, `UninstallVoicePackAsync` |

**Key Methods**:

- **Enumerate()**: Returns a snapshot of every known model with its current install state. Never null and
  never throws; the list may be empty.
- **DownloadAsync(modelId, progress, cancellationToken)**: Downloads, verifies and installs a known model and
  returns the outcome. Throws `ArgumentException` for an id that matches no known model and
  `OperationCanceledException` on cancellation; an already-installed model is a no-op.
- **UninstallAsync(modelId, cancellationToken)** and **CleanUpLeftoversAsync(modelId, cancellationToken)**:
  Remove a model's installed content, or best-effort delete its leftover staging directories. Both throw
  `ArgumentException` for an invalid id and treat a model with nothing to remove as a successful no-op.
  Uninstall can also throw `SpeechModelStoreException`; cleanup never throws for a failed deletion.
- **GetPreferredAudioFormat(descriptor)**: Returns the playback audio format a synthesis-role model prefers.
- **CreateSynthesizerEngineAsync(descriptor, parameterValues, cancellationToken)** and
  **CreateRecognizerEngineAsync(descriptor, parameterValues, cancellationToken)**: Load an engine with no device
  bound yet, for a model of the matching role. The caller binds a device afterwards through the engine's own
  `CreateSessionAsync`.
- **VoicePacks**, **UserVoicesDirectory**, **EnumerateVoices()**, **GetVoicePackState(packId)**: Browse the
  reference voices. `EnumerateVoices` re-reads the file system on every call.
- **VoicePackHasEmotionsAsync(packId, cancellationToken)**, **InstallVoicePackAsync(packId, includeEmotions,
  progress, cancellationToken)**, **UninstallVoicePackAsync(packId, cancellationToken)**: Manage an installed
  pack; uninstalling a pack that is not installed is a no-op.

**Error Handling**: A `null` descriptor is an `ArgumentNullException`. A descriptor whose model role does not
match the requested engine kind is an `ArgumentException` that identifies the model by id; the catalog refuses
and never returns an engine for it. An unknown model or pack id is an `ArgumentException`. The user-facing
wording that tells the operator how to find a model of the right role belongs to the command that validates
the role (`speak`, `recognize`, `ask`), not to this seam.

**Dependencies**: The library's `SpeechModelDescriptor`, `SpeechModelDownloadResult`,
`SpeechModelDownloadProgress`, `SpeechModelState`, `ReferenceVoicePack`, `ReferenceVoiceInfo`,
`ISpeechSynthesizerEngine`, `ISpeechRecognizerEngine` and `AudioFormat`.

**Callers**: Every command in the ModelCommandsSubsystem, plus `SpeakCommand`, `RecognizeCommand`,
`AskCommand` and `DoctorCommand`, which share the seam so that the role-specific narrowing of a model happens
in one place rather than in every caller. `SpeechModelCatalogAdapter` is its only production implementation;
tests use a hand-written fake.
