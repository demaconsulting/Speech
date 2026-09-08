package Speech {
    part def WavFileAudioCaptureDevice {
        doc /* File-backed IAudioCaptureDevice implementation that synchronously reads a mono,
             * 16-bit PCM WAV file and delivers it through FrameCaptured, always available and
             * exposing one additive EndOfFileReached event raised once the file is fully
             * consumed, since the interface itself has no end-of-stream concept. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/WavFileAudioCaptureDevice.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/WavFileAudioCaptureDeviceTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/wav-file-audio-capture-device.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/wav-file-audio-capture-device.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/wav-file-audio-capture-device.yaml */
    }
}
