## SpeechDemo ModelCatalogSubsystem Design

![ModelCatalogSubsystem Structure](ModelCatalogSubsystemView.svg)

### Overview

The ModelCatalogSubsystem provides the demo's model catalog and download panel. It contains the
following units, each documented in its own file:

- **ModelCatalogService** (`IModelCatalogService` / `ModelCatalogService`): the demo-owned
  catalog seam and its real implementation over the library's `SpeechModelCatalog` — see
  _ModelCatalogService Design_
- **ModelCatalogViewModel**: the panel's presentation state — the rows, the selection, the
  refresh and download commands, and the empty-catalog message — see _ModelCatalogViewModel
  Design_
- **ModelListItemViewModel**: one row's presentation state — see _ModelListItemViewModel Design_

### Interfaces

The subsystem exposes `IModelCatalogService`, `ModelCatalogService`, `ModelCatalogViewModel`, and
`ModelListItemViewModel` to the shell composition root and to the other panels
(`SynthesisPanelSubsystem`, `RecognitionPanelSubsystem`) that subscribe to
`IModelCatalogService.ModelInstalled` for auto-refresh-on-install. It consumes the library's
`SpeechModelCatalog` (via the seam, never directly by the view models) for enumeration and
download.

### Design

`ModelCatalogService` is the sole owner of the library-facing seam; `ModelCatalogViewModel` and
`ModelListItemViewModel` depend only on that seam interface and on the library's descriptor,
state, progress, and result value types — they never touch `SpeechModelCatalog`, the file
system, the network, or Avalonia directly. This is what allows the whole download lifecycle —
progress, success, checksum mismatch, transport failure, seam fault, and cancellation — to be
verified deterministically with no network access and no downloadable model in existence. See
each unit's own design document for its data model, algorithms, and error handling.
