package SpeechCli {
    part def ConversationCommandSubsystem {
        doc /* The one voice-conversation subcommand (ask) - an eleventh subcommand added
             * after all ten scaffolded subcommands were implemented - combining speak's
             * synthesis-then-play flow and recognize --mic's mic-listen flow into a
             * single invocation. Introduces no new ICliModelCatalog seam member: it
             * reuses SynthesisCommandSubsystem's CreateSynthesizer and
             * RecognitionCommandSubsystem's CreateRecognizer members exactly as speak
             * and recognize already do individually. Introduces its own small
             * ICliCaptureDeviceSource seam over real capture-device resolution,
             * mirroring SynthesisCommandSubsystem's ICliPlaybackDeviceSource, letting
             * ask's mic-mode happy-path tests run deterministically without depending
             * on real PortAudio hardware. */

        comment designRef /* Design: docs/design/speech-cli/conversation-command-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/conversation-command-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/conversation-command-subsystem.yaml */

        part askCommand : AskCommand;
        part cliCaptureDeviceSource : ICliCaptureDeviceSource;
        part audioDeviceFactoryCaptureDeviceSource : AudioDeviceFactoryCaptureDeviceSource;
    }

    part def AskCommand {
        doc /* Implements ask: resolves prompt text from --text/--file/piped stdin,
             * resolves and validates both a synthesis-role --tts-model and a
             * recognition-role --stt-model, validates --tts-param/--stt-param values
             * via the reused ParameterBagParser, speaks the resolved text through a
             * real playback device (Phase 1), then - once playback finishes - listens
             * through a real capture device resolved via ICliCaptureDeviceSource
             * (Phase 2) until the first final recognition result arrives, a reused
             * SilenceTimeoutRecognizerSession times out, or Ctrl+C is pressed. Phase
             * 2's recognizer (and its capture device) is now constructed concurrently
             * with Phase 1's speak/playback wait, via a background task, rather than
             * only once playback finishes, so the expensive model-load step overlaps
             * with prompt playback and the turnaround gap before listening starts is
             * minimized; only the model load is pre-warmed, never Start() (real
             * microphone capture), which still begins only once Phase 2 genuinely
             * runs. A genuine Ctrl+C during Phase 2 is distinguished from a legitimate
             * empty result (silence/start timeout) via the shared CancellationToken, and is
             * reported the same way a Phase-1 cancellation already is (WriteError),
             * skipping the output-writing step entirely rather than printing/writing
             * empty text as a false success. Otherwise, the final recognized text is
             * printed (or written to --output-text), and the synthesizer, playback
             * device, recognizer, and silence-timeout session (when present) are
             * disposed on every exit path. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/AskCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AskCommandTests.cs */
    }

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

    part def AudioDeviceFactoryCaptureDeviceSource {
        doc /* Production ICliCaptureDeviceSource implementation, forwarding every call
             * unchanged to a real, composed AudioDeviceFactory - including its
             * hardware-detection behavior - so AskCommand's real capture-device
             * resolution is exactly what it was before this seam was introduced. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/AudioDeviceFactoryCaptureDeviceSource.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AudioDeviceFactoryCaptureDeviceSourceTests.cs */
    }
}
