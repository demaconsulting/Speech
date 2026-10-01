package Speech {
    part def SpeechRecognizerFactory {
        doc /* Composition entry point whose LoadAsync overloads return a working
             * ISpeechRecognizerEngine for an installed recognition model, or an honest
             * UnavailableSpeechRecognizerEngine fallback, without ever throwing for an ordinary
             * machine state. Loading runs on a dedicated worker thread so the expensive native
             * backend construction never blocks the calling thread. A capture device is bound
             * later, per session, via ISpeechRecognizerEngine.CreateSessionAsync - not here - so
             * one loaded engine can be reused across many devices or many sequential sessions. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognizerFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SpeechRecognizerFactoryTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/speech-recognizer-factory.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/speech-recognizer-factory.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/speech-recognizer-factory.yaml */
    }
}