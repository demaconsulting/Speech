### SpeechModelStoreOptions

#### Verification Approach

Verified indirectly through `SpeechModelStoreTests.cs`'s root-resolution scenarios, since this
type is a plain options bag with no behavior of its own beyond exposing `RootPathOverride` to
`SpeechModelStore`'s constructor.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when a supplied `RootPathOverride` is used by `SpeechModelStore` verbatim, and when
omitting it falls back to the default `LocalApplicationData`-rooted path.

#### Test Scenarios

##### Root Path Override Used Verbatim

**Test**: `SpeechModelStore_Constructor_RootPathOverride_UsesOverrideVerbatim`

##### No Options Resolves Under LocalApplicationData

**Test**: `SpeechModelStore_Constructor_NoOptions_ResolvesUnderLocalApplicationData`
