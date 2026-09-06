### TarBz2ArchiveExtractor Verification

#### Verification Approach

`TarBz2ArchiveExtractor` is verified through deterministic unit tests using small, synthetic
`.tar.bz2` archives built in-memory (via a temp-file-based `TarBz2ArchiveFixtures` helper, since
`SharpCompress.Compressors.BZip2.BZip2Stream` closes its underlying stream on dispose), never
the real multi-hundred-megabyte production model archives.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Each test uses a unique scratch directory under `Path.GetTempPath()`

#### Acceptance Criteria

A single-entry archive extracts its entry's content under the archive's own top-level folder and
the archive file is deleted afterward; a multi-entry, multi-directory archive extracts every
entry; an empty archive path or empty destination directory throws `ArgumentException` before any
extraction is attempted.

#### Test Scenarios

##### A single-entry archive extracts its entry and the archive file is deleted afterward

**Test**: `TarBz2ArchiveExtractor_ExtractAndDeleteAsync_SingleEntryArchive_ExtractsEntryAndDeletesArchive`

##### A multi-entry, multi-directory archive extracts every entry

**Test**: `TarBz2ArchiveExtractor_ExtractAndDeleteAsync_MultiEntryArchive_ExtractsEveryEntry`

##### An empty archive path throws ArgumentException

**Test**: `TarBz2ArchiveExtractor_ExtractAndDeleteAsync_EmptyArchivePath_ThrowsArgumentException`

##### An empty destination directory throws ArgumentException

**Test**: `TarBz2ArchiveExtractor_ExtractAndDeleteAsync_EmptyDestinationDirectory_ThrowsArgumentException`
