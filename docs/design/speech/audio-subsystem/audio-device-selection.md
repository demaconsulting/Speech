### AudioDeviceSelection

**Purpose**: Immutably describe a host's audio device preference, and resolve that preference
against a list of available devices with a well-defined fallback rule.

**Data Model**: A `record` with a single property `DeviceName` (`string?`; `null` means "no
preference, use the system default"). A static `SystemDefault` property returns an instance with
`DeviceName` set to `null`, giving callers a named way to express "no preference" instead of
constructing `new AudioDeviceSelection(null)` directly.

**Key Methods**:

- **Resolve(IReadOnlyList&lt;AudioDeviceDescription&gt; availableDevices)**: Returns the device
  in `availableDevices` whose `Name` exactly matches `DeviceName` (ordinal comparison), or `null`
  when `DeviceName` is `null` or no exact match is found — both cases mean "fall back to the
  system default" and are treated identically by callers. Preconditions: `availableDevices` is
  non-null (may be empty). Postconditions: throws `ArgumentNullException` only when
  `availableDevices` itself is `null`; otherwise never throws.

**Error Handling**: `Resolve` throws `ArgumentNullException` when `availableDevices` is `null`,
distinguishing "not asked" from "no devices available"; every other input (empty list, stale or
unmatched name) resolves to `null` rather than throwing, per architecture.md's "nothing throws
at composition" decision.

**Dependencies**: `AudioDeviceDescription` (parameter/return type).

**Callers**: A later phase's real `AudioDeviceFactory` backend (to honor a host's device
preference); exercised directly by unit tests in this phase since `AudioDeviceFactory` does not
yet use it for resolution.
