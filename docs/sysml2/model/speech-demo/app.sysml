package SpeechDemo_App {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_AppLaunchOptions::*;
    private import SpeechDemo_AudioDeviceService::*;
    private import SpeechDemo_DeviceSelectionViewModel::*;
    private import SpeechDemo_MainWindowViewModel::*;
    private import SpeechDemo_ModelCatalogComposition::*;
    private import SpeechDemo_ModelCatalogService::*;
    private import SpeechDemo_ModelCatalogViewModel::*;
    private import SpeechDemo_RecognitionPanelViewModel::*;
    private import SpeechDemo_RecognizerSessionFactory::*;
    private import SpeechDemo_SynthesisPanelViewModel::*;
    private import SpeechDemo_SynthesizerSessionFactory::*;

    part def App {
        doc /* The Avalonia application object and the demo's manual composition root: builds the library entry points
             * and demo service adapters and wires them into the shell. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/App.axaml.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/SpeechDemoTests.cs", "test/DemaConsulting.Speech.Demo.Tests/AppTests.cs");
            designFiles = ("docs/design/speech-demo/app.md");
            verificationFiles = ("docs/verification/speech-demo/app.md");
            requirementFiles = ("docs/reqstream/speech-demo/app.yaml");
        }

        // Composition root: builds the catalog, store, audio and voice services and wires them into the
        // Avalonia view models.
        dependency App to AppLaunchOptions;
        dependency App to AudioDeviceService;
        dependency App to DeviceSelectionViewModel;
        dependency App to MainWindowViewModel;
        dependency App to ModelCatalogComposition;
        dependency App to ModelCatalogService;
        dependency App to ModelCatalogViewModel;
        dependency App to RecognitionPanelViewModel;
        dependency App to RecognizerSessionFactory;
        dependency App to SynthesisPanelViewModel;
        dependency App to SynthesizerSessionFactory;
        dependency App to SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboEnglishSynthesisModel::OnnxChatterboxTurboEnglishSynthesisModel;
        dependency App to SpeechOnnxChatterboxTurbo_SpeechModelCatalogChatterboxTurboExtensions::SpeechModelCatalogChatterboxTurboExtensions;
        dependency App to SpeechOnnxKokoro_SpeechModelCatalogKokoroExtensions::SpeechModelCatalogKokoroExtensions;
        dependency App to SpeechOnnxNemotronStt_SpeechModelCatalogNemotronSttExtensions::SpeechModelCatalogNemotronSttExtensions;
        dependency App to SpeechOnnxPiper_SpeechModelCatalogPiperExtensions::SpeechModelCatalogPiperExtensions;
        dependency App to SpeechSherpa_SpeechModelCatalogSherpaExtensions::SpeechModelCatalogSherpaExtensions;
        dependency App to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency App to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
        dependency App to Speech_SpeechModelCatalog::SpeechModelCatalog;
        dependency App to Speech_SpeechModelDownloaderOptions::SpeechModelDownloaderOptions;
        dependency App to Speech_SpeechModelStore::SpeechModelStore;
        dependency App to OtsDependencies::Avalonia;
    }
}
