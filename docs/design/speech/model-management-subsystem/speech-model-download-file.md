### SpeechModelDownloadFile

**Purpose**: Describe one downloadable file belonging to a model's download payload: where to
fetch it from, the SHA-256 checksum it must match, and where it lands once verified.

**Data Model**: `Uri` (the HTTPS source location), `Sha256Checksum` (a 64-character hexadecimal
digest), `RelativeInstallPath` (a relative, non-parent-escaping path within the model's installed
directory). A non-positional `sealed record` with an explicit constructor.

**Key Methods**:

- **SpeechModelDownloadFile(Uri, Sha256Checksum, RelativeInstallPath)**: Validates every field
  eagerly - `Uri` must be an absolute HTTPS URI, `Sha256Checksum` must be exactly 64 hexadecimal
  characters, and `RelativeInstallPath` must be non-empty, non-rooted, and free of parent-directory
  (`..`) segments.

**Error Handling**: Throws `ArgumentException` for any field that fails validation, and
`ArgumentNullException` for any null argument. Validation happens eagerly at construction so an
invalid descriptor is rejected the moment it is created, not silently accepted and only
discovered mid-download.

**Dependencies**: None beyond the .NET base class library.

**Callers**: `SpeechModelDownloadDescriptor` (as its file list); `SpeechModelDownloader` (reads
`Uri`, `Sha256Checksum`, and `RelativeInstallPath` while fetching and verifying each file).
