package Speech {
    part def AudioSubsystem {
        doc /* Audio capture/playback device and probe contracts, persisted name-based selection,
             * honest unavailable fallbacks, real PortAudio-backed implementations, and the
             * internal PortAudio child subsystem that isolates native interop behind a seam. */

        comment designRef /* Design: docs/design/speech/audio-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem.yaml */

        part audioFormat : AudioFormat;
        part windowedSincLowpassFilter : WindowedSincLowpassFilter;
        part audioDeviceDescription : AudioDeviceDescription;
        part audioDeviceSelection : AudioDeviceSelection;
        part iAudioCaptureDevice : IAudioCaptureDevice;
        part iAudioPlaybackDevice : IAudioPlaybackDevice;
        part iAudioCaptureDeviceProbe : IAudioCaptureDeviceProbe;
        part iAudioPlaybackDeviceProbe : IAudioPlaybackDeviceProbe;
        part audioDeviceFactory : AudioDeviceFactory;
        part portAudioCaptureDeviceProbe : PortAudioCaptureDeviceProbe;
        part portAudioPlaybackDeviceProbe : PortAudioPlaybackDeviceProbe;
        part portAudioCaptureDevice : PortAudioCaptureDevice;
        part portAudioPlaybackDevice : PortAudioPlaybackDevice;
        part unavailableAudioCaptureDevice : UnavailableAudioCaptureDevice;
        part unavailableAudioPlaybackDevice : UnavailableAudioPlaybackDevice;
        part unavailableAudioCaptureDeviceProbe : UnavailableAudioCaptureDeviceProbe;
        part unavailableAudioPlaybackDeviceProbe : UnavailableAudioPlaybackDeviceProbe;
        part audioDeviceUnavailableException : AudioDeviceUnavailableException;
        part portAudio : PortAudio;
    }
}
