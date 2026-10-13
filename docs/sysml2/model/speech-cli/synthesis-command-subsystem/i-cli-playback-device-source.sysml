package SpeechCli {
    part def ICliPlaybackDeviceSource {
        doc /* CLI-owned seam over real-playback-device resolution (PlaybackProbe,
             * CreatePlaybackDevice), mirroring ICliModelCatalog's "CLI-owned seam over a
             * sealed library type" pattern. Exists because AudioDeviceFactory's own
             * hardware-detection resolves to the honestly unavailable fallback whenever
             * no real PortAudio hardware is present, regardless of any injected probe -
             * making its public constructor unusable for deterministic tests on headless
             * CI, while its internal, hardware-injectable constructor is deliberately not
             * reachable from DemaConsulting.Speech.Cli.Tests. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/SynthesisCommandSubsystem/ICliPlaybackDeviceSource.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/SynthesisCommandSubsystem/SpeakCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/synthesis-command-subsystem.md (documented within the subsystem doc; no dedicated unit doc) */
        comment verificationRef /* Verification: docs/verification/speech-cli/synthesis-command-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/synthesis-command-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */
    }
}
