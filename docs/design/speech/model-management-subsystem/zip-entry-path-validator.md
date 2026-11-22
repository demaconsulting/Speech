### ZipEntryPathValidator

**Purpose**: Hold the security-sensitive zip-slip and name-portability checks in one place, so every installer
that extracts a downloaded archive (`ReferenceVoicePackInstaller` and `SpeechModelBundleInstaller`) applies the
identical guard. A checksum proves an archive is the published one; it does not prove the entry names inside it
are safe, so the names are validated as untrusted input.

**Data Model**: An internal static class with no state. A private character set rejected in every segment (the
platform's invalid file-name characters plus `* ? < > | "`, so the rule is the strictest platform's on every
platform) and a private set of Windows reserved device names (`CON`, `PRN`, `AUX`, `NUL`, `COM0`-`COM9`,
`LPT0`-`LPT9`, compared ignoring case).

**Key Methods**:

- **SplitSafely(entryName)**: Rejects a name containing a backslash (never a separator in a zip, but one on
  Windows), a colon (drive qualifier or alternate data stream), a NUL, or a leading slash, then splits on `/`
  dropping empty segments (so a directory entry `onnx/` yields `["onnx"]`) and rejects any `.` or `..` segment.
  Returns the segments.
- **ValidatePortableSegments(entryName, segments)**: Rejects a segment containing a rejected or control
  character, ending with a dot or a space, or whose stem (the part before the first dot, trailing spaces removed)
  is a reserved device name, so `NUL.txt` and `CON .flac` are refused like Windows would treat them.
- **Validate(entryName)**: `SplitSafely` followed by `ValidatePortableSegments`; the entry point installers call
  for every entry, extracted or not, before anything is written.
- **ResolveUnderRoot(rootFullPath, relativePath, entryName)**: Combines and canonicalizes the destination
  (`Path.GetFullPath`) and requires it to start with the root plus a directory separator (ordinal comparison).
  This last check is independent of the name rules: a destination equal to the root, outside it, or in a sibling
  directory sharing the root's name as a prefix is refused even if a name rule were weakened. Returns the
  canonical destination.

**Error Handling**: Every rejection is an `InvalidDataException` whose message names the entry ("has an unsafe
path", "has a name that is not valid on every platform", "resolves outside the install directory"), so the
downloader classifies it as an install-hook failure (`Failed`) and installs nothing. The class has no other
failure modes.

**Dependencies**: None beyond the .NET base class library (`System.IO.Path`).

**Callers**: `ReferenceVoicePackInstaller` (every pack entry), `SpeechModelBundleInstaller` (every bundle entry
and every `model.json` path) and `SpeechModelBundle` (the same `Validate` check on every vendor path at
construction).
