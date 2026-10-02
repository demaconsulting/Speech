package Speech {
    part def SpeechRecognizerUnavailableException {
        doc /* Exception signaling that an operational member of an unavailable speech recognizer
             * engine or session was invoked, or that a session which claimed to be available
             * failed on first use (for example the capture device going unavailable mid-session). */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognizerUnavailableException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/UnavailableSpeechRecognizerEngineTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem.md (documented as a supporting fallback type) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem.md (documented as a supporting fallback type) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/unavailable-speech-recognizer-engine.yaml */
    }
}
