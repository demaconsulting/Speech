package SpeechDemo_IAudioDeviceService {
    private import SoftwareItemMetadata::*;

    part def IAudioDeviceService {
        doc /* Demo-owned seam for enumerating capture and playback devices, so the pickers can be tested without
             * audio hardware. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/DeviceSelectionSubsystem/IAudioDeviceService.cs");
            designFiles = ("docs/design/speech-demo/device-selection-subsystem/audio-device-service.md");
            verificationFiles = ("docs/verification/speech-demo/device-selection-subsystem/audio-device-service.md");
            requirementFiles = ("docs/reqstream/speech-demo/device-selection-subsystem/audio-device-service.yaml");
        }

        // Defines device listing and creation in terms of library descriptions, selections and devices.
        dependency IAudioDeviceService to Speech_AudioDeviceDescription::AudioDeviceDescription;
        dependency IAudioDeviceService to Speech_AudioDeviceSelection::AudioDeviceSelection;
        dependency IAudioDeviceService to Speech_IAudioCaptureDevice::IAudioCaptureDevice;
        dependency IAudioDeviceService to Speech_IAudioPlaybackDevice::IAudioPlaybackDevice;
    }
}
