package Speech_UnavailableAudioPlaybackDeviceProbe {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceDescription::*;
    private import Speech_IAudioPlaybackDeviceProbe::*;

    part def UnavailableAudioPlaybackDeviceProbe {
        doc /* Honest "unavailable" IAudioPlaybackDeviceProbe fallback: always enumerates zero devices rather than
             * throwing. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/UnavailableAudioPlaybackDeviceProbe.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/UnavailableAudioDeviceTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/unavailable-audio-playback-device-probe.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/unavailable-audio-playback-device-probe.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/unavailable-audio-playback-device-probe.yaml");
        }

        // Implements the playback probe contract by returning an empty device list.
        dependency UnavailableAudioPlaybackDeviceProbe to AudioDeviceDescription;
        dependency UnavailableAudioPlaybackDeviceProbe to IAudioPlaybackDeviceProbe;
    }
}
