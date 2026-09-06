<!-- cspell:ignore ALSA portaudio -->
## PortAudioSharp2

### Purpose

PortAudioSharp2 is used as the Speech library's managed binding over PortAudio. It was chosen so
this repository could add real cross-platform audio capture and playback without authoring a full
managed PortAudio wrapper from scratch.

### Features Used

- Managed `PortAudio` initialization and device enumeration APIs
- Managed `Stream` callback-based capture/playback APIs using `float` samples
- Transitive native runtime packages for `win-x64`, `linux-x64`, `linux-aarch64`, `osx-x64`,
  and `osx-arm64`

### Integration Pattern

The library references `PortAudioSharp2` directly from `DemaConsulting.Speech.csproj`. The
internal `PortAudioApi` adapter wraps the package's managed APIs and supplements the missing
host-API enumeration functions with repository-local P/Invoke declarations against the same
native `portaudio` library name the package already uses. Public callers never consume
PortAudioSharp2 types directly; they interact only through `IAudioCaptureDevice`,
`IAudioPlaybackDevice`, their probes, and `AudioDeviceFactory`.
