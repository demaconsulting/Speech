package SpeechCli {
    part def RecognizeCommand {
        doc /* Implements recognize: resolves exactly one of --input/--mic, resolves and
             * validates the requested recognition model, validates --stt-param values via
             * the reused ParameterBagParser, loads an ISpeechRecognizerEngine via
             * ICliModelCatalog.CreateRecognizerEngineAsync and binds an IRecognitionSession
             * to the resolved capture device, drives file-input mode to completion via
             * StartAsync's blocking file delivery followed by an explicit drain StopAsync,
             * wires mic-input mode to Ctrl+C (session.StopAsync) and an always-constructed
             * SilenceTimeoutRecognizerSession decorator around GetResultsAsync (file mode
             * pumps the session directly, with no decorator needed), filters
             * console output per --interim/--final-only, writes only final results to
             * --output-text, and disposes the engine and session (await using) on every
             * exit path. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/RecognitionCommandSubsystem/RecognizeCommand.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/RecognitionCommandSubsystem/RecognizeCommandTests.cs */
        comment designRef /* Design: docs/design/speech-cli/recognition-command-subsystem.md (documented within the subsystem doc; no dedicated unit doc) */
        comment verificationRef /* Verification: docs/verification/speech-cli/recognition-command-subsystem.md (verified within the subsystem doc; no dedicated unit doc) */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/recognition-command-subsystem.yaml (requirements held in the subsystem file; no dedicated unit file) */
    }
}
