package SpeechCli {
    part def AskCommand {
        doc /* Implements ask: resolves prompt text from --text/--file/piped stdin,
             * resolves and validates both a synthesis-role --tts-model and a
             * recognition-role --stt-model, validates --tts-param/--stt-param values
             * via the reused ParameterBagParser, speaks the resolved text through a
             * real playback device via an ISpeechSynthesizerEngine/ISynthesisSession
             * (speak step), then - once playback finishes - listens through a real
             * capture device resolved via ICliCaptureDeviceSource using an
             * ISpeechRecognizerEngine/IRecognitionSession wrapped in a reused
             * SilenceTimeoutRecognizerSession (listen step) until the first final
             * recognition result arrives, the silence/start timeout elapses, or Ctrl+C
             * is pressed. The listen step's recognizer engine and session (and its capture
             * device) are constructed concurrently with the speak step's SpeakAsync wait,
             * via a background Task.Run (PrewarmRecognizerAsync), rather than only once
             * playback finishes, so the expensive model-load step overlaps with prompt
             * playback and the turnaround gap before listening starts is minimized;
             * only the model load is pre-warmed, never session.StartAsync (real
             * microphone capture), which still begins only once the listen step genuinely
             * runs - if the speak step is canceled or fails first, the pre-warmed engine and
             * session are disposed fire-and-forget via DisposePrewarmedRecognizerAsync
             * instead of being awaited directly. A genuine Ctrl+C during the listen step is
             * distinguished from a legitimate empty result (silence/start timeout) via
             * the shared CancellationToken, and is reported the same way a speak-step
             * cancellation already is (WriteError), skipping the output-writing step
             * entirely rather than printing/writing empty text as a false success.
             * Otherwise, the final recognized text is printed (or written to
             * --output-text), and the synthesis session/engine, playback device,
             * recognition session/engine, and capture device are all disposed (await
             * using) on every exit path. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ConversationCommandSubsystem/AskCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ConversationCommandSubsystem/AskCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/conversation-command-subsystem.md (documented within the subsystem doc; no dedicated unit doc) */
        comment verificationRef /* Verification: docs/verification/speech-cli/conversation-command-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/conversation-command-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */
    }
}
