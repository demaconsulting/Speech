### SpeechModelDownloaderOptions

**Purpose**: Let a host configure how `SpeechModelDownloader` fetches a model's declared files -
currently, whether to redirect every download through an internal `DownloadMirror` - without any
library code change.

**Data Model**: `Mirror` (optional `DownloadMirror?`, `null` by default) - when non-null,
`SpeechModelDownloader` resolves each file's effective request URI beneath it and applies its
configured authentication to requests sent to it.

**Key Methods**: N/A - a plain options record with a single init-only property and no behavior of its own.

**Error Handling**: N/A - this type performs no validation or I/O; validation of a supplied `Mirror` happens
eagerly in `DownloadMirror`'s own constructor, never in this options type itself.

**Thread Safety**: Instances are immutable after initialization, so one instance can be shared freely between
threads and downloaders. The mirror's optional `NetworkCredential` is mutable and owned by the caller, who must not
modify it after supplying it.

**Dependencies**: `DownloadMirror`.

**Callers**: Hosts constructing a `SpeechModelDownloader`; `SpeechModelDownloader`'s constructor
reads `Mirror`.
