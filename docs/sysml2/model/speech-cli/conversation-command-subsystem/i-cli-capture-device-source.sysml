package SpeechCli {
    part def ICliCaptureDeviceSource {
        doc /* CLI-owned seam over real-capture-device resolution (CaptureProbe,
             * CreateCaptureDevice), mirroring SynthesisCommandSubsystem's
             * ICliPlaybackDeviceSource exactly. Exists because AudioDeviceFactory's
             * own hardware-detection resolves to the honestly unavailable fallback
             * whenever no real PortAudio hardware is present, regardless of any
             * injected probe - making it unusable for deterministic mic-mode
             * happy-path tests on headless CI. Unlike ask, RecognizeCommand still
             * resolves its capture device directly from an injected
             * AudioDeviceFactory, a known, pre-existing, out-of-scope limitation left
             * unchanged by this seam's introduction. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/ICliCaptureDeviceSource.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AskCommandTests.cs */
    }
}
