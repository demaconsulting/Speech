### SpeechModelStoreOptions

**Purpose**: Let a host override `SpeechModelStore`'s default per-user storage root without any
library code change.

**Data Model**: `RootPathOverride` (optional `string?`, `init`-only, `null` by default) - when it contains at least
one non-whitespace character, `SpeechModelStore` uses this path verbatim (no trimming or normalization) instead
of resolving under `Environment.SpecialFolder.LocalApplicationData`. A null, empty, or whitespace-only value is
treated as "no override" and selects the default root (the decision resolves the earlier requirement/design
mismatch: whitespace-only is the same as unset).

**Key Methods**: A plain options class with a single `init`-only property; no behavior of
its own. The null/empty/whitespace decision is made by the `SpeechModelStore` constructor.

**Error Handling**: None - this type has no error path and never throws. Validation of the resolved root happens
lazily in `SpeechModelStore` (directories are created on first write); an unwritable override surfaces as a
first-use I/O failure from the store, never from this options type.

**Dependencies**: None.

**Callers**: Hosts constructing a `SpeechModelStore`; `SpeechModelStore`'s constructor reads
`RootPathOverride`.
