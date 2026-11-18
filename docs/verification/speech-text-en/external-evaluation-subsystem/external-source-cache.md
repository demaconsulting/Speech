### ExternalSourceCache Verification

#### Verification Approach

`ExternalSourceCache` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/ExternalSourceCacheTests.cs`. The tests
use the external source fixture (`Fakes/ExternalSourceFixture.cs`), which builds hand-written fake dataset files, packs
them into an in-memory compressed archive and writes the pins that describe them exactly, with the real SHA-256 hashes
and sizes. The network is the one injected dependency: the fetcher delegate serves the fake archive, counts its calls
and can fail. The defects are written into copies of the fixture: an archive whose file differs, lacks a pinned file,
holds an unpinned entry, an entry that tries to leave the folder or an entry larger than its pin, a cache with one byte
changed or resized, and malformed pins. The committed pins are loaded once, because the pins themselves define the
evaluation. Nothing else is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test has its own temporary folders, deleted on disposal
- **Not used**: the network and any real dataset. One test reads the committed pins files above the test binaries

#### Acceptance Criteria

**Pins.** The committed pins load and pin the two sources with only the files the evaluation reads; a per-file list
edited without the pins file, a missing pins file and each malformed pin are refused with a message that names the
problem.

**Verified fetch.** A valid archive is published with exactly the pinned files; an archive that lacks a pinned file,
whose file has another hash or whose entry is larger than its pin is refused and leaves nothing in the cache; a failed
download leaves an empty or damaged cache as it was; a file that was modified, resized or is not pinned is refused at
reading, naming the file.

**Cache reuse.** A valid cache is not fetched again even when a fetch is allowed; a missing or damaged cache without
permission to fetch is refused, the message says to fetch and the fetcher is never called; a damaged cache with
permission is replaced by a verified download.

#### Test Scenarios

##### Pins

**Tests**: `ExternalSourcePins_Load_CommittedConfig_IsConsistent`,
`ExternalSourcePins_Load_EditedFilesTsv_FailsConsistencyCheck`, `ExternalSourcePins_Load_MalformedPins_Throws` (a
theory over eight defects: short commit, parent, absolute and backslash paths, HTTP archive address, unsorted rows,
wrong schema and wrong header), `ExternalSourcePins_Load_InvalidUtf8InFilesTsv_ThrowsNamingFilesTsv`,
`ExternalSourcePins_Load_InvalidUtf8InJson_ThrowsNamingJson`, `ExternalSourcePins_Load_InvalidByteCount_Throws` (a
theory over eight byte counts: negative, signed, not a number, beyond 64 bits and beyond 16 MiB),
`ExternalSourcePins_Load_MissingFile_Throws`

Each edit of one pins file, and each malformed pin, throws `InvalidDataException` with a message that contains the
expected text.

##### Verified fetch

**Tests**: `ExternalSourceCache_EnsureCached_ValidArchive_ExtractsOnlyPinnedFiles`,
`ExternalSourceCache_EnsureCached_MissingPinnedFile_Throws`,
`ExternalSourceCache_EnsureCached_FetchFails_LeavesCacheUnchanged`,
`ExternalSourceCache_EnsureCached_Sha256Mismatch_ThrowsAndLeavesNothingInCache`,
`ExternalSourceCache_EnsureCached_OversizedEntry_Throws`,
`ExternalSourceCache_ReadAllBytes_ModifiedFileOrUnpinnedPath_Throws`

A valid archive gives a cache that holds the pinned files and nothing else, even though the archive also holds an
unpinned file and entries that try to leave the folder. A missing pinned file, a hash mismatch (the message names the
file and both hashes), an oversized entry or a failing fetcher throws and the cache folder is as it was. Reading refuses
a modified file, a resized file and a path that is not pinned.

##### Cache reuse

**Tests**: `ExternalSourceCache_EnsureCached_CacheAlreadyValid_DoesNotFetch`,
`ExternalSourceCache_EnsureCached_DamagedCacheWithFetch_IsReplacedByVerifiedDownload`,
`ExternalSourceCache_EnsureCached_NoCacheAndNoFetch_ThrowsWithoutFetching`,
`ExternalSourceCache_EnsureCached_DamagedCacheAndNoFetch_ThrowsWithoutFetching`

The fetcher's call count is zero when the cache is valid and when a fetch is not allowed, and the message of a refusal
says to run with the fetch option; a damaged cache with permission is replaced after one fetch.
