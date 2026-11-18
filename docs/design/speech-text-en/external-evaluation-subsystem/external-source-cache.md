### ExternalSourceCache

**Purpose**: Hold the pins of the external datasets, fetch a pinned revision into a local cache, verify every file
against its pin and hand out only verified bytes. It is the only unit that opens a file of the cache, and only a pinned
path, so nothing that is not exactly the pinned data can reach a conversion or an evaluation.

**Data Model** (internal types):

- `PinnedFile`: the relative `/`-separated path of a file inside the source, its lowercase hexadecimal SHA-256 and its
  size in bytes
- `ExternalSourcePin`: a source id (`whd` or `bench`), the repository address (documentation of origin), the
  40-character
  lowercase hexadecimal commit, the HTTPS address of the archive of that commit, the license statement, the hash of the
  canonical listing of the files and the pinned files in ordinal path order
- `ExternalSourcePins`: the sources in the order of the pins file, with `Load` and `Find`
- `ExternalSourceCache`: the cache folder and the **fetcher**, a delegate that opens an archive from an address and a
  cancellation token as a stream of a gzip-compressed archive. The default fetcher (`HttpArchiveFetcher`) accepts HTTPS
  addresses only and has a timeout of five minutes. Tests supply their own delegate, so no test reaches the network. The
  class is not thread-safe

**The pins.** The pins are two committed files in the trainer folder. `external-sources.json` has a schema number and,
for each source, its id, repository, commit, archive address, license, file count, total size and the SHA-256 of the
canonical listing; `external-files.tsv` has the header `source`, `path`, `sha256`, `bytes` and one row per pinned file,
sorted by source and then path in ordinal order. The canonical listing of a source is the text of one line
`path TAB sha256 LF` per file in that order. `Load` rejects pins unless the schema is the supported one, the source ids
are unique, every commit is 40 lowercase hexadecimal characters, every archive address is an HTTPS address that contains
its commit, every path is a safe relative path (letters, digits, dot, underscore, hyphen and slash; no empty, `.` or
`..` segment; no leading or trailing slash), every hash is 64 lowercase hexadecimal characters, the rows are unique and
sorted, and for every source the file count, the total size and the recomputed hash of the listing equal the values of
the pins file. Editing either file alone is therefore detected when the pins are loaded.

**The cache layout.** A source lives in `<cache>/<source id>/<commit>/<pinned path>`.

**Key Methods**:

- **ExternalSourcePins.Load(jsonPath)**: loads and validates the pins as above; the per-file list is the sibling
  `external-files.tsv`
- **EnsureCached(source, fetch)**: makes the source present and verified. A cache that verifies is left alone and the
  fetcher is not called. When it is missing or fails and `fetch` is false the call throws, saying that the cache is
  missing or fails verification and to run with `--fetch`. When `fetch` is true the source is downloaded: only the
  regular files of the archive whose path, after the single top-level folder is removed, is a pinned path are written,
  into a sibling folder named `<commit>.partial`, each limited to its pinned size plus one byte (an entry that is larger
  is an error); every other entry is ignored. Every pinned file is then verified in the partial folder; only when all
  are
  exact is an older folder deleted and the partial folder renamed to `<commit>`. On any failure the partial folder is
  deleted and the cache is as it was
- **ReadAllBytes(source, relativePath)**: refuses a path that is not pinned, verifies the file's size and SHA-256
  against the pin and only then returns its bytes, so a file that changed after the first check is still refused; the
  message names the source and the path and never any content

**Error Handling**: null arguments throw `ArgumentNullException`. A malformed or inconsistent pin, a missing cache that
may not be fetched, a missing or different pinned file, an archive entry that is larger than its pin and a download that
lacks a pinned file are `InvalidDataException`; a failed download is an `IOException`. The command reports each as an
error with exit code 2.

**Dependencies**: the .NET base class library only (file system, hashing, JSON reading, compression and the archive
reader).

**Callers**: `WikipediaHomographAdapter` and `HeteronymBenchAdapter` (`ReadAllBytes`), `ExternalEvaluationRecord` (the
pins), `EvaluateExternalCommand` (`Load`, `Find`, `EnsureCached`) and the unit tests.
