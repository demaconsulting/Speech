### ReferenceVoicePackListItemViewModel

**Purpose**: Present one reference voice pack row in the model catalog panel: its identity, size,
license and attribution, install state, progress and failure explanation, plus the derived
captions and enablement values the row displays.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `Id` / `DisplayName` | `string` | The pack identity (for example `voice-zero-1.0`) |
| `SizeText` | `string` | The download size, for example "Download: 570 MiB" |
| `IncludeEmotions` | `bool` | The user's choice to install the emotion variants (default `true`) |
| `HasEmotions` | `bool` | Whether the installed pack has emotion variants (constructor `hasEmotions`) |
| `InstalledSizeText` | `string` | "Installs/Uses about N MiB (with emotions or voices only)" |
| `InstallButtonText` | `string` | "Add emotions" for a voices-only install, otherwise "Install" |
| `CanUpgradeToEmotions` / `ShowEmotionOption` / `EffectiveIncludeEmotions` | `bool` | See below |
| `LicenseText` | `string` | "{attribution} - {license}" |
| `ConsentNotice` | `string` | The voice-cloning consent note |
| `State` | `SpeechModelState` | The install lifecycle state |
| `ProgressFraction` | `double?` | Install progress, when known |
| `FailureMessage` | `string?` | The explanation for a failed attempt |
| `StatusText` | `string` | Not installed, Installing, Installed (with emotions / voices only), Failed or corrupt |
| `CanInstall` / `CanRemove` / `IsInstalling` / `HasFailureMessage` | `bool` | Derived enablement and visibility |

**Key Methods**: None beyond the constructor (copies a `ReferenceVoicePack`) and property setters,
which announce the derived values when `State` or `FailureMessage` changes.

`CanInstall` is true for `NotDownloaded` and `FailedOrCorrupt` (so a transient failure can be
retried) and for a `Downloaded` pack that lacks emotions (the "Add emotions" upgrade); `CanRemove` is true only for `Downloaded`.

**Error Handling**: The constructor throws `ArgumentNullException` for a null pack; otherwise this
unit never throws.

**Dependencies**: The library's `ReferenceVoicePack` and `SpeechModelState`. Never touches the
seam, the file system, the network, or Avalonia.

**Callers**: `ModelCatalogViewModel` (owns the `VoicePacks` collection of these rows).
