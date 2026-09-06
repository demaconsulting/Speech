#### PortAudioApi

**Purpose**: Adapt PortAudioSharp2 and the supplementary host-API P/Invoke bindings to the
`IPortAudioApi` seam.

**Data Model**: Stateless singleton adapter exposed through `PortAudioApi.Instance`.

**Key Methods**:

- **Initialize()**: Calls the managed PortAudioSharp2 initialization path.
- **GetHostApiInfo(...)**: Reads a native `PaHostApiInfo` structure through
  `PortAudioNativeMethods` and converts it to a managed `PortAudioHostApiInfo` record.
- **GetDeviceInfo(...)**: Converts PortAudioSharp2 device metadata into a managed
  `PortAudioDeviceInfo` record.
- **OpenCaptureStream(...) / OpenPlaybackStream(...)**: Open PortAudioSharp2 callback streams,
  marshal float sample buffers between native memory and managed arrays, and wrap the resulting
  `PortAudioSharp.Stream` in the `IPortAudioStream` seam.

**Error Handling**: Throws managed exceptions when PortAudio returns invalid metadata pointers or
negative count values. Higher-level callers decide whether those faults are composition-time
fallbacks or first-use device exceptions.

**Dependencies**: `PortAudioSharp2`, `PortAudioNativeMethods`, `PortAudioDeviceInfo`,
`PortAudioHostApiInfo`, and `IPortAudioStream`.

**Callers**: `PortAudioEnvironment.Shared`.
