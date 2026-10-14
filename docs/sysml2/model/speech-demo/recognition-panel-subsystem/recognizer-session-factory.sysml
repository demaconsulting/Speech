package SpeechDemo {
    part def RecognizerSessionFactory {
        doc /* Real seam implementation: resolves the model's installed-files directory from
             * the shared SpeechModelStore, narrows the public model to IRecognitionModel, and
             * forwards to SpeechRecognizerFactory.LoadAsync, returning the library's own
             * UnavailableSpeechRecognizerEngine for a model of the wrong role. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/RecognizerSessionFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/RecognitionPanelSubsystem/RecognizerSessionFactoryTests.cs */
        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem/recognizer-session-factory.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/recognition-panel-subsystem/recognizer-session-factory.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/recognition-panel-subsystem/recognizer-session-factory.yaml */
    }
}
