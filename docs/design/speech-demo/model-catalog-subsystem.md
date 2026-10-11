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
- **ReferenceVoicePackListItemViewModel**: one reference voice pack row's presentation state — see
  _ReferenceVoicePackListItemViewModel Design_

### Interfaces

The subsystem exposes `IModelCatalogService`, `ModelCatalogService`, `ModelCatalogViewModel`, and
`ModelListItemViewModel`, and `ReferenceVoicePackListItemViewModel` to the shell composition root and to the other panels
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

### Mirror Reconfiguration

A `ModelCatalogService` constructed with a catalog factory (rather than a single, fixed catalog)
supports redirecting every subsequent download to a different mirror (or back to each model's
own public URI) while the application keeps running, through `ApplyMirror(DownloadMirror?)`.
`ApplyMirror` calls the factory again with freshly resolved `SpeechModelDownloaderOptions`,
swaps the service's current catalog to the one the factory returns, and disposes the catalog it
replaced; a service constructed over a single, externally owned catalog has no factory to call
and instead throws `InvalidOperationException` from `ApplyMirror`, rather than silently doing
nothing. See _ModelCatalogService Design_ for this seam's exact ownership and disposal contract,
and _Launch Options and Mirror Configuration_ in the SpeechDemo system design document for how
the composition root wires a `CatalogFactory` closure that both `App` (via launch-time mirror
arguments) and this panel (via its own mirror-settings fields) can invoke.

The panel itself exposes a mirror-settings sub-section on `ModelCatalogViewModel` - a base URL
field, an optional HTTP Basic username/password pair, and an optional bearer token, pre-populated
from any launch-time mirror arguments - and an apply command that builds a `DownloadMirror`
through the same `MirrorOptionsFactory` the launch-time options use, then calls the seam's
`ApplyMirror`. An invalid configuration (a malformed URL, or a seam that rejects reconfiguration
because it was not built with a catalog factory) is reported as an explained panel state rather
than thrown at the user, consistent with how every other seam failure in this panel is handled; a
blank URL applies a `null` mirror, reverting every subsequent download to each model's own public
URI.

### Reference Voice Packs

A `ModelCatalogService` constructed with a `ModelCatalogComposition` (a catalog plus the shared
`ReferenceVoiceLibrary`) and a composition factory also exposes the library's voice packs, their
install state and the user voices folder, and installs or uninstalls a pack. Because the
Chatterbox voice list is fixed when the model is constructed, a successful install, an uninstall
and a mirror change each rebuild the catalog and library together through the factory, dispose the
old pair, and raise `ReferenceVoicesChanged`. The panel (`ModelCatalogViewModel`) lists packs as
`ReferenceVoicePackListItemViewModel` rows in a "Reference voices" section with the consent note
and the user folder, and refuses pack operations while a model downloads (and model downloads or
mirror changes while a pack installs) because the rebuild would dispose the catalog an in-flight
download uses. Services built without a library report no packs and hide the section.

### Thread Safety

`ModelCatalogService.ApplyMirror` and `Dispose` are not safe to call concurrently with each
other or with themselves: both read and then replace the same current-catalog field without
synchronization, so two concurrent `ApplyMirror` calls (or a concurrent `ApplyMirror` and
`Dispose`) could race on which catalog ends up current or disposed. This panel only ever invokes
them serially from the UI thread, one command at a time, which is the only thread-safety
guarantee this service currently provides; a host that calls either member from more than one
thread must provide its own external synchronization.
