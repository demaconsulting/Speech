package SpeechCli {
    part def AudioDeviceFactoryPlaybackDeviceSource {
        doc /* Production ICliPlaybackDeviceSource implementation, forwarding every call
             * unchanged to a real, composed AudioDeviceFactory - including its
             * hardware-detection behavior - so SpeakCommand's real playback-device
             * resolution is exactly what it was before this seam was introduced. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/AudioDeviceFactoryPlaybackDeviceSource.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/AudioDeviceFactoryPlaybackDeviceSourceTests.cs */
        comment designRef /* Design: docs/design/speech-cli/synthesis-command-subsystem.md (documented within the subsystem doc; no dedicated unit doc) */
        comment verificationRef /* Verification: docs/verification/speech-cli/synthesis-command-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/synthesis-command-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */
    }
}
