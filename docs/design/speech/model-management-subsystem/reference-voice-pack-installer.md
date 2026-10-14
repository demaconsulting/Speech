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

0. Yields once (`Task.Yield`) so the synchronous zip central-directory read does not run on the caller's thread,
   then entries are copied with asynchronous file I/O. Cancellation is checked immediately after the yield,
   before the archive is opened, so a canceled call reports `OperationCanceledException` even when the staged
   archive is missing.
1. Opens `{stagingDirectory}\{pack.ArchiveFileName}` as a zip archive.
2. For every entry, before the allow-list is consulted, applies the zip-slip guard and the portable-name check
   to all of its path segments (see Error Handling), so an entry the installer would ignore is still rejected
   when its name is invalid on some platform. Then considers only entries under `{ArchiveRootFolder}/`:
    `voices/*.flac`, `voices/README.md`,
   `LICENSE.md` and `README.md`. When `includeEmotions` is true it also takes
   `voices-emotion/README.md` and `voices-emotion/{speaker}/{mood}.flac` (exactly four path segments, a
   `.flac` name with a non-empty stem; misshapen entries are ignored). Everything else is ignored. The
   root folder is removed, so the result is `voices\*.flac`, `voices\README.md`, `LICENSE.md`,
   `README.md` and optionally `voices-emotion\{speaker}\{mood}.flac`.
3. Applies the zip-slip guard to every extracted entry name (below) and streams each entry to disk,
   counting the bytes actually read from the entry stream against the per-entry and total caps (the caps do not
   depend on the sizes the archive headers declare; the zip reader itself never delivers more bytes than a
   header declares, which a test with a forged header confirms).
4. Fails when no voice clip was extracted (cancellation is checked again first, so an archive with no entries
   still reports cancellation). Emotion clips alone never satisfy this step.
5. Writes `pack.json` (id, name, license, license URL, attribution, source URL,
   archive URL and SHA-256, voice count, emotion clip count, variants: `["voices"]` or
   `["voices","voices-emotion"]`) and only then deletes the archive, so a metadata failure or cancellation keeps it.

**Error Handling**: A null pack throws `ArgumentNullException` and a null or empty staging directory throws
`ArgumentException`. All archive problems are `InvalidDataException` (which the downloader classifies as
`Failed`, installing nothing): a file that is not a zip archive, an entry name containing a backslash,
a colon, a leading slash, a NUL, or a `..` or `.` segment (rejected for every entry, even one the allow-list
would otherwise ignore, because the name check runs before the allow-list), an entry (extracted or
 ignored) with a segment that is
not
valid on every platform (characters `* ? < > | "` or control characters, Windows reserved device names
such as `CON`/`NUL`/`COM1`, a trailing dot or space), a resolved path outside the staging
directory, two entries mapping to one destination (compared case-insensitively, so entries differing only in case
collide),
an entry or total size over its cap, or no voices.
A missing or unreadable staged archive is not converted: it surfaces as the `IOException` (for example
`FileNotFoundException`) or `UnauthorizedAccessException` the file system reports. The staged archive is left
in place when the install fails or is canceled; only a fully successful install (extraction and metadata) deletes
it. Cancellation propagates as
`OperationCanceledException`. Because the archive is checksum-verified the
caps are a second line of protection rather than the first.

**Dependencies**: `ReferenceVoicePack`, `System.IO.Compression`.

**Callers**: `ReferenceVoiceLibrary.InstallPackAsync`, through the downloader's install-hook overload.
