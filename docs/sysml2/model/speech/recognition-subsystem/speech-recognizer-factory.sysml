package Speech {
    part def SpeechRecognizerFactory {
        doc /* Composition entry point that returns a working speech recognizer for an installed recognition model and available capture device, or an honest unavailable fallback, without ever throwing for an ordinary machine state. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognizerFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SpeechRecognizerFactoryTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/speech-recognizer-factory.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/speech-recognizer-factory.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/speech-recognizer-factory.yaml */
    }
}