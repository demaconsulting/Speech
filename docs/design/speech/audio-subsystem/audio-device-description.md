### AudioDeviceDescription

**Purpose**: Immutably describe one audio device by name and basic capabilities, without
depending on any native backend type.

**Data Model**: A `record` with properties `Name` (`string`, non-null, the device's sole
identity key per architecture.md's name-only identity decision), `Direction`
(`AudioDeviceDirection`: `Capture` or `Playback`), `ChannelCount` (`int`), and `SampleRate`
(`int`). Records are immutable after construction and support value-based equality, which the
subsystem relies on for comparing descriptions in tests and in future selection logic.

**Key Methods**: None beyond the compiler-generated record members (constructor, `Equals`,
`GetHashCode`, `ToString`, `with`-expressions). No behavior beyond data carriage.

**Error Handling**: None — the type performs no validation; it is a pure data carrier populated
by probes (in a later phase) or by tests today.

**Dependencies**: `AudioDeviceDirection` (declared alongside it).

**Callers**: `IAudioCaptureDeviceProbe.Enumerate()` / `IAudioPlaybackDeviceProbe.Enumerate()`
(return type); `AudioDeviceSelection.Resolve` (consumes a list of these).
