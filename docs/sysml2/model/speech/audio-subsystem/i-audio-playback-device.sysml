package Speech {
    part def IAudioPlaybackDevice {
        doc /* Contract for an audio playback device (availability flag, start/stop/write,
             * reported channel count/sample rate), independent of any real hardware or native
             * runtime. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/IAudioPlaybackDevice.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/i-audio-playback-device.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/i-audio-playback-device.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/i-audio-playback-device.yaml */
    }
}
