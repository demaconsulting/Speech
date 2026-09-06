### SpeechModelDownloadProgress

#### Verification Approach

Verified through direct unit tests in `SpeechModelDownloadProgressTests.cs` proving
`FractionComplete`'s three cases: known total, unknown total, and zero total.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when a known, positive total yields the correct fraction, an unknown (`null`) total
yields `null`, and a total of exactly zero yields `1.0` rather than dividing by zero.

#### Test Scenarios

##### FractionComplete: Known Total Computes Fraction

**Test**: `SpeechModelDownloadProgress_FractionComplete_KnownTotal_ComputesFraction`

##### FractionComplete: Unknown Total Returns Null

**Test**: `SpeechModelDownloadProgress_FractionComplete_UnknownTotal_ReturnsNull`

##### FractionComplete: Zero Total Bytes Returns One

**Test**: `SpeechModelDownloadProgress_FractionComplete_ZeroTotalBytes_ReturnsOne`
