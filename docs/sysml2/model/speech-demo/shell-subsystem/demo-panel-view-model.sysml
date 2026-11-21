package SpeechDemo_DemoPanelViewModel {
    private import SoftwareItemMetadata::*;

    part def DemoPanelViewModel {
        doc /* One navigable panel entry: a display title paired with the panel view model the shell was composed
             * with. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ShellSubsystem/DemoPanelViewModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ShellSubsystem/DemoPanelViewModelTests.cs");
            designFiles = ("docs/design/speech-demo/shell-subsystem/demo-panel-view-model.md");
            verificationFiles = ("docs/verification/speech-demo/shell-subsystem/demo-panel-view-model.md");
            requirementFiles = ("docs/reqstream/speech-demo/shell-subsystem/demo-panel-view-model.yaml");
        }

        // Uses CommunityToolkit MVVM to pair a panel title with its view model for navigation.
        dependency DemoPanelViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
