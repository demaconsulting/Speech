package Speech {
    part def SpeechRecognizerUnavailableException {
        doc /* Exception signaling that an operational member of an unavailable speech recognizer
             * engine or session was invoked, or that a session which claimed to be available
             * failed on first use (for example the capture device going unavailable mid-session). */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognizerUnavailableException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/UnavailableSpeechRecognizerEngineTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/speech-recognizer-unavailable-exception.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/speech-recognizer-unavailable-exception.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/speech-recognizer-unavailable-exception.yaml */
    }
}
