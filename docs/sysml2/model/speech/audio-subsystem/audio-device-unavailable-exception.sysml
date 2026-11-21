package Speech_AudioDeviceUnavailableException {
    private import SoftwareItemMetadata::*;

    part def AudioDeviceUnavailableException {
        doc /* Exception thrown when an operational member of an unavailable audio device or probe is invoked despite
             * IsAvailable reporting false. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/AudioDeviceUnavailableException.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/UnavailableAudioDeviceTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/audio-device-unavailable-exception.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/audio-device-unavailable-exception.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/audio-device-unavailable-exception.yaml");
        }
    }
}
