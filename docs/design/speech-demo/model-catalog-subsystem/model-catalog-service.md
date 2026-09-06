### ModelCatalogService

**Purpose**: Provide a demo-owned seam over the library's `SpeechModelCatalog` so the model
catalog panel can be exercised against controlled catalog data, and raise a notification once a
download genuinely installs a model.

**Why a Demo-Owned Seam**: `SpeechModelCatalog` is a sealed, disposable concrete type, and its
model-injecting constructor is internal to the library. A demo-owned interface over it is
therefore the only way to exercise the panel against controlled catalog data without adding
public API to the library. `IModelCatalogService` and `ModelCatalogService` are documented as
one unit because the interface has no independently observable behavior of its own - every test
exercises it through `ModelCatalogService`, its sole implementation.

**Data Model**:

| Member | Returns | Behavior |
| --- | --- | --- |
| `Enumerate()` | `IReadOnlyList<SpeechModelDescriptor>` | Never throws |
| `DownloadAsync(modelId, progress, cancellationToken)` | `Task<SpeechModelDownloadResult>` | Throws if id unknown |
| `ModelInstalled` | `event EventHandler<ModelInstalledEventArgs>?` | Raised once a model installs |

**Key Methods**:

- **Enumerate()**: Forwards unchanged to the injected `SpeechModelCatalog.Enumerate()`.
- **DownloadAsync(modelId, progress?, cancellationToken)**: Forwards to the injected catalog's
  `DownloadAsync`. Its one piece of added behavior is raising `ModelInstalled` after a call whose
  result reports `SpeechModelDownloadOutcome.Installed`: it looks up the installed model's `Role`
  from the catalog's own `Enumerate()` and raises the event with `(modelId, role)`, so the STT
  and TTS panels (each already depending on this same seam) can refresh themselves automatically
  the moment a matching-role model finishes installing, without a manual click or app restart.
  The event is never raised for a failed, canceled, or checksum-mismatched download attempt.

**Error Handling**: `Enumerate()` never throws; it never invents placeholder models when the
library's registry is empty - presenting an honest empty catalog is a presentation concern, not
a reason to fabricate data. `DownloadAsync` propagates the library's own `ArgumentException` for
an unknown model id.

**Dependencies**: The library's `SpeechModelCatalog`, `SpeechModelDescriptor`,
`SpeechModelDownloadResult`, `SpeechModelDownloadOutcome`. Deliberately does not dispose the
catalog - the composition root owns that lifetime - so a shared catalog can outlive any one
adapter.

**Callers**: `ModelCatalogViewModel` (the catalog panel's presentation state);
`SynthesisPanelViewModel` and the recognition panel view model (both subscribe to
`ModelInstalled` for auto-refresh-on-install).
