### AudioClip Verification

#### Verification Approach

Verified by `AudioFileReaderTests.cs` with a small hand-built clip.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK; project `test/DemaConsulting.Speech.Tests`
- **Isolation**: in-memory images and files in the test output or temp directory; no network

#### Acceptance Criteria

Frame count, duration, mono down-mix and clamped PCM16 conversion are correct, and invalid
construction or `with` values are rejected.

#### Test Scenarios

##### Invalid values are rejected

**Test**: `AudioClip_InvalidValues_Throw`

##### Empty samples have zero duration

**Test**: `AudioClip_EmptySamples_HasZeroDuration`

##### Derived members are consistent

**Test**: `AudioClip_DerivedMembers_AreConsistent`
