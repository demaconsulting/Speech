package SpeechDemo_AudioDeviceService {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_IAudioDeviceService::*;

    part def AudioDeviceService {
        doc /* Real enumeration seam implementation, delegating to the library's AudioDeviceFactory capture and
             * playback probes. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/DeviceSelectionSubsystem/AudioDeviceService.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/DeviceSelectionSubsystem/AudioDeviceServiceTests.cs");
            designFiles = ("docs/design/speech-demo/device-selection-subsystem/audio-device-service.md");
            verificationFiles = ("docs/verification/speech-demo/device-selection-subsystem/audio-device-service.md");
            requirementFiles = ("docs/reqstream/speech-demo/device-selection-subsystem/audio-device-service.yaml");
        }

        // Implements the device seam by delegating listing and device creation to AudioDeviceFactory.
        dependency AudioDeviceService to IAudioDeviceService;
        dependency AudioDeviceService to Speech_AudioDeviceDescription::AudioDeviceDescription;
        dependency AudioDeviceService to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency AudioDeviceService to Speech_AudioDeviceSelection::AudioDeviceSelection;
        dependency AudioDeviceService to Speech_IAudioCaptureDevice::IAudioCaptureDevice;
        dependency AudioDeviceService to Speech_IAudioPlaybackDevice::IAudioPlaybackDevice;
    }
}
