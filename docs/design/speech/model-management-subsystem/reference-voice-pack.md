### ReferenceVoicePack

**Purpose**: Describe a downloadable pack of reference-voice clips - where its single archive lives,
how to verify it, and what license and attribution apply - so a model-agnostic library can install it
through the normal verified download path. The unit also defines the two small value types that travel
with it, `ReferenceVoiceSource` and `ReferenceVoiceInfo`; the library populates them.

**Data Model**:

- `ReferenceVoicePack` (sealed class, reference identity; instances with equal values are not equal):
  `Id`, `DisplayName`, `Uri` (HTTPS), `Sha256Checksum` (64 hex),
  `SizeBytes`, `VoicesBytes`, `EmotionsBytes`, `ArchiveFileName`, `ArchiveRootFolder`, `LicenseName`,
  `LicenseUrl`, `Attribution`, `SourceUrl`. `VoicesBytes`/`EmotionsBytes` (default 0) are the measured
  installed sizes of the base voices (with the license and readme files) and of the emotion clips, and
  `InstalledSizeBytes(bool includeEmotions)` sums the chosen variants. An internal `DownloadDescriptor`
  projects it onto a one-file `SpeechModelDownloadDescriptor` whose relative install path is the archive
  file name. This internal shape is the design's mechanism for the requirement
  `Speech-Models-ReferenceVoicePack-DownloadDescriptor`, which states only the information the download needs.
- `ReferenceVoicePack.VoiceZero10`: the built-in voice-zero 1.0 pack by OwenTyme (CC0 1.0 Universal).
  Its archive URL, size, SHA-256, root folder and measured installed sizes are pinned only in
  `ReferenceVoicePack.cs` and verified by the unit tests (see the requirement
  `Speech-Models-ReferenceVoicePack-SizeMetadata`), so they are not repeated here. `ReferenceVoicePack.All`
  lists it.
- `ReferenceVoiceSource` (enum): `Directory` (a caller-named directory), `User` (the user folder),
  `Pack` (an installed pack). Assigned by `ReferenceVoiceLibrary.EnumerateVoices`; see the
  `ReferenceVoiceLibrary` design for the precedence rules that decide which source a voice has.
- `ReferenceVoiceInfo` (sealed record): `Id`, `DisplayName`, `FilePath`, `Source`, `PackId`, `Speaker`,
  `Mood`, `LicenseName`, `Attribution`, `MoodClips`; the engine-neutral description of one clip on
  disk, populated by `ReferenceVoiceLibrary.EnumerateVoices` (which owns how each field is filled, see
  the `ReferenceVoiceLibrary` design). `MoodClips` is an optional read-only map of lower-case mood name to
  the absolute path of that speaker's emotion clip, or `null` when the voice has none (user and directory
  voices, and packs installed without emotions). `MoodClips` is
  copied at construction into a read-only dictionary enumerated in ordinal mood order, so later changes
  to the supplied dictionary never show through (the requirements `...MoodClipsImmutable` and `...MoodClipsReadOnly`
  state only the observable outcome; the record, copy and ordering are this design's choice).
  `ReferenceVoiceInfo.KnownMoods` lists the 13 moods the
   pack
  provides:
  anger, calm, confused, enthused, excited, frustrated, happy, neutral, sad, shout, surprised, tired,
  worried. Moods are variants of a voice, not separate voices; `Mood` stays `null` for pack voices.

**Key Methods**:

- `ReferenceVoicePack(...)` (constructor): validates every argument (see Error Handling) and builds the
  internal one-file `DownloadDescriptor`.
- `InstalledSizeBytes(bool includeEmotions)`: returns `VoicesBytes` when emotions are excluded, otherwise
  `VoicesBytes + EmotionsBytes`, so a host can show the disk size of either install variant.
- `ReferenceVoiceInfo.KnownMoods`: the static list of the 13 mood names a pack's emotion clips use.
- `ReferenceVoiceInfo.TryGetMoodClip(mood, out filePath)`: finds a mood clip in `MoodClips`, matching the name
  case-insensitively after trimming; a null or blank name, an unknown mood or a voice without mood clips
  reports no match. Consumers use it instead of indexing `MoodClips` directly.

**Error Handling**: The constructor throws `ArgumentNullException` for a null reference argument,
`ArgumentException` for a blank text argument, a non-HTTPS URL, a malformed
checksum (an empty string is a malformed checksum, a null one an `ArgumentNullException`), or an id,
archive file name or archive root folder that is not a single safe path segment, and
`ArgumentOutOfRangeException` for a non-positive size or a negative `VoicesBytes` / `EmotionsBytes`.
A safe segment contains none of the characters invalid in a file name on any platform (including the
path separators, the drive colon and NUL), is not rooted, is not `.` or `..`, has no leading or trailing
whitespace and no trailing dot, and is not a Windows reserved device name (`CON`, `PRN`, `AUX`, `NUL`,
`COM1`-`COM9`, `LPT1`-`LPT9`, with or without an extension).

Arguments are checked in this order: blank or null text and null URIs, the size ranges, the safe
names, then the HTTPS scheme and checksum shape, which the constructor delegates to the
`SpeechModelDownloadFile` it builds for the download descriptor.

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
