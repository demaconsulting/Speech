### DataManifest

**Purpose**: Verify that the files of the data folder are exactly the files that the committed `MANIFEST.tsv` lists,
with the hash, size and row count recorded for each. The manifest is a reviewed file; this unit is what makes a change
to a data file that nobody reviewed fail the build.

**Data Model**: `DataManifest` holds the well-formed `Entries` of the manifest, each a `ManifestEntry` read-only record
of the manifest line, the relative path (with forward slashes), the SHA-256 in lowercase hexadecimal, the byte count and
the row count. The manifest is a tab-separated file without a header, with leading `#` comment lines (comments are not
hashed and are not entries) and the columns file, sha256, bytes and rows. A row count is the number of lines minus the
header for a `.tsv` file, and the number of lines for any other text file. The excluded names are the manifest itself
and the two local cache folders `external/` and `work/`. Its findings use the check name `manifest`.

**Key Methods**:

- **Load(root, report)**: reads the manifest through `TsvTable` with comments allowed and without a header, and parses
  each line; a line that does not have four columns, whose hash is not 64 lowercase hexadecimal digits or whose counts
  are not integers is a finding with its line and is not an entry.
- **Verify(root, report)**: loads the manifest and then checks it: the entries must be unique and in ordinal path order
  (a finding for each violation); each entry's path must be a relative path with forward slashes below the folder, with
  no empty, `.` or `..` segment; the file must exist, and its SHA-256, byte count and row count must equal the entry's,
  each difference being a finding that names the file and both values; finally every file under the folder that no
  entry lists is a finding. A missing manifest is one finding. Findings are in the order of the manifest and then of
  the unlisted files in ordinal order, so the output is stable.
- **EnumerateDataFiles(root)**: every file under the folder as a relative path with forward slashes in ordinal order,
  without the manifest and the cache folders. `LicenseChecker` and `TextEnDataSet` use it so all units see the same
  files.
- **CountLines(bytes)**: the number of line terminators plus one for an unterminated last line.

This unit only verifies; it never writes the manifest, which is produced by a one-off tool that is not part of the
repository.

**Error Handling**: every defect of the manifest or of a listed file is a finding. Input and output exceptions while
reading a file propagate to `CheckDataCommand` (exit code 2).

**Dependencies**: `TsvTable` and `CheckReport`; `System.Security.Cryptography.SHA256` from the base class library.

**Callers**: `CheckDataCommand` (first check), `TextEnDataSet`, `LicenseChecker` and the unit tests.
