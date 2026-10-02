package SpeechCli {
    part def RecognitionCommandSubsystem {
        doc /* The one speech-to-text subcommand (recognize) plus the
             * SilenceTimeoutRecognizerSession idle-timeout decorator, extending the
             * ModelCommandsSubsystem's ICliModelCatalog seam with two further members
             * (GetAudioFormat, CreateRecognizerEngineAsync) rather than introducing a
             * second, competing seam. Consumes the library's async Engine/Session
             * RecognitionSubsystem surface (ISpeechRecognizerEngine/IRecognitionSession)
             * rather than the prior synchronous, event-based ISpeechRecognizer. */

        comment designRef /* Design: docs/design/speech-cli/recognition-command-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/recognition-command-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/recognition-command-subsystem.yaml */

        part recognizeCommand : RecognizeCommand;
        part silenceTimeoutRecognizerSession : SilenceTimeoutRecognizerSession;
    }

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
    }

    part def SilenceTimeoutRecognizerSession {
        doc /* Stateless IAsyncEnumerable<SpeechRecognitionEvent> decorator wrapping an
             * IRecognitionSession's GetResultsAsync: each iteration races the wrapped
             * enumerator's MoveNextAsync against a TimeProvider-based Task.Delay via
             * Task.WhenAny, calls the wrapped session's StopAsync and raises TimedOut when
             * the delay wins before any result (partial or final) arrives, then continues
             * draining so trailing results still surface. Enforces two sequential idle
             * windows: a start-timeout grace period (defaulting to the silence-timeout
             * value) armed once before any result has arrived, then re-armed with the
             * silence-timeout value on every subsequent result from the first onward - no
             * IDisposable, no locks, and deterministic/real-wall-clock-free for unit
             * testing since cleanup is solely via the compiler-generated await using on
             * the inner enumerator. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/RecognitionCommandSubsystem/SilenceTimeoutRecognizerSession.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/RecognitionCommandSubsystem/SilenceTimeoutRecognizerSessionTests.cs */
    }
}
