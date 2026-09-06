### ModelListItemViewModel

**Purpose**: Present one catalog row's identity, role, install state, download progress, and
failure explanation, plus the derived captions and enablement values the row displays.

**Data Model**:

| Member | Type | Purpose |
| --- | --- | --- |
| `Id` / `DisplayName` / `Role` | descriptor values | The model's identity and role |
| `State` | `SpeechModelState` | The install lifecycle state |
| `ProgressFraction` | `double?` | Download progress, when known |
| `FailureMessage` | `string?` | The explanation for a failed attempt |
| `StatusText` | `string` | Human-readable caption for `State` |
| `CanDownload` | `bool` | True only for `NotDownloaded` and `FailedOrCorrupt` |
| `IsDownloading` | `bool` | Whether to show the progress bar |
| `HasFailureMessage` | `bool` | Whether to show the failure text |

**Key Methods**: This unit exposes no behavioral methods beyond its constructor (which copies a
`SpeechModelDescriptor`'s identity/role/state) and its property setters, which announce derived
values on change.

`CanDownload` deliberately excludes `Downloaded`, so working installed content cannot be
disturbed, and excludes `Downloading`, so a second attempt cannot be started over an in-flight
one. It includes `FailedOrCorrupt`, because most download failures are transient and a retry must
remain possible. Changing `State` re-announces `StatusText`, `CanDownload`, and `IsDownloading`
together so a bound row cannot go stale mid-download.

**Error Handling**: The constructor throws `ArgumentNullException` for a null descriptor;
otherwise this unit never throws.

**Dependencies**: The library's `SpeechModelDescriptor`, `SpeechModelRole`, `SpeechModelState`
value types. Never touches the seam, the file system, the network, or Avalonia.

**Callers**: `ModelCatalogViewModel` (owns the `Models` collection of these rows).
