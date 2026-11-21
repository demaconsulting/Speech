package SpeechDemo_SynthesisPanelViewModel {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_DeviceSelectionViewModel::*;
    private import SpeechDemo_IAudioDeviceService::*;
    private import SpeechDemo_IModelCatalogService::*;
    private import SpeechDemo_ISynthesizerSessionFactory::*;
    private import SpeechDemo_ModelSettingsViewModel::*;

    enum def SynthesisPlaybackState {
        enum Idle;
        enum Synthesizing;
        enum Playing;
        enum Error;
    }

    part def SynthesisPanelViewModel {
        doc /* Presentation state for the text-to-speech panel: installed synthesis models, the embedded settings
             * panel for the selected model, example audio tag hints, the async PlayAsync/StopAsync lifecycle driving
             * an ISpeechSynthesizerEngine and an ISynthesisSession, and a CanChangeModel guard that disables
             * model/voice selection while Synthesizing or Playing. SynthesisPlaybackState is derived from
             * SynthesisSessionState via ISynthesisSession.StateChanged rather than ad hoc assignment at each call
             * site. Caches at most one loaded engine (reloaded only when the selected model or the settings panel's
             * built parameter values change) and at most one session created from it (recreated only when the engine
             * was just reloaded or the selected playback device changes), reusing both across many Play calls instead
             * of recomposing them on every click - fixing the bug where a prior design recreated the engine and
             * session on every Play. Forwards Settings.BuildValueBag() to the session factory on an engine reload, so
             * a selected voice genuinely changes synthesized output. Re-reads the catalog on ReferenceVoicesChanged
             * keeping the chosen settings, and shows the voice-cloning consent note and installed pack attribution
             * for Chatterbox Turbo. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/SynthesisPanelSubsystem/SynthesisPanelViewModel.cs", "src/DemaConsulting.Speech.Demo/SynthesisPanelSubsystem/SynthesisPlaybackState.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/SynthesisPanelSubsystem/SynthesisPanelViewModelTests.cs");
            designFiles = ("docs/design/speech-demo/synthesis-panel-subsystem/synthesis-panel-view-model.md");
            verificationFiles = ("docs/verification/speech-demo/synthesis-panel-subsystem/synthesis-panel-view-model.md");
            requirementFiles = ("docs/reqstream/speech-demo/synthesis-panel-subsystem/synthesis-panel-view-model.yaml");
        }

        // Uses the catalog, settings, voices and synthesizer seams to speak text and handle session states
        // and errors.
        dependency SynthesisPanelViewModel to DeviceSelectionViewModel;
        dependency SynthesisPanelViewModel to IAudioDeviceService;
        dependency SynthesisPanelViewModel to IModelCatalogService;
        dependency SynthesisPanelViewModel to ISynthesizerSessionFactory;
        dependency SynthesisPanelViewModel to ModelSettingsViewModel;
        dependency SynthesisPanelViewModel to SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboEnglishSynthesisModel::OnnxChatterboxTurboEnglishSynthesisModel;
        dependency SynthesisPanelViewModel to Speech_AudioDeviceSelection::AudioDeviceSelection;
        dependency SynthesisPanelViewModel to Speech_AudioTagDescriptor::AudioTagDescriptor;
        dependency SynthesisPanelViewModel to Speech_ISpeechSynthesizerEngine::ISpeechSynthesizerEngine;
        dependency SynthesisPanelViewModel to Speech_ISynthesisSession::ISynthesisSession;
        dependency SynthesisPanelViewModel to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
        dependency SynthesisPanelViewModel to Speech_SpeechModelContract::SpeechModelContract;
        dependency SynthesisPanelViewModel to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency SynthesisPanelViewModel to Speech_SpeechSynthesizerUnavailableException::SpeechSynthesizerUnavailableException;
        dependency SynthesisPanelViewModel to Speech_SynthesisEngineBusyException::SynthesisEngineBusyException;
        dependency SynthesisPanelViewModel to Speech_SynthesisSessionFaultedException::SynthesisSessionFaultedException;
        dependency SynthesisPanelViewModel to Speech_SynthesisSessionState::SynthesisSessionState;
        dependency SynthesisPanelViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
