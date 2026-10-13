package SpeechCli {
    part def ICliCaptureDeviceSource {
        doc /* CLI-owned seam over real-capture-device resolution (CaptureProbe,
             * CreateCaptureDevice), mirroring SynthesisCommandSubsystem's
             * ICliPlaybackDeviceSource. Exists because AudioDeviceFactory's own
             * hardware detection resolves to the honestly unavailable fallback
             * whenever no real PortAudio hardware is present, regardless of any
             * injected probe - making it unusable for deterministic microphone-mode
             * happy-path tests on a headless build agent. Both AskCommand and
             * RecognizeCommand resolve their capture devices through it. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/ICliCaptureDeviceSource.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AudioDeviceFactoryCaptureDeviceSourceTests.cs */
        comment designRef /* Design: docs/design/speech-cli/conversation-command-subsystem/i-cli-capture-device-source.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/conversation-command-subsystem/i-cli-capture-device-source.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/conversation-command-subsystem/i-cli-capture-device-source.yaml */
    }
}
