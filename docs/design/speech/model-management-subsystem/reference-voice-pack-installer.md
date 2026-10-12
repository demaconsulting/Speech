### ReferenceVoicePackInstaller

**Purpose**: The install hook the downloader runs on a verified pack archive: extract only what the
voice library needs, safely, and leave the staging directory in the shape that becomes the pack's
`current\` directory.

**Data Model**: An internal static class (no state). Constants: `PackMetadataFileName` (`pack.json`),
`VoicesDirectoryName` (`voices`), `EmotionsDirectoryName` (`voices-emotion`), `MaximumEntryBytes` (128 MiB)
and `MaximumTotalBytes` (2 GiB).

**Key Methods**: `InstallAsync(pack, includeEmotions, stagingDirectory, cancellationToken)` (uses the default
caps) delegates to an internal overload that also takes `maximumEntryBytes` and `maximumTotalBytes`, which
exists so tests can exercise the caps with small limits:

1. Opens `{stagingDirectory}\{pack.ArchiveFileName}` as a zip archive.
2. Considers only entries under `{ArchiveRootFolder}/`: `voices/*.flac`, `voices/README.md`,
   `LICENSE.md` and `README.md`. When `includeEmotions` is true it also takes
   `voices-emotion/README.md` and `voices-emotion/{speaker}/{mood}.flac` (exactly four path segments, a
   `.flac` name with a non-empty stem; misshapen entries are ignored). Everything else is ignored. The
   root folder is removed, so the result is `voices\*.flac`, `voices\README.md`, `LICENSE.md`,
   `README.md` and optionally `voices-emotion\{speaker}\{mood}.flac`.
3. Applies the zip-slip guard to every extracted entry name (below) and streams each entry to disk,
   counting the bytes actually read against the per-entry and total caps.
4. Fails when no voice clip was extracted. Emotion clips alone never satisfy this step.
5. Deletes the archive and writes `pack.json` (id, name, license, license URL, attribution, source URL,
   archive URL and SHA-256, voice count, emotion clip count, variants: `["voices"]` or
   `["voices","voices-emotion"]`).

**Error Handling**: All problems are `InvalidDataException` (which the downloader classifies as
`Failed`, installing nothing): a file that is not a zip archive, an entry name containing a backslash,
a colon, a leading slash, a NUL, or a `..` or `.` segment, an extracted entry with a segment that is not
valid on every platform (characters `* ? < > | "` or control characters, Windows reserved device names
such as `CON`/`NUL`/`COM1`, a trailing dot or space), a resolved path outside the staging
directory, two entries mapping to one destination, an entry or total size over its cap, or no voices.
Cancellation propagates as `OperationCanceledException`. Because the archive is checksum-verified the
caps are a second line of protection rather than the first.

**Dependencies**: `ReferenceVoicePack`, `System.IO.Compression`.

**Callers**: `ReferenceVoiceLibrary.InstallPackAsync`, through the downloader's install-hook overload.
