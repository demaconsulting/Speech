package Speech {
    part def IPortAudioApi {
        doc /* Internal seam for PortAudio initialization, host/device metadata access, and
             * opening capture or playback streams without exposing PortAudioSharp2 types. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/PortAudio/IPortAudioApi.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudio/PortAudioEnvironmentTests.cs; test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioCaptureDeviceTests.cs; test/DemaConsulting.Speech.Tests/AudioSubsystem/PortAudioPlaybackDeviceTests.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/port-audio/i-port-audio-api.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/port-audio/i-port-audio-api.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/port-audio/i-port-audio-api.yaml */
    }
}
