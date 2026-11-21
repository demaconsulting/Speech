package Speech_UnavailableAudioCaptureDeviceProbe {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceDescription::*;
    private import Speech_IAudioCaptureDeviceProbe::*;

    part def UnavailableAudioCaptureDeviceProbe {
        doc /* Honest "unavailable" IAudioCaptureDeviceProbe fallback: always enumerates zero devices rather than
             * throwing. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/UnavailableAudioCaptureDeviceProbe.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/UnavailableAudioDeviceTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/unavailable-audio-capture-device-probe.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/unavailable-audio-capture-device-probe.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/unavailable-audio-capture-device-probe.yaml");
        }

        // Implements the capture probe contract by returning an empty device list.
        dependency UnavailableAudioCaptureDeviceProbe to AudioDeviceDescription;
        dependency UnavailableAudioCaptureDeviceProbe to IAudioCaptureDeviceProbe;
    }
}
