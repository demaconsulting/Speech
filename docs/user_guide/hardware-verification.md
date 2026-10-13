## Hardware Verification Boundary

Automated tests in this repository verify only the seam-driven logic that is safe for CI:
preferred-host selection, name-based device matching, fallback to the host API's default device,
and degradation to unavailable devices or `AudioDeviceUnavailableException` when PortAudio
initialization or stream opening fails.

Actual end-to-end hardware I/O still requires manual/local verification on a machine with a real
microphone or speaker. CI cannot honestly claim coverage for opening a physical device and moving
audio through it. The same applies to recognition accuracy and synthesized speech quality: the
recognition and synthesis pipelines are each verified against a deterministic test engine, so
proving that real speech becomes correct text - or that rendered text becomes intelligible speech
through a real model - is also a manual/local activity.
