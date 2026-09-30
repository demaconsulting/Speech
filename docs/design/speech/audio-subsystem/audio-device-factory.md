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
- **RefreshDevices()**: Forces the underlying PortAudio device table to be re-scanned by
  delegating to `PortAudioEnvironment.Refresh()`, then re-evaluates `CaptureProbe`/`PlaybackProbe`
  against the new `IsInitialized` outcome - but only for a probe that was not explicitly injected
  at construction. Reports a Warning diagnostic when the post-refresh runtime is still
  unavailable, or an Info diagnostic on success, through the same `ISpeechDiagnostics` sink and
  `"AudioSubsystem"` category used elsewhere in this type.

**Error Handling**: No member throws during composition. PortAudio initialization failure is
reported through diagnostics and represented by unavailable fallback return values.
`RefreshDevices()` is the one deliberate exception to this "never throws" policy: it propagates
`AudioDeviceInUseException` from `PortAudioEnvironment.Refresh()` untouched when any capture or
playback device created from this factory's environment currently has an open/started stream, and
in that case neither the environment nor this factory's probes are modified.

**Dependencies**: `AudioFormat`, `PortAudioEnvironment`, `PortAudioCaptureDeviceProbe`,
`PortAudioPlaybackDeviceProbe`, `PortAudioCaptureDevice`, `PortAudioPlaybackDevice`, the
`Unavailable*` fallbacks, `AudioDeviceInUseException`, and `ISpeechDiagnostics`.

**Callers**: Hosts that need audio devices, and later speech-recognition/synthesis subsystems.
