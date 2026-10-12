package SpeechDemo {
    part def IRecognizerSessionFactory {
        doc /* Demo-owned seam accepting the library's public ISpeechModel contract to compose
             * a speech recognizer engine, so the panel can be tested with a plain fake model
             * with no InternalsVisibleTo grant from the library. Exposes a single LoadAsync
             * member returning an ISpeechRecognizerEngine; per-run capture-device binding is
             * done later, directly against the returned engine's CreateSessionAsync, so a host
             * can load one engine per model and reuse it across many IRecognitionSession runs. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/IRecognizerSessionFactory.cs */
        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem/recognizer-session-factory.md (documented together with its sole implementation, RecognizerSessionFactory) */
        comment verificationRef /* Verification: docs/verification/speech-demo/recognition-panel-subsystem/recognizer-session-factory.md (documented together with its sole implementation, RecognizerSessionFactory) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/recognition-panel-subsystem/recognizer-session-factory.yaml */
    }

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
