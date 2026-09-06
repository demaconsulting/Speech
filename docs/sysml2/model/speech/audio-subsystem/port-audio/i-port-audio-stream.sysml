package Speech {
    part def IPortAudioStream {
        doc /* Internal seam for a started or startable PortAudio callback stream, allowing unit
             * tests to simulate capture and playback without loading a native runtime. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/PortAudio/IPortAudioStream.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioCaptureDeviceTests.cs; test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioPlaybackDeviceTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/port-audio/i-port-audio-stream.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/port-audio/i-port-audio-stream.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/port-audio/i-port-audio-stream.yaml */
    }
}
