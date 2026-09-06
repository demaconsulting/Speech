package SpeechDemo {
    part def RecognitionPanelSubsystem {
        doc /* The speech-to-text panel: a demo-owned session seam over the library's
             * SpeechRecognizerFactory narrowing a public model to the library's recognition
             * role, and the Start/Stop streaming lifecycle with progressive partial-then-final
             * transcript rendering and honest reporting of every unavailable state. */

        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/recognition-panel-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/recognition-panel-subsystem.yaml */

        part iRecognizerSessionFactory : IRecognizerSessionFactory;
        part recognizerSessionFactory : RecognizerSessionFactory;
        part recognitionPanelViewModel : RecognitionPanelViewModel;
    }

    part def IRecognizerSessionFactory {
        doc /* Demo-owned seam accepting the library's public ISpeechModel contract to compose
             * a speech recognizer, so the panel can be tested with a plain fake model with no
             * InternalsVisibleTo grant from the library. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/IRecognizerSessionFactory.cs */
        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/recognition-panel-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/recognition-panel-subsystem.yaml (consolidated at subsystem level) */
    }

    part def RecognizerSessionFactory {
        doc /* Real seam implementation: resolves the model's installed-files directory from
             * the shared SpeechModelStore, narrows the public model to IRecognitionModel, and
             * forwards to SpeechRecognizerFactory.Create, returning the library's own
             * UnavailableSpeechRecognizer for a model of the wrong role. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/RecognizerSessionFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/RecognitionPanelSubsystem/RecognizerSessionFactoryTests.cs */
        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/recognition-panel-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/recognition-panel-subsystem.yaml (consolidated at subsystem level) */
    }

    part def RecognitionPanelViewModel {
        doc /* Presentation state for the speech-to-text panel: installed recognition models,
             * the Start/Stop streaming lifecycle, the progressive partial-then-final transcript,
             * a CanChangeModel guard that disables model selection while Listening, and honest
             * unavailable-state reporting. Implements IDisposable to release an active
             * recognizer and its capture device deterministically. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/RecognitionPanelViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/RecognitionPanelSubsystem/RecognitionPanelViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/recognition-panel-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/recognition-panel-subsystem.yaml (consolidated at subsystem level) */
    }
}
