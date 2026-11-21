### CorpusSnapshot

**Purpose**: Build and read the byte-reproducible snapshot archive of the normalized word-cluster corpus, so the corpus
can be re-created when the mirror changes and so its SHA-256 in the clustering record identifies exactly which corpus a
table came from.

**Data Model**:

- `CorpusSnapshotInfo`: the archive path (empty for a measured archive), its lowercase hexadecimal SHA-256, its size and
  its number of member files
- the archive: an uncompressed POSIX `ustar` archive whose members are, in this order, `NOTICE.txt` (a generated notice
  naming the number of books, their public-domain status in the USA and the removal of the distributor's text),
  `cluster-sources.tsv` and `bodies/gutenberg-N.txt` for every body in ordinal id order

**Key Methods**:

- **Build(path, sourcesFile, bodies)**: writes every header itself rather than through a tar library, so no library
  default can leak in: the name (ASCII, at most 100 characters, a safe relative path), mode `0000644`, owner and group
  `0`, the size in octal, modification time `0`, type regular file, magic `ustar` with version `00`, empty owner and
  group names and device numbers `0`, then the checksum. Data is padded to 512-byte blocks, two zero blocks end the
  archive and zeros fill the last 10,240-byte record. The archive is written to a `.partial` sibling and renamed, and
  the SHA-256 is computed while writing. The same inputs, in any order, give the same bytes on every platform
- **Measure(sourcesFile, bodies)**: produces the same bytes as `Build` but only hashes and counts them (they are written
  to a null stream), returning the identity with an empty path and writing no file; `ClusterCommand --check` uses it so
  that comparing an existing archive never replaces it
- **Read(path)**: reads every member with the standard tar reader and returns the bytes by name, accepting only regular
  files with a unique, safe relative name of at most 64 MiB
- **Notice(books)**, **BodyMemberName(id)**, **FileSha256(path)**: the notice text, the member name of a body and the
  hash of a file

**Error Handling**: null arguments throw `ArgumentNullException`; a member name that is not a safe ASCII name of at most
100 characters throws `ArgumentException`. A missing archive, an invalid archive and a member that is not a unique
regular file with a safe name throw `InvalidDataException`.

**Dependencies**: the .NET base class library only (files, SHA-256, the tar reader).

**Callers**: `ClusterCommand` (build, measure), `ClusterCorpusCache` (read) and the unit tests.
