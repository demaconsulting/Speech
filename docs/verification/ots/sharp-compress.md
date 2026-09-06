## SharpCompress Verification

This document provides the verification evidence for the SharpCompress OTS software item.
Requirements for this OTS item are defined in the SharpCompress OTS Software Requirements
document.

### Required Functionality

SharpCompress provides the managed BZip2 tar (`.tar.bz2`) decoder used by the internal
`TarBz2ArchiveExtractor` helper. In this phase, the library relies on it to unpack both new
recognition models' downloaded archives after checksum verification.

### Verification Approach

Automated verification for this OTS item uses small, deterministic, in-memory `.tar.bz2`
fixtures built with SharpCompress's own writer APIs (`TarWriter`/`BZip2Stream`), so every
scenario runs without any real network access or the multi-hundred-megabyte production model
downloads this package ultimately unpacks in production:

- A single-entry archive extracts its entry's content and preserves the archive's own top-level
  folder structure
- A multi-entry, multi-directory archive extracts every entry, not just the first
- Extraction removes the archive file only after every entry succeeds
- Invalid arguments (empty archive path, empty destination directory) are rejected eagerly

Automated tests do **not** claim proof of every archive shape SharpCompress can decode; they
prove the one shape (`.tar.bz2` with `ExtractFullPath`/`Overwrite` extraction options) this
library actually relies on.

### Test Scenarios

#### TarBz2ArchiveExtractor_ExtractAndDeleteAsync_SingleEntryArchive_ExtractsEntryAndDeletesArchive

**Scenario**: A synthetic single-entry `.tar.bz2` archive is extracted into a destination
directory.

**Expected**: The entry's content is written under the archive's own top-level folder, and the
archive file itself is deleted afterward.

**Requirement coverage**: `Speech-OTS-SharpCompress-TarBz2Extraction`.

#### TarBz2ArchiveExtractor_ExtractAndDeleteAsync_MultiEntryArchive_ExtractsEveryEntry

**Scenario**: A synthetic archive containing multiple files across nested folders is extracted.

**Expected**: Every entry is written to its own relative path under the destination directory.

**Requirement coverage**: `Speech-OTS-SharpCompress-TarBz2Extraction`.

#### TarBz2ArchiveExtractor_ExtractAndDeleteAsync_EmptyArchivePath_ThrowsArgumentException

**Scenario**: `ExtractAndDeleteAsync` is called with an empty archive path.

**Expected**: `ArgumentException` is thrown before SharpCompress is invoked.

**Requirement coverage**: `Speech-OTS-SharpCompress-ArgumentValidation`.

#### TarBz2ArchiveExtractor_ExtractAndDeleteAsync_EmptyDestinationDirectory_ThrowsArgumentException

**Scenario**: `ExtractAndDeleteAsync` is called with an empty destination directory.

**Expected**: `ArgumentException` is thrown before SharpCompress is invoked.

**Requirement coverage**: `Speech-OTS-SharpCompress-ArgumentValidation`.

### Requirements Coverage

- **`Speech-OTS-SharpCompress-TarBz2Extraction`**:
  `TarBz2ArchiveExtractor_ExtractAndDeleteAsync_SingleEntryArchive_ExtractsEntryAndDeletesArchive`,
  `TarBz2ArchiveExtractor_ExtractAndDeleteAsync_MultiEntryArchive_ExtractsEveryEntry`
- **`Speech-OTS-SharpCompress-ArgumentValidation`**:
  `TarBz2ArchiveExtractor_ExtractAndDeleteAsync_EmptyArchivePath_ThrowsArgumentException`,
  `TarBz2ArchiveExtractor_ExtractAndDeleteAsync_EmptyDestinationDirectory_ThrowsArgumentException`
