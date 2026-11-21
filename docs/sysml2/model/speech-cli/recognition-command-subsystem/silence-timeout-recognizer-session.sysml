package SpeechCli_SilenceTimeoutRecognizerSession {
    private import SoftwareItemMetadata::*;

    part def SilenceTimeoutRecognizerSession {
        doc /* Stateless IAsyncEnumerable<SpeechRecognitionEvent> decorator wrapping an IRecognitionSession's
             * GetResultsAsync: each iteration races the wrapped enumerator's MoveNextAsync against a
             * TimeProvider-based Task.Delay via Task.WhenAny, calls the wrapped session's StopAsync and raises
             * TimedOut when the delay wins before any result (partial or final) arrives, then continues draining so
             * trailing results still surface. Enforces two sequential idle windows: a start-timeout grace period
             * (defaulting to the silence-timeout value) armed once before any result has arrived, then re-armed with
             * the silence-timeout value on every subsequent result from the first onward - no IDisposable, no locks,
             * and deterministic/real-wall-clock-free for unit testing since cleanup is solely via the
             * compiler-generated await using on the inner enumerator. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/RecognitionCommandSubsystem/SilenceTimeoutRecognizerSession.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/RecognitionCommandSubsystem/SilenceTimeoutRecognizerSessionTests.cs");
            designFiles = ("docs/design/speech-cli/recognition-command-subsystem/silence-timeout-recognizer-session.md");
            verificationFiles = ("docs/verification/speech-cli/recognition-command-subsystem/silence-timeout-recognizer-session.md");
            requirementFiles = ("docs/reqstream/speech-cli/recognition-command-subsystem/silence-timeout-recognizer-session.yaml");
        }

        // Wraps a recognition session's result stream and ends it when no new results come before the
        // timeout.
        dependency SilenceTimeoutRecognizerSession to Speech_IRecognitionSession::IRecognitionSession;
        dependency SilenceTimeoutRecognizerSession to Speech_SpeechRecognitionResult::SpeechRecognitionResult;
    }
}
