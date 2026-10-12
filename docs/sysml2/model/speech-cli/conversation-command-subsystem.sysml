package SpeechCli {
    part def ConversationCommandSubsystem {
        doc /* The one voice-conversation subcommand (ask) - combining speak's
             * synthesis-then-play flow and recognize --mic's mic-listen flow into a
             * single invocation. Introduces no new ICliModelCatalog seam member: it
             * reuses SynthesisCommandSubsystem's CreateSynthesizerEngineAsync and
             * RecognitionCommandSubsystem's CreateRecognizerEngineAsync members exactly
             * as speak and recognize already do individually. Introduces its own small
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
}
