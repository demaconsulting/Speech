package Speech {
    part def IAudioCaptureDevice {
        doc /* Contract for an audio capture device (availability flag, start/stop, captured-frame
             * event), independent of any real hardware or native runtime. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/IAudioCaptureDevice.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/i-audio-capture-device.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/i-audio-capture-device.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/i-audio-capture-device.yaml */
    }
}
