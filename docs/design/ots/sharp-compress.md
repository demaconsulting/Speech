## SharpCompress

### Purpose

SharpCompress is used as the managed BZip2-compressed tar (`.tar.bz2`)
archive reader of the SpeechSherpa library and of the SpeechOnnxPiper library. It was chosen because the .NET BCL has no
BZip2 decoder, and sherpa-onnx's own official
GitHub Releases publish its ASR model archives (including the Zipformer and Nemotron models), and the archive of the
Piper voice that SpeechOnnxPiper installs, as
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

The SpeechSherpa library references `SharpCompress` directly from
`DemaConsulting.Speech.Sherpa.csproj` — the single source of truth for the exact pinned
version — chosen to avoid a moderate-severity `NU1902` advisory affecting older releases,
which this repository's build treats as an error. The internal `TarBz2ArchiveExtractor`
helper is the sole consumer of the package; public callers never reference `SharpCompress`
types directly, and the Speech library does not reference the package at all. Every SpeechSherpa model -
`SherpaOnnxZipformerEnRecognitionModel`, `SherpaOnnxNemotronStreamingEnRecognitionModel`,
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`, and `SherpaOnnxKokoroEnglishSynthesisModel` -
delegates its `InstallAsync` override to this helper to unpack their declared `.tar.bz2`
download payload after checksum verification and before `SpeechModelStore`'s atomic install
swap.

The SpeechOnnxPiper library also references `SharpCompress` directly from
`DemaConsulting.Speech.Onnx.Piper.csproj`, at the same pinned version, and uses only
`ReaderFactory.Open`, `IReader.MoveToNextEntryAsync` and `IReader.WriteEntryToAsync` in its internal
`PiperArchiveExtractor`. That helper reads the downloaded voice archive as a stream, extracts only the voice graph
(`.onnx`) and its configuration (`.onnx.json`) by bare file name into the model's install folder, and never writes any
other entry (the archive also holds espeak-ng data under a different license); an archive entry path therefore cannot
leave the install folder. It is the sole consumer of the package in that library, and no `SharpCompress` type appears
in its public API.
