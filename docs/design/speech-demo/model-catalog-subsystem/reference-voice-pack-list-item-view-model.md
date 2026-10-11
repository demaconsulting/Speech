### ReferenceVoicePackListItemViewModel

**Purpose**: Present one reference voice pack row in the model catalog panel: its identity, size,
license and attribution, install state, progress and failure explanation, plus the derived
captions and enablement values the row displays.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `Id` / `DisplayName` | `string` | The pack identity (for example `voice-zero-1.0`) |
| `SizeText` | `string` | The download size, for example "570 MiB download" |
| `LicenseText` | `string` | "{attribution} - {license}" |
| `ConsentNotice` | `string` | The voice-cloning consent note |
| `State` | `SpeechModelState` | The install lifecycle state |
| `ProgressFraction` | `double?` | Install progress, when known |
| `FailureMessage` | `string?` | The explanation for a failed attempt |
| `StatusText` | `string` | "Not installed", "Installing", "Installed" or "Failed or corrupt" |
| `CanInstall` / `CanRemove` / `IsInstalling` / `HasFailureMessage` | `bool` | Derived enablement and visibility |

**Key Methods**: None beyond the constructor (copies a `ReferenceVoicePack`) and property setters,
which announce the derived values when `State` or `FailureMessage` changes.

`CanInstall` is true for `NotDownloaded` and `FailedOrCorrupt` (so a transient failure can be
retried); `CanRemove` is true only for `Downloaded`.

**Error Handling**: The constructor throws `ArgumentNullException` for a null pack; otherwise this
unit never throws.

**Dependencies**: The library's `ReferenceVoicePack` and `SpeechModelState`. Never touches the
seam, the file system, the network, or Avalonia.

**Callers**: `ModelCatalogViewModel` (owns the `VoicePacks` collection of these rows).
