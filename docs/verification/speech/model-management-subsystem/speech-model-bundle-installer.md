### SpeechModelBundleInstaller

#### Verification Approach

Unit-tested in `SpeechModelBundleInstallerTests.cs` by calling the internal `InstallAsync` directly on small
synthetic bundle zips built in memory with `System.IO.Compression` by `Fakes/ModelBundleTestZips.cs` and staged in
a temporary directory. Each malformed case starts from a well-formed entry list (README.txt, LICENSE, NOTICE,
`model.json`, `onnx/model.onnx`, `voices/af.bin`) and changes one thing: an added, removed or replaced entry, a
replaced `model.json`, or a different compiled pin set. The real `ZipEntryPathValidator` and `SpeechModelBundle`
(documented dependencies) are used, not mocked. No network access is used.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **File system**: a temporary directory under the system temp path, created per test and deleted afterward

#### Acceptance Criteria

Tests pass when a valid bundle installs exactly its vendor files (exact bytes) and its `licenses/{name}/` folder
and deletes the zip; NOTICE is optional and directory entries are ignored; and every unsafe or non-portable entry
name, case-only duplicate, unexpected or missing entry, missing metadata entry, malformed or oversized `model.json`,
size mismatch, SHA-256 mismatch against `model.json` or the compiled pin, pinned file the bundle lacks, declared file
without a pin, and non-zip file throws `InvalidDataException`. For unsafe names nothing but the staged zip may exist
afterwards, inside or outside the staging directory; a canceled install throws `OperationCanceledException` and
keeps the zip.

#### Test Scenarios

##### Valid Bundle

**Test**: `SpeechModelBundleInstaller_InstallAsync_ValidBundle_ExtractsVendorFilesAndLicenses`

**Test**: `SpeechModelBundleInstaller_InstallAsync_WithoutNotice_Installs`

**Test**: `SpeechModelBundleInstaller_InstallAsync_DirectoryEntries_AreIgnored`

##### Entry Name Safety (Zip-Slip)

**Test**: `SpeechModelBundleInstaller_InstallAsync_EntryEscapingDirectory_ThrowsInvalidDataException` (`../x`,
`onnx/../../x`, `/abs`, `a\b`, `C:x`, `onnx/./x` and `nul.txt`, each appended last so a writer that did not
validate first would already have written files)

**Test**: `SpeechModelBundleInstaller_InstallAsync_DuplicateEntryDifferingInCase_ThrowsInvalidDataException`

##### Tampered Vendor Files

**Test**: `SpeechModelBundleInstaller_InstallAsync_FileHashDiffersFromModelJson_ThrowsInvalidDataException`
(same-length tampered bytes)

**Test**: `SpeechModelBundleInstaller_InstallAsync_FileHashDiffersFromPinnedContents_ThrowsInvalidDataException`
(a self-consistent bundle whose vendor file differs from the compiled pin)

**Test**: `SpeechModelBundleInstaller_InstallAsync_EntrySizeDiffersFromDeclared_ThrowsInvalidDataException`
(declared size one byte smaller - the zip-bomb cap - and one byte larger)

##### Exact Entry Set And Pin Coverage

**Test**: `SpeechModelBundleInstaller_InstallAsync_EntryNotInModelJson_ThrowsInvalidDataException`

**Test**: `SpeechModelBundleInstaller_InstallAsync_DeclaredEntryMissing_ThrowsInvalidDataException`

**Test**: `SpeechModelBundleInstaller_InstallAsync_PinnedFileMissing_ThrowsInvalidDataException`

**Test**: `SpeechModelBundleInstaller_InstallAsync_DeclaredFileWithoutPin_ThrowsInvalidDataException`

##### Metadata And model.json

**Test**: `SpeechModelBundleInstaller_InstallAsync_MissingMetadataEntry_ThrowsInvalidDataException` (`model.json`,
`LICENSE`, `README.txt`)

**Test**: `SpeechModelBundleInstaller_InstallAsync_MalformedModelJson_ThrowsInvalidDataException` (not JSON, a JSON
array, another bundle's name, schema version 2, an empty file list, a path under `licenses/`, a malformed SHA-256,
a negative size)

**Test**: `SpeechModelBundleInstaller_InstallAsync_OversizedMetadataEntry_ThrowsInvalidDataException` (also asserts
the 1 MiB cap constant)

##### Archive Handling

**Test**: `SpeechModelBundleInstaller_InstallAsync_NotAZip_ThrowsInvalidDataException`

**Test**: `SpeechModelBundleInstaller_InstallAsync_CanceledToken_ThrowsAndKeepsZip`
