package Speech {
    part def ISpeechRecognizerEngine {
        doc /* Layer 3 contract for one loaded speech-inference engine bound to one recognition
             * model: an availability flag plus CreateSessionAsync, which leases exclusive use of
             * the engine to a new IRecognitionSession for one capture device. Exposes no
             * inference-engine type. Never throws for an ordinary unavailable machine state;
             * throws RecognitionEngineBusyException only when a session is already leased. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/ISpeechRecognizerEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SherpaOnnxSpeechRecognizerEngineTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/i-speech-recognizer-engine.md */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/i-speech-recognizer-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/i-speech-recognizer-engine.yaml */
    }
}
