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
}
