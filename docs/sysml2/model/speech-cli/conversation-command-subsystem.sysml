package SpeechCli {
    part def ConversationCommandSubsystem {
        doc /* The one voice-conversation subcommand (ask) - combining speak's
             * synthesis-then-play flow and recognize --mic's mic-listen flow into a
             * single invocation. It uses the ICliModelCatalog members that
             * SynthesisCommandSubsystem (CreateSynthesizerEngineAsync) and
             * RecognitionCommandSubsystem (CreateRecognizerEngineAsync) use, and owns
             * a small ICliCaptureDeviceSource seam over real capture-device
             * resolution, mirroring SynthesisCommandSubsystem's
             * ICliPlaybackDeviceSource, so the listen phase runs deterministically in
             * tests without real PortAudio hardware. */

        comment designRef /* Design: docs/design/speech-cli/conversation-command-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/conversation-command-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/conversation-command-subsystem.yaml */

        part askCommand : AskCommand;
        part cliCaptureDeviceSource : ICliCaptureDeviceSource;
        part audioDeviceFactoryCaptureDeviceSource : AudioDeviceFactoryCaptureDeviceSource;
    }
}
