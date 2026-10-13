### SpeechModelStoreOptions

#### Verification Approach

Verified indirectly through `SpeechModelStoreTests.cs`'s root-resolution scenarios, since this
type is a plain options bag with no behavior of its own beyond exposing `RootPathOverride` to
`SpeechModelStore`'s constructor.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when a supplied `RootPathOverride` containing non-whitespace characters is used by
`SpeechModelStore` verbatim (including when padded with whitespace), and when omitting it, or supplying a
null, empty, or whitespace-only value, falls back to the default `LocalApplicationData`-rooted path.

#### Error Paths

This unit has no error path: it is a property bag that never validates or throws, so no error scenario
applies. Invalid or unwritable roots surface only on first write in `SpeechModelStore` (verified in
that unit's verification document).

#### Test Scenarios

##### Root Path Override Used Verbatim

**Test**: `SpeechModelStore_Constructor_RootPathOverride_UsesOverrideVerbatim`

##### Boundary: Padded Override Is Still an Override

**Test**: `SpeechModelStore_Constructor_PaddedRootPathOverride_UsesOverrideVerbatim`

A value with non-whitespace characters surrounded by spaces is not "whitespace-only", so it is used exactly as
given (no trimming).

##### Boundary: Whitespace-Only Override Uses the Default

**Test**: `SpeechModelStore_Constructor_WhitespaceRootPathOverride_UsesDefaultRoot`

Spaces, a tab, and a CR/LF-padded value are each treated as unset.

##### Boundary: Null or Empty Override Uses the Default

**Test**: `SpeechModelStore_Constructor_NullOrEmptyRootPathOverride_UsesDefaultRoot`

##### No Options Resolves Under LocalApplicationData

**Test**: `SpeechModelStore_Constructor_NoOptions_ResolvesUnderLocalApplicationData`
