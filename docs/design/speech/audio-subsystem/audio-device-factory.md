### AudioDeviceFactory

**Purpose**: Serve as the sole composition entry point for obtaining audio capture/playback
devices and their enumeration probes.

**Data Model**: `CaptureProbe` and `PlaybackProbe` hold the public probe implementations exposed
by the factory. A private `_environment` field tracks whether PortAudio initialized
successfully, and `_diagnostics` records structural selection and fallback events.

**Key Methods**:

- **AudioDeviceFactory(...)**: Accepts optional injected probes and a diagnostics sink, and an
  internal deterministic environment for tests. When the environment reports successful
  PortAudio initialization, default probes are the real PortAudio-backed implementations;
  otherwise they are the shared `Unavailable*` probes.
- **CreateCaptureDevice(...)**: Accepts an optional preferred `AudioFormat` and returns a real
  `PortAudioCaptureDevice` when PortAudio initialized successfully **and** the requested
  selection (or, when omitted, at least one device) is known to `CaptureProbe`'s enumeration;
  otherwise `UnavailableAudioCaptureDevice.Instance`.
- **CreatePlaybackDevice(...)**: Accepts an optional preferred `AudioFormat` and returns a real
  `PortAudioPlaybackDevice` when PortAudio initialized successfully **and** the requested
  selection (or, when omitted, at least one device) is known to `PlaybackProbe`'s enumeration;
  otherwise `UnavailableAudioPlaybackDevice.Instance`.
- **IsSelectionKnownToProbe(...)** (private, shared by both create methods): Checks a requested
  selection's device name against a probe's enumerated devices, or, for the default selection,
  that the probe enumerates at least one device. This keeps device creation consistent with
  whichever probe was actually injected into the factory - previously `CreateCaptureDevice`/
  `CreatePlaybackDevice` resolved a selection by independently re-scanning the real PortAudio
  environment, silently ignoring an injected probe entirely, so an injected test double
  reporting zero known devices had no effect on device creation at all. A `null`-or-empty
  `DeviceName` is treated the same as "no device requested" (falls through to "any known device
  is acceptable"), matching `AudioDeviceSelection.Resolve`'s treatment of an empty name: an
  empty name can never exactly match a real device, so `Resolve` always falls back to the
  system default for it, and this check must not demand an impossible exact match instead.

**Error Handling**: No member throws during composition. PortAudio initialization failure is
reported through diagnostics and represented by unavailable fallback return values.

**Dependencies**: `AudioFormat`, `PortAudioEnvironment`, `PortAudioCaptureDeviceProbe`,
`PortAudioPlaybackDeviceProbe`, `PortAudioCaptureDevice`, `PortAudioPlaybackDevice`, the
`Unavailable*` fallbacks, and `ISpeechDiagnostics`.

**Callers**: Hosts that need audio devices, and later speech-recognition/synthesis subsystems.
