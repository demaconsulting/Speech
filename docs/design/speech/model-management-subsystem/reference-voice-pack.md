### ReferenceVoicePack

**Purpose**: Describe a downloadable pack of reference-voice clips - where its single archive lives,
how to verify it, and what license and attribution apply - so a model-agnostic library can install it
through the normal verified download path. Also hosts the two small value types that travel with it,
`ReferenceVoiceSource` and `ReferenceVoiceInfo`.

**Data Model**:

- `ReferenceVoicePack` (sealed record): `Id`, `DisplayName`, `Uri` (HTTPS), `Sha256Checksum` (64 hex),
  `SizeBytes`, `VoicesBytes`, `EmotionsBytes`, `ArchiveFileName`, `ArchiveRootFolder`, `LicenseName`, `LicenseUrl`, `Attribution`,
  `SourceUrl`. `VoicesBytes`/`EmotionsBytes` (default 0) are the measured installed sizes of the base
  voices (with the license and readme files) and of the emotion clips, and `InstalledSizeBytes(bool
  includeEmotions)` sums the chosen variants. An internal `DownloadDescriptor` projects it onto a one-file
  `SpeechModelDownloadDescriptor` whose relative install path is the archive file name.
- `ReferenceVoicePack.VoiceZero10`: the built-in voice-zero 1.0 pack - archive
  `https://github.com/OwenTyme/voice-zero/releases/download/1.0/voice-zero-1.0.zip`, 597,269,359 bytes,
  SHA-256 `001d5982fc4b602f7c62a764046dea1f53999a493de43955f35686d2c16d7c66`, CC0 1.0 Universal,
  attribution "voice-zero by OwenTyme", root folder `voice-zero`, 45,632,432 bytes of voices and 553,873,246
  bytes of emotion clips (599,505,678
  installed in total, about 572 MiB; the download is about 570 MiB). `ReferenceVoicePack.All` lists it.
- `ReferenceVoiceSource` (enum): `Directory` (a caller-named directory), `User` (the user folder),
  `Pack` (an installed pack).
- `ReferenceVoiceInfo` (sealed record): `Id`, `DisplayName`, `FilePath`, `Source`, `PackId`, `Speaker`,
  `Mood`, `LicenseName`, `Attribution`, `MoodClips`; the engine-neutral description of one clip on
  disk. `MoodClips` is an optional read-only map of lower-case mood name to the absolute path of that
  speaker's emotion clip, or `null` when the voice has none (user and directory voices, and packs
  installed without emotions). `ReferenceVoiceInfo.KnownMoods` lists the 13 moods the pack provides:
  anger, calm, confused, enthused, excited, frustrated, happy, neutral, sad, shout, surprised, tired,
  worried. Moods are variants of a voice, not separate voices; `Mood` stays `null` for pack voices.

**Error Handling**: The constructor throws `ArgumentException` for a non-HTTPS URL, a malformed
checksum, or an id, archive file name or archive root folder that is not a single safe path segment
(no separators, `..` or rooted names), and `ArgumentOutOfRangeException` for a non-positive size or a
negative `VoicesBytes` / `EmotionsBytes`.

**Dependencies**: `SpeechModelDownloadDescriptor` / `SpeechModelDownloadFile`.

**Callers**: `ReferenceVoiceLibrary`, `ReferenceVoicePackInstaller`, and hosts that list packs or
voices.

#### Alternatives Considered: Range Requests Versus Whole-Archive Download

The upstream archive is about 570 MiB but the base voices are only about 44 MiB of it (154 clips); the 2,002
emotion clips (13 moods for each
of the 154 speakers, about 528 MiB) make up the rest and are now an optional extra. Two ways of installing
only the voices were weighed.

- **Whole-archive download (chosen).** The archive is fetched in full through the existing
  `SpeechModelDownloader`, verified against the published archive SHA-256, and only the allow-listed
  entries are extracted (see `ReferenceVoicePackInstaller`) before the archive is deleted. This reuses
  all of the downloader's behavior unchanged - integrity verification of the exact published bytes,
  progress, mirror redirection and authentication, honest failure classification, atomic install - and
  needs nothing from the server beyond a plain GET, which every mirror supports.
- **HTTP range requests for the zip central directory and the wanted entries (rejected for now).** This
  would transfer roughly 44 MiB instead of 570 MiB, but it needs a new optional range-capable download
  client interface (the existing `IModelDownloadClient` is deliberately frozen), it cannot verify the
  published whole-archive hash because the whole archive is never read (so integrity would rest on a
  separately maintained per-entry manifest), and an internal mirror or proxy may not support `Range`
  at all, which would make the feature unreliable exactly where mirrors are used.

A per-entry manifest with range requests remains a possible future optimization; the descriptor and
installer are kept separate so it could be added without changing the public surface.
