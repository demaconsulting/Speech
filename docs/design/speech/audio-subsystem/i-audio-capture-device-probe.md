### IAudioCaptureDeviceProbe

**Purpose**: Define the contract for enumerating available audio capture devices.

**Data Model**: No properties; a pure behavioral contract.

**Key Methods**:

- **Enumerate()**: Returns the currently available capture devices as
  `IReadOnlyList<AudioDeviceDescription>`. Real implementations restrict enumeration to the
  platform's preferred PortAudio host API and return an empty list rather than throwing when
  no devices can be enumerated.

**Error Handling**: Every shipped implementation treats "cannot enumerate" as
"enumerate zero devices" rather than throwing.

**Dependencies**: `AudioDeviceDescription`.

**Callers**: `AudioDeviceFactory` and hosts that present a capture-device list to a user.
