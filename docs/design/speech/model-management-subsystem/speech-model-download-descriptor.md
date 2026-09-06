### SpeechModelDownloadDescriptor

**Purpose**: Describe the complete, ordered set of files that make up one model's downloadable
payload.

**Data Model**: `Files` (an ordered `IReadOnlyList<SpeechModelDownloadFile>`). A non-positional
`sealed record` with an explicit constructor.

**Key Methods**:

- **SpeechModelDownloadDescriptor(Files)**: Validates the file list eagerly - must be non-null,
  non-empty, and free of two entries sharing the same
  `SpeechModelDownloadFile.RelativeInstallPath` (which would silently clobber one another during
  install). Declaration order is preserved and is the order `SpeechModelDownloader` fetches and
  verifies each file in.

**Error Handling**: Throws `ArgumentException` for an empty or duplicate-path file list, and
`ArgumentNullException` for a null list. Each individual `SpeechModelDownloadFile` has already
validated its own URI/checksum/path at construction; this type only adds the cross-file
duplicate-path check.

**Dependencies**: `SpeechModelDownloadFile`.

**Callers**: `SpeechModelDownloader.DownloadAsync(...)`, which iterates `Files` in order.
