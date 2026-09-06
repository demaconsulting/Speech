package Speech {
    part def SpeechRecognizerUnavailableException {
        doc /* Exception signaling that an operational member of an unavailable speech recognizer
             * was invoked, or that a recognizer which claimed to be available failed on first use. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognizerUnavailableException.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/UnavailableSpeechRecognizerTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem.md (documented as a supporting fallback type) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem.md (documented as a supporting fallback type) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/unavailable-speech-recognizer.yaml */
    }
}
