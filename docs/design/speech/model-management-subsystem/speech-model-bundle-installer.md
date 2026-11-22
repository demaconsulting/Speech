### SpeechModelBundleInstaller

**Purpose**: The extraction and verification engine behind `SpeechModelBundle.InstallAsync` (mirroring the
`ReferenceVoicePack`/`ReferenceVoicePackInstaller` split): turn one verified, staged bundle zip into the model's
vendor files plus a `licenses/{Name}/` metadata folder, proving every vendor file is the unmodified upstream file,
then delete the zip.

**Data Model**: An internal static class with no state. Constants: the metadata entry names `model.json`,
`README.txt`, `LICENSE` and `NOTICE`; `RequiredMetadataEntryNames` (the first three) and `MetadataEntryNames` (all
four); `SupportedSchemaVersion` (1); `MaximumMetadataBytes` (1 MiB, applied to each metadata entry including
`model.json`). A private `ManifestFile(Path, Size, Sha256)` record holds one parsed `model.json` file entry.

**Bundle format** (what the bundling tool must produce):

- Entries: exactly the vendor files `model.json` lists (each at its install path, `/`-separated) plus
  `model.json`, `README.txt` and `LICENSE` at the root and, where the license requires one, `NOTICE`. Directory
  entries are allowed and ignored.
- `model.json`: UTF-8 JSON, at most 1 MiB, an object with `"schemaVersion": 1`, `"bundle"` equal to the bundle
  `Name`, and a non-empty `"files"` array of objects with `"path"` (string, canonical `/`-separated, not under
  `licenses/`, not a metadata name, not the zip name), `"size"` (non-negative integer) and `"sha256"` (64 hex).
  Every other property (for example `"sourceUrl"`, `"licenses"`, `"upstream"`, `"modelIds"`) is accepted and not
  interpreted.

**Key Methods**:

- **InstallAsync(bundle, stagingDirectory, cancellationToken)** *(internal)*: Yields once (the central-directory
  read is synchronous) and honors cancellation before touching the zip, then:
  1. Opens `{stagingDirectory}/{bundle.FileName}` with `ZipFile.OpenRead`; a file that is not a zip becomes an
     `InvalidDataException`.
  2. Validates **every** entry name before anything is written: `ZipEntryPathValidator.Validate`, a non-empty
     name, and `ZipEntryPathValidator.ResolveUnderRoot` against the canonical staging root. Directory entries are
     then skipped; file entries are indexed by their `/`-joined path, rejecting two that are equal ignoring case.
  3. Reads `model.json` with the 1 MiB cap and parses it with `System.Text.Json.JsonDocument` (schema version,
     bundle name, files; each path validated as above and the paths checked for collisions with
     `SpeechModelBundle.FindPathCollision`).
  4. Requires the file entries to be exactly the `model.json` paths plus the required metadata, with `NOTICE`
     optional; an unexpected or a missing entry fails.
  5. Requires the `model.json` file list to equal the compiled `Contents` exactly: every pinned file declared with
     the same SHA-256, and no declared file without a pin.
  6. Copies each vendor entry, in `model.json` order, to its resolved destination with `FileMode.CreateNew`,
     hashing it with `IncrementalHash` (SHA-256) while copying and stopping as soon as the bytes exceed the declared
     size (the decompression-bomb cap, applied to the bytes actually read). Afterwards the size must equal the
     declared size, and the hash must equal both the `model.json` SHA-256 and the compiled pin.
  7. Writes the parsed `model.json` bytes, `README.txt`, `LICENSE` and (if present) `NOTICE` to
     `licenses/{Name}/` with `FileMode.CreateNew`, each read with the 1 MiB cap.
  8. Deletes the zip, only after everything above succeeded.

  The zip is read with `System.IO.Compression` only.

**Error Handling**: A null bundle throws `ArgumentNullException`; a null or empty staging directory throws
`ArgumentException`. Every archive problem is an `InvalidDataException` naming the bundle and the offending entry:
not a zip, an unsafe or non-portable name, a resolved path outside the staging directory, a case-insensitive
duplicate, a missing or oversized `model.json`, a `model.json` that is not JSON or breaks a schema rule, an
unexpected or missing entry, a pin set that differs from `model.json`, a size or SHA-256 mismatch, or an oversized
metadata entry. A missing or unreadable zip, or a file that already exists at a destination, surfaces as the file
system's `IOException`; cancellation as `OperationCanceledException`. Nothing is handled locally: the downloader
classifies the exception (`InvalidDataException` is `Failed`, `IOException` is `IoFailure`) and discards the
staging directory, so partly extracted files never reach `current/` and a prior install stays intact. The zip is
kept on failure.

**Dependencies**: `SpeechModelBundle`, `ZipEntryPathValidator`, `SpeechModelDownloadFile` (through
`SpeechModelBundle.NormalizePath`), `System.IO.Compression`, `System.Text.Json`, `System.Security.Cryptography`.

**Callers**: `SpeechModelBundle.InstallAsync` only.
