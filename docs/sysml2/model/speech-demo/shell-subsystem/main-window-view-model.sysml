package SpeechDemo_MainWindowViewModel {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_DemoPanelViewModel::*;
    private import SpeechDemo_DeviceSelectionViewModel::*;
    private import SpeechDemo_ModelCatalogViewModel::*;
    private import SpeechDemo_RecognitionPanelViewModel::*;
    private import SpeechDemo_SynthesisPanelViewModel::*;

    part def MainWindowViewModel {
        doc /* Navigation state for the application window: the ordered panel list and the currently selected panel. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ShellSubsystem/MainWindowViewModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ShellSubsystem/MainWindowViewModelTests.cs");
            designFiles = ("docs/design/speech-demo/shell-subsystem/main-window-view-model.md");
            verificationFiles = ("docs/verification/speech-demo/shell-subsystem/main-window-view-model.md");
            requirementFiles = ("docs/reqstream/speech-demo/shell-subsystem/main-window-view-model.yaml");
        }

        // Puts the device, catalog, recognition and synthesis panels into one navigable list.
        dependency MainWindowViewModel to DemoPanelViewModel;
        dependency MainWindowViewModel to DeviceSelectionViewModel;
        dependency MainWindowViewModel to ModelCatalogViewModel;
        dependency MainWindowViewModel to RecognitionPanelViewModel;
        dependency MainWindowViewModel to SynthesisPanelViewModel;
        dependency MainWindowViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
