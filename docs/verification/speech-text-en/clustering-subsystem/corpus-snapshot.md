### CorpusSnapshot Verification

#### Verification Approach

`CorpusSnapshot` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ClusteringSubsystem/CorpusSnapshotTests.cs`. The tests build archives
from a fixed source list and two fixed bodies in a temporary folder and inspect them with the standard tar reader. A
hostile archive is written with the standard tar writer. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test class instance has its own temporary folder, deleted on disposal

#### Acceptance Criteria

**Reproducible.** The archive of the fixed inputs has the recorded SHA-256 on every platform; two builds are byte-equal;
the size is a whole number of 10,240-byte records with four files; the input order does not matter; the members are in
the fixed order with format ustar, time 0, owner and group 0 and mode 0644. Measuring the same inputs gives the golden
SHA-256, size and file count of the built archive, an empty path and no file.

**Read.** A built archive reads back with the source list, the bodies and the notice; an archive whose member escapes
the folder, a missing file and a file that is not an archive are refused.

#### Test Scenarios

##### Reproducible

**Tests**: `CorpusSnapshot_Build_FixedInputs_GoldenShaAndByteEqual`, `CorpusSnapshot_Build_ReversedInput_SameArchive`,
`CorpusSnapshot_Build_HeaderFields_AreFixed`, `CorpusSnapshot_Measure_FixedInputs_SameIdentityWithoutWriting`

The archive's SHA-256 equals the golden value, the second build has the same bytes, the reported hash equals the file's
and the size is a multiple of 10,240. Bodies given in reverse order give the same hash. The members are `NOTICE.txt`,
`cluster-sources.tsv`, `bodies/gutenberg-10.txt` and `bodies/gutenberg-2.txt` (ordinal order), each with the fixed
header fields. Measuring the reversed bodies gives the golden hash and the built archive's size and file count with an
empty path, and the temporary folder holds only the built archive.

##### Read

**Tests**: `CorpusSnapshot_Read_BuiltArchive_ReturnsMembers`, `CorpusSnapshot_Read_UnsafeOrMissing_Throws`

The read members equal the inputs and the notice names two books. A member `../escape.txt`, a missing path and a file of
1,024 letters each throw `InvalidDataException`.
