### SpeechModelDownloadProgress

**Purpose**: Report progress of a single file transfer within a model download, suitable for
`IProgress<T>` consumption by a host progress bar.

**Data Model**: A positional `sealed record` with `FileIndex` (zero-based, within the parent
descriptor's file list), `FileCount` (total files for this model), `BytesTransferred`, and
`TotalBytes` (nullable - `null` when the server did not report `Content-Length`).

**Key Methods**:

- **FractionComplete** (computed property): Returns `BytesTransferred / TotalBytes` in the range
  `[0, 1]` when `TotalBytes` is known and non-zero, `1.0` when `TotalBytes` is exactly zero
  (rather than dividing by zero), and `null` when `TotalBytes` is unknown.

**Error Handling**: None - a pure, side-effect-free value type; `FractionComplete` never throws
for any combination of its inputs.

**Dependencies**: None.

**Callers**: `IModelDownloadClient` implementations construct instances with `FileIndex = 0`,
`FileCount = 1` for the single file they fetch; `SpeechModelDownloader` rewrites both fields to
the file's true position within the model's overall download before forwarding to a
caller-supplied `IProgress<SpeechModelDownloadProgress>`.
