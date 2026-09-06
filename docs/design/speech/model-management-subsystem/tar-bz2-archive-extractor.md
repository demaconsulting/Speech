### TarBz2ArchiveExtractor

**Purpose**: Provide the shared `.tar.bz2` archive-extraction helper both new recognition
models' `InstallAsync` implementations use, since the BCL has no BZip2 decoder.

**Data Model**: N/A - a stateless internal static helper class with a single entry point.

**Key Methods**:

- **ExtractAndDeleteAsync(archivePath, destinationDirectory, cancellationToken)**: Opens
  `archivePath` and reads it via `SharpCompress.Readers.ReaderFactory.Open` (which auto-detects
  the BZip2-wrapped tar container), iterating entries with `MoveToNextEntryAsync`, skipping
  directory entries (SharpCompress creates any needed directory as a side effect of
  `ExtractionOptions.ExtractFullPath = true`), and writing each file entry to
  `destinationDirectory` via `WriteEntryToDirectoryAsync`, preserving the archive's own relative
  folder structure. Deletes the archive file only after every entry has extracted successfully.

**Error Handling**: Throws `ArgumentException` for a null or empty `archivePath` or
`destinationDirectory`. Any exception raised while opening, reading, or writing an entry
propagates unchanged to the caller and leaves the archive file in place - irrelevant in practice,
since the whole staging directory (archive included) is discarded by
`SpeechModelDownloader`/`SpeechModelStore` on any install-hook failure, identically to a download
failure. Propagates `OperationCanceledException` when `cancellationToken` is canceled mid-extraction.

**Dependencies**: `SharpCompress.Readers.ReaderFactory`/`IReaderExtensions` (see
`docs/design/ots/sharp-compress.md`), `System.IO.File`.

**Callers**: `SherpaOnnxZipformerEnRecognitionModel.InstallAsync`,
`SherpaOnnxNemotronStreamingEnRecognitionModel.InstallAsync`.
