package SpeechDemo {
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
        comment designRef /* Design: docs/design/speech-demo/recognition-panel-subsystem/recognition-panel-view-model.md */
        comment verificationRef /* Verification: docs/verification/speech-demo/recognition-panel-subsystem/recognition-panel-view-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech-demo/recognition-panel-subsystem/recognition-panel-view-model.yaml */
    }
}
