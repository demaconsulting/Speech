### ZipEntryPathValidator

#### Verification Approach

Unit-tested in `ZipEntryPathValidatorTests.cs` by calling the internal static methods directly with literal entry
names and paths. Nothing is mocked: the unit is a pure function of its inputs over `System.IO.Path`, and
`ResolveUnderRoot` only canonicalizes paths, so no file is created. Both installers that use it are additionally
covered by their own tests (`ReferenceVoicePackInstallerTests.cs`, `SpeechModelBundleInstallerTests.cs`).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **File system**: none written; `ResolveUnderRoot` tests use a path under the system temp path that need not exist

#### Acceptance Criteria

Tests pass when every unsafe name (parent or current-directory segment, leading slash, backslash, colon, NUL) and
every non-portable segment (rejected or control characters, trailing dot or space, reserved device names with or
without an extension, in any letter case) throws `InvalidDataException` with a message naming the problem; a safe
name splits into its non-empty segments; and a destination inside the root resolves to its canonical path while
one outside the root, in a sibling sharing the root's name as a prefix, or equal to the root throws
`InvalidDataException`.

#### Test Scenarios

##### Unsafe Names Are Rejected

**Test**: `ZipEntryPathValidator_SplitSafely_UnsafeName_ThrowsInvalidDataException`

Covers `../x`, `a/../../x`, `./x`, `a/./x`, `/abs`, `a\b`, `C:x`, `a/b:stream` and a NUL.

##### Safe Names Split Into Segments

**Test**: `ZipEntryPathValidator_SplitSafely_SafeName_ReturnsSegments`

`onnx/model.onnx` gives two segments and the directory entry `onnx/` gives one.

##### Non-Portable Segments Are Rejected

**Test**: `ZipEntryPathValidator_Validate_NonPortableSegment_ThrowsInvalidDataException`

Covers `*`, `?`, `<`, `>`, `|`, `"`, a control character, a trailing dot, a trailing space, and the reserved names
`CON`, `nul.bin`, `lpt1.txt` and `com0`.

##### Destinations Must Stay Inside The Root

**Test**: `ZipEntryPathValidator_ResolveUnderRoot_PathInsideRoot_ReturnsFullPath`

**Test**: `ZipEntryPathValidator_ResolveUnderRoot_PathOutsideRoot_ThrowsInvalidDataException`

Covers a parent escape, a sibling `zip-root-evil` directory next to the `zip-root` root, and `.` (the root itself).
