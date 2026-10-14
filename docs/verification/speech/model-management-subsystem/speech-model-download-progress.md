### SpeechModelDownloadProgress

#### Verification Approach

Verified through direct unit tests in `SpeechModelDownloadProgressTests.cs` proving
`FractionComplete`'s cases: known total, unknown total, zero total, and out-of-range counts (clamped).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when a known, positive total yields the correct fraction, an unknown (`null`) total
yields `null`, a total of exactly zero yields `1.0` rather than dividing by zero, and a transferred count above
the total or below zero yields `1.0` or `0.0` respectively.

#### Test Scenarios

##### FractionComplete: Known Total Computes Fraction

**Test**: `SpeechModelDownloadProgress_FractionComplete_KnownTotal_ComputesFraction`

##### FractionComplete: Unknown Total Returns Null

**Test**: `SpeechModelDownloadProgress_FractionComplete_UnknownTotal_ReturnsNull`

##### FractionComplete: Zero Total Bytes Returns One

**Test**: `SpeechModelDownloadProgress_FractionComplete_ZeroTotalBytes_ReturnsOne`

##### FractionComplete: Out-of-Range Counts Are Clamped

**Test**: `SpeechModelDownloadProgress_FractionComplete_BytesOverTotal_ClampsToOne`

**Test**: `SpeechModelDownloadProgress_FractionComplete_NegativeBytes_ClampsToZero`

250 bytes of a 200-byte total yield `1.0`; a negative transferred count yields `0.0`.
