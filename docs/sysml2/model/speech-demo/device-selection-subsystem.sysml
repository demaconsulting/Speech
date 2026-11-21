package SpeechDemo_DeviceSelectionSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_AudioDeviceService::*;
    private import SpeechDemo_DeviceSelectionViewModel::*;
    private import SpeechDemo_IAudioDeviceService::*;

    part def DeviceSelectionSubsystem {
        doc /* The capture/playback device pickers: a demo-owned enumeration seam over the library's audio device
             * factory and probes, and the presentation state that lists devices, tracks the chosen device by name,
             * and explains an empty machine honestly. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/DeviceSelectionSubsystem/DeviceSelectionSubsystemTests.cs");
            designFiles = ("docs/design/speech-demo/device-selection-subsystem.md");
            verificationFiles = ("docs/verification/speech-demo/device-selection-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-demo/device-selection-subsystem.yaml");
        }

        part iAudioDeviceService : IAudioDeviceService;
        part audioDeviceService : AudioDeviceService;
        part deviceSelectionViewModel : DeviceSelectionViewModel;
    }
}
