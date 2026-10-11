### ReferenceVoicePack

**Purpose**: Describe a downloadable pack of reference-voice clips - where its single archive lives,
how to verify it, and what license and attribution apply - so a model-agnostic library can install it
through the normal verified download path. Also hosts the two small value types that travel with it,
`ReferenceVoiceSource` and `ReferenceVoiceInfo`.

**Data Model**:

- `ReferenceVoicePack` (sealed record): `Id`, `DisplayName`, `Uri` (HTTPS), `Sha256Checksum` (64 hex),
  `SizeBytes`, `ArchiveFileName`, `ArchiveRootFolder`, `LicenseName`, `LicenseUrl`, `Attribution`,
  `SourceUrl`. An internal `DownloadDescriptor` projects it onto a one-file
  `SpeechModelDownloadDescriptor` whose relative install path is the archive file name.
- `ReferenceVoicePack.VoiceZero10`: the built-in voice-zero 1.0 pack - archive
  `https://github.com/OwenTyme/voice-zero/releases/download/1.0/voice-zero-1.0.zip`, 597,269,359 bytes,
  SHA-256 `001d5982fc4b602f7c62a764046dea1f53999a493de43955f35686d2c16d7c66`, CC0 1.0 Universal,
  attribution "voice-zero by OwenTyme", root folder `voice-zero`. `ReferenceVoicePack.All` lists it.
- `ReferenceVoiceSource` (enum): `Directory` (a caller-named directory), `User` (the user folder),
  `Pack` (an installed pack).
- `ReferenceVoiceInfo` (sealed record): `Id`, `DisplayName`, `FilePath`, `Source`, `PackId`, `Speaker`,
  `Mood`, `LicenseName`, `Attribution`; the engine-neutral description of one clip on disk.

**Error Handling**: The constructor throws `ArgumentException` for a non-HTTPS URL, a malformed
checksum, or an id, archive file name or archive root folder that is not a single safe path segment
(no separators, `..` or rooted names), and `ArgumentOutOfRangeException` for a non-positive size.

**Dependencies**: `SpeechModelDownloadDescriptor` / `SpeechModelDownloadFile`.

**Callers**: `ReferenceVoiceLibrary`, `ReferenceVoicePackInstaller`, and hosts that list packs or
voices.

#### Alternatives Considered: Range Requests Versus Whole-Archive Download

The upstream archive is about 570 MiB but the voices a model needs are about 46 MB of it (154 clips);
roughly 2,000 emotion clips make up the rest. Two ways of installing only the voices were weighed.

- **Whole-archive download (chosen).** The archive is fetched in full through the existing
  `SpeechModelDownloader`, verified against the published archive SHA-256, and only the allow-listed
  entries are extracted (see `ReferenceVoicePackInstaller`) before the archive is deleted. This reuses
  all of the downloader's behavior unchanged - integrity verification of the exact published bytes,
  progress, mirror redirection and authentication, honest failure classification, atomic install - and
  needs nothing from the server beyond a plain GET, which every mirror supports.
- **HTTP range requests for the zip central directory and the wanted entries (rejected for now).** This
  would transfer roughly 46 MB instead of 570 MiB, but it needs a new optional range-capable download
  client interface (the existing `IModelDownloadClient` is deliberately frozen), it cannot verify the
  published whole-archive hash because the whole archive is never read (so integrity would rest on a
  separately maintained per-entry manifest), and an internal mirror or proxy may not support `Range`
  at all, which would make the feature unreliable exactly where mirrors are used.

A per-entry manifest with range requests remains a possible future optimization; the descriptor and
installer are kept separate so it could be added without changing the public surface.
