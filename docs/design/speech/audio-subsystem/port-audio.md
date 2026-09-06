<!-- cspell:ignore ALSA portaudio -->
### PortAudio

![PortAudio Structure](PortAudioView.svg)

#### Overview

The PortAudio child subsystem isolates every direct dependency on `PortAudioSharp2` and the
supplementary native host-API P/Invoke bindings. Its responsibility is not to expose public API;
instead it gives the higher-level audio-device implementations a mockable managed seam for
initialization, host-API resolution, device metadata access, and stream lifecycle control.

Contained units:

- **IPortAudioApi**: internal seam for PortAudio initialization, metadata access, and stream opening
- **IPortAudioStream**: internal seam for stream lifecycle
- **PortAudioEnvironment**: shared initialization state and preferred-host resolver
- **PortAudioApi**: real PortAudioSharp2 + supplementary P/Invoke adapter
- **PortAudioDeviceInfo**, **PortAudioHostApiInfo**, **PortAudioHostApiType**, and
  **PortAudioNativeMethods**: supporting metadata and interop declarations

#### Interfaces

This subsystem exposes only internal interfaces. `IPortAudioApi` and `IPortAudioStream` are the
narrow seam consumed by `PortAudioCaptureDeviceProbe`, `PortAudioPlaybackDeviceProbe`,
`PortAudioCaptureDevice`, and `PortAudioPlaybackDevice`.

#### Design

`PortAudioEnvironment` is the higher-level entry point into this subsystem. It wraps an
`IPortAudioApi` implementation and caches one initialization attempt so callers can query
`IsInitialized` and `InitializationFailureMessage` without handling native exceptions. It also
contains the policy that maps Windows to `WASAPI`, Linux to `ALSA`, and macOS to `CoreAudio`.

`PortAudioApi` is the concrete adapter used in production. It composes PortAudioSharp2's managed
`PortAudio` and `Stream` types with repository-local `PortAudioNativeMethods` P/Invoke bindings
for `Pa_GetHostApiCount`, `Pa_GetHostApiInfo`, and `Pa_HostApiTypeIdToHostApiIndex`, which
PortAudioSharp2 does not expose. The adapter converts native/device structures into the managed
`PortAudioDeviceInfo` and `PortAudioHostApiInfo` records, and wraps the concrete
`PortAudioSharp.Stream` inside the `IPortAudioStream` seam.

#### Supporting Types

**PortAudioDeviceInfo**: Managed device metadata record carrying device name, host-API index,
channel counts, default sample rate, and default low-latency values.

**PortAudioHostApiInfo**: Managed host-API metadata record carrying the stable host-API type,
host-API name, and host-API-scoped default input/output device indices.

**PortAudioHostApiType**: Internal enum carrying the stable PortAudio constants used by the OS to
preferred-host mapping policy.

**PortAudioNativeMethods**: Internal static class declaring the native host-API functions absent
from PortAudioSharp2 and the managed layout for `PaHostApiInfo`.
