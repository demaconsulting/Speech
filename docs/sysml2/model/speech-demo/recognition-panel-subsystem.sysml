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
             * a speech recognizer engine, so the panel can be tested with a plain fake model
             * with no InternalsVisibleTo grant from the library. Exposes a single LoadAsync
             * member returning an ISpeechRecognizerEngine; per-run capture-device binding is
             * done later, directly against the returned engine's CreateSessionAsync, so a host
             * can load one engine per model and reuse it across many IRecognitionSession runs. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/IRecognizerSessionFactory.cs */
        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/recognition-panel-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/recognition-panel-subsystem.yaml (consolidated at subsystem level) */
    }

    part def RecognizerSessionFactory {
        doc /* Real seam implementation: resolves the model's installed-files directory from
             * the shared SpeechModelStore, narrows the public model to IRecognitionModel, and
             * forwards to SpeechRecognizerFactory.LoadAsync, returning the library's own
             * UnavailableSpeechRecognizerEngine for a model of the wrong role. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/RecognizerSessionFactory.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/RecognitionPanelSubsystem/RecognizerSessionFactoryTests.cs */
        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/recognition-panel-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/recognition-panel-subsystem.yaml (consolidated at subsystem level) */
    }

    part def RecognitionPanelViewModel {
        doc /* Presentation state for the speech-to-text panel: installed recognition models,
             * the async StartAsync/StopAsync streaming lifecycle driving an ISpeechRecognizerEngine
             * and a single-use IRecognitionSession, the progressive partial-then-final transcript
             * pumped from IRecognitionSession.GetResultsAsync, a CanChangeModel guard that
             * disables model selection while Listening, and honest unavailable-state reporting.
             * RecognitionStreamingState is derived from RecognitionSessionState via
             * IRecognitionSession.StateChanged rather than ad hoc assignment at each call site.
             * Caches at most one loaded engine (reused across repeated Start/Stop cycles for as
             * long as the selected model is unchanged, instead of reloading the model on every
             * click) and creates a fresh session from it on every Start, since a session is
             * single-use; invalidates the engine - stopping and releasing an active session
             * first, even while Listening - whenever the selected model changes, and invalidates
             * only the session (keeping the engine) whenever the selected capture device changes
             * or a shared device refresh replaces the underlying capture device. Implements
             * IAsyncDisposable to release an active session and engine deterministically. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/RecognitionPanelViewModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Demo.Tests/RecognitionPanelSubsystem/RecognitionPanelViewModelTests.cs */
        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-demo/recognition-panel-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/recognition-panel-subsystem.yaml (consolidated at subsystem level) */
    }
}
