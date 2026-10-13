### ReferenceVoicePackListItemViewModel

**Purpose**: Present one reference voice pack row in the model catalog panel: its identity, size,
license and attribution, install state, progress and failure explanation, plus the derived
captions and enablement values the row displays.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `Id` / `DisplayName` | `string` | The pack identity (for example `voice-zero-1.0`) |
| `SizeText` | `string` | The download size, for example "Download: 570 MiB" (the size comes from the pack descriptor, not from the view model) |
| `HasEmotionVariants` | `bool` | Whether the pack ships emotion clips (`EmotionsBytes` is greater than zero) |
| `VoicesBytes` / `EmotionsBytes` | `long` (private) | Voices-only and emotion-clip sizes copied from the pack |
| `IncludeEmotions` | `bool` | The user's choice to install the emotion variants (default `true`) |
| `HasEmotions` | `bool` | Whether the installed pack was extracted with its emotion clips (constructor `hasEmotions`) |
| `InstalledSizeText` | `string` | Empty when `VoicesBytes` is not positive; else "Uses"/"Installs about N MiB" |
| `InstallButtonText` | `string` | "Add emotions" for a voices-only install, otherwise "Install" |
| `CanUpgradeToEmotions` | `bool` | `State` is `Downloaded`, the pack has emotion variants, and the install lacks them |
| `ShowEmotionOption` | `bool` | Pack has emotion variants and `State` is `NotDownloaded` or `FailedOrCorrupt` |
| `EffectiveIncludeEmotions` | `bool` | Install argument: needs `HasEmotionVariants`, then upgrade/`IncludeEmotions` |
| `LicenseText` | `string` | "{attribution} - {license}" |
| `ConsentNotice` | `string` | The voice-cloning consent note |
| `State` | `SpeechModelState` | The install lifecycle state |
| `ProgressFraction` | `double?` | Install progress, when known |
| `FailureMessage` | `string?` | The explanation for a failed attempt |
| `StatusText` | `string` | "Not installed", "Installing", "Installed" (with or without emotions), "Failed or corrupt" |
| `CanInstall` / `CanRemove` / `IsInstalling` / `HasFailureMessage` | `bool` | Derived enablement and visibility |

The property-change announcements are:

| Property set | Also announces |
| --- | --- |
| `State` | `StatusText`, `CanInstall`, `CanRemove`, `IsInstalling`, `CanUpgradeToEmotions`, and more (below) |
| `IncludeEmotions` | `EffectiveIncludeEmotions` |
| `HasEmotions` | `StatusText`, `CanUpgradeToEmotions`, `InstallButtonText`, `EffectiveIncludeEmotions` |
| `FailureMessage` | `HasFailureMessage` |

`State` additionally announces `ShowEmotionOption`, `InstallButtonText` and
`EffectiveIncludeEmotions`.

**Key Methods**: None beyond the constructor
`ReferenceVoicePackListItemViewModel(ReferenceVoicePack pack, SpeechModelState state,
bool hasEmotions = false)`, which copies the pack's identity, sizes and license and sets the initial
state and emotion status, and the property setters, which announce the derived values when `State`,
`IncludeEmotions`, `HasEmotions` or `FailureMessage` changes (including `EffectiveIncludeEmotions`,
which depends on all three of `State`, `IncludeEmotions` and `HasEmotions`).

`CanInstall` is true for `NotDownloaded` and `FailedOrCorrupt` (so a transient failure can be
retried) and for a `Downloaded` pack that lacks emotions (the "Add emotions" upgrade); `CanRemove`
is true only for `Downloaded`.
The panel-level "voice busy" message that explains a refused pack operation belongs to
`ModelCatalogViewModel`, not to this row.

**Error Handling**: The constructor throws `ArgumentNullException` for a null pack; otherwise this
unit never throws.

**Dependencies**: The library's `ReferenceVoicePack` and `SpeechModelState`, and the static
`ReferenceVoiceLibrary.ConsentNotice` constant. Never touches the
seam, the file system, the network, or Avalonia.

**Callers**: `ModelCatalogViewModel` (owns the `VoicePacks` collection of these rows).
