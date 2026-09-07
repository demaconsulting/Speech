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
- **IsCaptureFormatSupported(...) / IsPlaybackFormatSupported(...)**: Build the same
  `PortAudioSharp.StreamParameters` the corresponding open-stream path builds, marshal it to
  unmanaged memory, and call the supplementary native `Pa_IsFormatSupported` entry point
  (declared in `PortAudioNativeMethods` alongside the other native bindings PortAudioSharp2 does
  not wrap) to confirm whether the exact channel-count/sample-rate combination can actually be
  opened. Always frees the unmanaged memory in a `finally` block and fails safe (returns `false`)
  if the probe itself throws, so a probe failure is never worse than un-negotiated forwarding.
- **OpenCaptureStream(...) / OpenPlaybackStream(...)**: Open PortAudioSharp2 callback streams,
  marshal float sample buffers between native memory and managed arrays, and wrap the resulting
  `PortAudioSharp.Stream` in the `IPortAudioStream` seam.

**Error Handling**: Throws managed exceptions when PortAudio returns invalid metadata pointers or
negative count values. Higher-level callers decide whether those faults are composition-time
fallbacks or first-use device exceptions.

**Dependencies**: `PortAudioSharp2`, `PortAudioNativeMethods`, `PortAudioDeviceInfo`,
`PortAudioHostApiInfo`, and `IPortAudioStream`.

**Callers**: `PortAudioEnvironment.Shared`.
