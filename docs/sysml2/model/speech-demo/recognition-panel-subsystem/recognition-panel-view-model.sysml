package SpeechDemo_RecognitionPanelViewModel {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_CaptureDebugRecorder::*;
    private import SpeechDemo_DeviceSelectionViewModel::*;
    private import SpeechDemo_IAudioDeviceService::*;
    private import SpeechDemo_IModelCatalogService::*;
    private import SpeechDemo_IRecognizerSessionFactory::*;

    enum def RecognitionStreamingState {
        enum Idle;
        enum Listening;
        enum Error;
    }

    part def RecognitionPanelViewModel {
        doc /* Presentation state for the speech-to-text panel: installed recognition models, the async
             * StartAsync/StopAsync streaming lifecycle driving an ISpeechRecognizerEngine and a single-use
             * IRecognitionSession, the progressive partial-then-final transcript pumped from
             * IRecognitionSession.GetResultsAsync, a CanChangeModel guard that disables model selection while
             * Listening, and honest unavailable-state reporting. RecognitionStreamingState is derived from
             * RecognitionSessionState via IRecognitionSession.StateChanged rather than ad hoc assignment at each call
             * site. Caches at most one loaded engine (reused across repeated Start/Stop cycles for as long as the
             * selected model is unchanged, instead of reloading the model on every click) and creates a fresh session
             * from it on every Start, since a session is single-use; invalidates the engine - stopping and releasing
             * an active session first, even while Listening - whenever the selected model changes, and invalidates
             * only the session (keeping the engine) whenever the selected capture device changes or a shared device
             * refresh replaces the underlying capture device. Implements IAsyncDisposable to release an active
             * session and engine deterministically. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/RecognitionPanelViewModel.cs", "src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/RecognitionStreamingState.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/RecognitionPanelSubsystem/RecognitionPanelViewModelTests.cs");
            designFiles = ("docs/design/speech-demo/recognition-panel-subsystem/recognition-panel-view-model.md");
            verificationFiles = ("docs/verification/speech-demo/recognition-panel-subsystem/recognition-panel-view-model.md");
            requirementFiles = ("docs/reqstream/speech-demo/recognition-panel-subsystem/recognition-panel-view-model.yaml");
        }

        // Uses the catalog, device and recognizer seams to stream speech recognition and handle session
        // failures.
        dependency RecognitionPanelViewModel to CaptureDebugRecorder;
        dependency RecognitionPanelViewModel to DeviceSelectionViewModel;
        dependency RecognitionPanelViewModel to IAudioDeviceService;
        dependency RecognitionPanelViewModel to IModelCatalogService;
        dependency RecognitionPanelViewModel to IRecognizerSessionFactory;
        dependency RecognitionPanelViewModel to Speech_IRecognitionSession::IRecognitionSession;
        dependency RecognitionPanelViewModel to Speech_ISpeechRecognizerEngine::ISpeechRecognizerEngine;
        dependency RecognitionPanelViewModel to Speech_RecognitionEngineBusyException::RecognitionEngineBusyException;
        dependency RecognitionPanelViewModel to Speech_RecognitionSessionFaultedException::RecognitionSessionFaultedException;
        dependency RecognitionPanelViewModel to Speech_RecognitionSessionState::RecognitionSessionState;
        dependency RecognitionPanelViewModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency RecognitionPanelViewModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency RecognitionPanelViewModel to Speech_SpeechRecognitionResult::SpeechRecognitionResult;
        dependency RecognitionPanelViewModel to Speech_SpeechRecognizerUnavailableException::SpeechRecognizerUnavailableException;
        dependency RecognitionPanelViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
