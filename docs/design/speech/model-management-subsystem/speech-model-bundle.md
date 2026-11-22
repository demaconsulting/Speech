### SpeechModelBundle

**Purpose**: Describe a model published as one or more bundle archives on the project's long-lived `models`
GitHub release (or any HTTPS host): one SHA-256-pinned zip per bundle carrying the model's unmodified vendor files
plus their license material. It lets a model be downloaded from one firewall-friendly host as a single verified
file while keeping the original per-file pins as a second, compiled-in proof that every extracted file is the
unmodified upstream file.

**Data Model**: A public, immutable, sealed class:

- `ModelsReleaseBaseUri` (static): `https://github.com/demaconsulting/Speech/releases/download/models/`, with a
  trailing slash so `new Uri(ModelsReleaseBaseUri, fileName)` appends a file name.
- `Uri`: the absolute HTTPS location of the zip, with no query or fragment.
- `Sha256Checksum`: the 64-hex pin of the whole zip.
- `FileName`: the unescaped last segment of `Uri`, which must match
  `^[a-z0-9]+([.-][a-z0-9]+)*-(fp32|fp16|int8|int4)-b[1-9][0-9]*\.zip$` (lowercase model and upstream-release
  tokens, a quantization token, a bundle revision `bN`), for example `kokoro-en-v1.0-int8-b1.zip`.
- `Name`: `FileName` without `.zip`; recorded in the bundle's `model.json` and naming its `licenses/{Name}/`
  folder.
- `Contents`: a `SpeechModelDownloadDescriptor` of the vendor files exactly as the model would declare them for a
  direct download: original upstream URI (provenance only; never fetched), SHA-256 pin and install path, which is
  also the path inside the zip.
- `DownloadFile`: the `SpeechModelDownloadFile(Uri, Sha256Checksum, FileName)` the downloader fetches.
- The internal constant `LicensesDirectoryName` (`licenses`).

Invariants enforced at construction: no `Contents` path (normalized to `/`, compared ignoring case) is under
`licenses/`, equals a metadata entry name (`model.json`, `README.txt`, `LICENSE`, `NOTICE`) or `FileName`, has a
segment that is not portable (per `ZipEntryPathValidator`), or collides with another path of the bundle (equal
ignoring case, or one a parent directory of another).

**Key Methods**:

- **SpeechModelBundle(uri, sha256Checksum, contents)**: Validates the arguments in order (null checks, HTTPS,
  no query or fragment, naming rule, checksum shape through `SpeechModelDownloadFile`, contents rules) and stores
  them.
- **CreateDescriptor(bundles)** *(static)*: Validates the bundle set and returns a `SpeechModelDownloadDescriptor`
  of each bundle's `DownloadFile`, in order. A model returns it as its `DownloadDescriptor`.
- **InstallAsync(bundles, stagedFilesDirectory, cancellationToken)** *(static)*: Validates the bundle set and the
  directory, then runs `SpeechModelBundleInstaller.InstallAsync` for each bundle in order (one extraction at a
  time keeps the peak disk use to the staged zips plus one extraction). A model's `ISpeechModel.InstallAsync`
  override is this one call.
- **Bundle-set validation** *(private)*: the set must be non-empty with no null element; no two bundles may share
  a `Name` (ignoring case), because their metadata folders would collide; the vendor paths of all bundles must be
  disjoint (the same collision rule as within a bundle), because they install into one directory; and no bundle's
  `FileName` may equal any bundle's vendor path, because the zips are staged beside the extracted files.
- **NormalizePath(file)** and **FindPathCollision(paths)** *(internal)*: the shared helpers that normalize a
  declared install path to `/`-separated segments and find the first equal-ignoring-case or parent-directory
  collision; `SpeechModelBundleInstaller` uses them for `model.json` paths too.

**Error Handling**: The constructor throws `ArgumentNullException` for a null argument and `ArgumentException`
for a non-HTTPS URI, a query or fragment, a file name that breaks the naming rule (parameter `uri`), a malformed
checksum, or a `Contents` path that breaks an invariant (parameter `contents`). `CreateDescriptor` and
`InstallAsync` throw `ArgumentNullException` for a null list and `ArgumentException` for an empty list, a null
element or a collision; `InstallAsync` also throws `ArgumentException` for a null or empty directory and
propagates every installer exception unchanged (`InvalidDataException`, `IOException`,
`OperationCanceledException`), which the downloader classifies and recovers from by discarding the staging
directory.

**Dependencies**: `SpeechModelDownloadFile`, `SpeechModelDownloadDescriptor`, `SpeechModelBundleInstaller`,
`ZipEntryPathValidator`.

**Callers**: Model packages that distribute their files as bundles (their `DownloadDescriptor` and `InstallAsync`
members), and the downloader indirectly through those members.
