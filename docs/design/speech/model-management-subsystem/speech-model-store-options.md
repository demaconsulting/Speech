### SpeechModelStoreOptions

**Purpose**: Let a host override `SpeechModelStore`'s default per-user storage root without any
library code change.

**Data Model**: `RootPathOverride` (optional `string?`, `null` by default) - when non-null and
non-empty, `SpeechModelStore` uses this path verbatim instead of resolving under
`Environment.SpecialFolder.LocalApplicationData`.

**Key Methods**: A plain options record/class with a single settable property; no behavior of
its own.

**Error Handling**: None - validation of the resolved root happens lazily in `SpeechModelStore`
(directories are created on first write), never in this options type itself.

**Dependencies**: None.

**Callers**: Hosts constructing a `SpeechModelStore`; `SpeechModelStore`'s constructor reads
`RootPathOverride`.
