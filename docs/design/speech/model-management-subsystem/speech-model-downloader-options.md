### SpeechModelDownloaderOptions

**Purpose**: Let a host configure how `SpeechModelDownloader` fetches a model's declared files -
currently, whether to redirect every download through an internal `DownloadMirror` - without any
library code change.

**Data Model**: `Mirror` (optional `DownloadMirror?`, `null` by default) - when non-null,
`SpeechModelDownloader` resolves each file's effective request URI beneath it and applies its
configured authentication to requests sent to it.

**Key Methods**: A plain options record with a single settable property; no behavior of its own.

**Error Handling**: None - validation of a supplied `Mirror` happens eagerly in `DownloadMirror`'s
own constructor, never in this options type itself.

**Dependencies**: `DownloadMirror`.

**Callers**: Hosts constructing a `SpeechModelDownloader`; `SpeechModelDownloader`'s constructor
reads `Mirror`.
