package SpeechDemo_DeviceSelectionViewModel {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_IAudioDeviceService::*;

    part def DeviceSelectionViewModel {
        doc /* Presentation state for both device pickers: the listed devices, the chosen device in each direction
             * expressed as the library's name-based selection, the refresh command, and the honest empty-machine
             * status text. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/DeviceSelectionSubsystem/DeviceSelectionViewModel.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/DeviceSelectionSubsystem/DeviceSelectionViewModelTests.cs");
            designFiles = ("docs/design/speech-demo/device-selection-subsystem/device-selection-view-model.md");
            verificationFiles = ("docs/verification/speech-demo/device-selection-subsystem/device-selection-view-model.md");
            requirementFiles = ("docs/reqstream/speech-demo/device-selection-subsystem/device-selection-view-model.yaml");
        }

        // Lists devices from the device service, maps picks to AudioDeviceSelection and shows
        // device-in-use errors.
        dependency DeviceSelectionViewModel to IAudioDeviceService;
        dependency DeviceSelectionViewModel to Speech_AudioDeviceDescription::AudioDeviceDescription;
        dependency DeviceSelectionViewModel to Speech_AudioDeviceInUseException::AudioDeviceInUseException;
        dependency DeviceSelectionViewModel to Speech_AudioDeviceSelection::AudioDeviceSelection;
        dependency DeviceSelectionViewModel to OtsDependencies::CommunityToolkitMvvm;
    }
}
