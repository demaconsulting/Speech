### AudioDeviceSelection

#### Verification Approach

Verified through direct unit tests in `AudioDeviceSelectionTests.cs` covering every documented
resolution outcome. No mocking is applicable; `AudioDeviceDescription` instances are constructed
directly as plain data.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when `Resolve` returns the exact-match device when present, `null` for a stale or
system-default selection, `null` for an empty device list, and throws `ArgumentNullException`
for a `null` device list.

#### Test Scenarios

##### Resolution: Matching Name Returns the Matching Device

**Test**: `AudioDeviceSelection_Resolve_MatchingName_ReturnsMatchingDevice`

##### Resolution: No Longer Present Returns Null

**Test**: `AudioDeviceSelection_Resolve_NoLongerPresent_ReturnsNull`

##### Resolution: System Default Always Returns Null

**Test**: `AudioDeviceSelection_Resolve_SystemDefault_ReturnsNull`

##### Resolution: Empty Device List Returns Null

**Test**: `AudioDeviceSelection_Resolve_EmptyDeviceList_ReturnsNull`

##### Resolution: Null Device List Throws ArgumentNullException

**Test**: `AudioDeviceSelection_Resolve_NullDeviceList_ThrowsArgumentNullException`
