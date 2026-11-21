package SpeechDemo_ShellSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_DemoPanelViewModel::*;
    private import SpeechDemo_MainWindowViewModel::*;

    part def ShellSubsystem {
        doc /* The application's manual composition root, window shell, and capability navigation: it builds the
             * library entry points and demo service adapters, injects them into the panel view models, and presents
             * one titled panel per demonstrated capability. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/speech-demo/shell-subsystem.md");
            verificationFiles = ("docs/verification/speech-demo/shell-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-demo/shell-subsystem.yaml");
        }

        part mainWindowViewModel : MainWindowViewModel;
        part demoPanelViewModel : DemoPanelViewModel;
    }
}
