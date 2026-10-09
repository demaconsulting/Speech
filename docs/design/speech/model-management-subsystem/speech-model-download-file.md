### SpeechModelDownloadFile

**Purpose**: Describe one downloadable file belonging to a model's download payload: where to
fetch it from, the SHA-256 checksum it must match, and where it lands once verified.

**Data Model**: `Uri` (the HTTPS source location), `Sha256Checksum` (a 64-character hexadecimal
digest), `RelativeInstallPath` (a relative path within the model's installed directory, with at
least one named segment and free of parent-directory (`..`) and current-directory (`.`)
segments). A non-positional `sealed record` with an explicit constructor.

**Key Methods**:

- **SpeechModelDownloadFile(Uri, Sha256Checksum, RelativeInstallPath)**: Validates every field
  eagerly - `Uri` must be an absolute HTTPS URI, `Sha256Checksum` must be exactly 64 hexadecimal
  characters, and `RelativeInstallPath` must be non-empty, non-rooted, free of parent-directory
  (`..`) and current-directory (`.`) segments, and normalize to at least one named segment - a
  `..` segment could escape the model's own installed directory, a `.` segment would otherwise
  silently collapse away under `Path.Combine`/`Uri` canonicalization (letting two declared paths
  that look distinct collide at the same staged file), and a path made up entirely of separators
  (e.g. `/`) has no named segment at all and would otherwise resolve to the staging directory
  itself.
- **SplitRelativeInstallPathSegments(string)** *(internal)*: Splits an already-validated
  `RelativeInstallPath` into its non-empty segments on either separator (`/` or `\`). The single
  shared implementation `SpeechModelDownloadDescriptor` (duplicate-path detection) and
  `SpeechModelDownloader` (staging and mirror-URI resolution) both use, so the two separators -
  and the `.`/`..`/empty-segment rejection enforced once, here, at construction - can never drift
  apart between consumers.
- **ResolveStagedPath(stagedFilesDirectory, relativeInstallPath)** *(public)*: Combines a staging
  (or installed) directory with a declared `RelativeInstallPath`'s segments using this platform's
  own `Path.DirectorySeparatorChar`, regardless of which separator the path was declared with.
  `SpeechModelDownloader` uses this to compute each file's staging destination; an
  `ISpeechModel.InstallAsync` override that needs to locate one of its own declared files within
  `stagedFilesDirectory` must use this same method rather than a raw `Path.Join`/string
  concatenation, so the hook always agrees with the downloader on the exact staged path.

**Error Handling**: Throws `ArgumentException` for any field that fails validation, and
`ArgumentNullException` for any null argument (including to `ResolveStagedPath`). Validation
happens eagerly at construction so an invalid descriptor is rejected the moment it is created,
not silently accepted and only discovered mid-download.

**Dependencies**: None beyond the .NET base class library.

**Callers**: `SpeechModelDownloadDescriptor` (as its file list); `SpeechModelDownloader` (reads
`Uri`, `Sha256Checksum`, and `RelativeInstallPath` while fetching and verifying each file, and
calls `ResolveStagedPath` to compute each file's staging destination); any `ISpeechModel`
implementation whose `InstallAsync` override needs to locate its own declared file(s) within the
staging directory.
