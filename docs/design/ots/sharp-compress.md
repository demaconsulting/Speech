## SharpCompress

### Purpose

SharpCompress is used as the Speech library's managed BZip2-compressed tar (`.tar.bz2`) archive
reader. It was chosen because the .NET BCL has no BZip2 decoder, and sherpa-onnx's own official
GitHub Releases publish its ASR model archives (including both models added in Phase 7a) as
`.tar.bz2`, so a maintained, permissively-licensed managed decoder is needed to unpack them
without shelling out to an external `tar`/`bzip2` executable.

### Features Used

- `SharpCompress.Readers.ReaderFactory.Open` to auto-detect and open a BZip2-wrapped tar stream
- `SharpCompress.Readers.IReader.MoveToNextEntryAsync` to enumerate archive entries
  asynchronously
- `SharpCompress.Readers.IReaderExtensions.WriteEntryToDirectoryAsync` with
  `ExtractionOptions { ExtractFullPath = true, Overwrite = true }` to extract each file entry
  while preserving the archive's own relative folder structure

### Integration Pattern

The library references `SharpCompress` directly from `DemaConsulting.Speech.csproj` — the single
source of truth for the exact pinned version — chosen to avoid a moderate-severity `NU1902`
advisory affecting older releases, which this repository's build treats as an error. The internal
`TarBz2ArchiveExtractor` helper is the sole
consumer of the package; public callers never reference `SharpCompress` types directly. Both
`SherpaOnnxZipformerEnRecognitionModel` and `SherpaOnnxNemotronStreamingEnRecognitionModel`
delegate their `InstallAsync` override to this helper to unpack their declared `.tar.bz2`
download payload after checksum verification and before `SpeechModelStore`'s atomic install
swap.
