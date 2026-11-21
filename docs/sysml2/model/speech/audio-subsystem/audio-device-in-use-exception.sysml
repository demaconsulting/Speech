package Speech_AudioDeviceInUseException {
    private import SoftwareItemMetadata::*;

    part def AudioDeviceInUseException {
        doc /* Exception thrown when a PortAudio device-table refresh is refused because one or more capture/playback
             * devices created from the environment currently have an open/started stream. The one deliberate
             * exception to this library's "never throws at composition" policy - caller-driven, recoverable misuse. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/AudioDeviceInUseException.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioDeviceInUseExceptionTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/audio-device-in-use-exception.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/audio-device-in-use-exception.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/audio-device-in-use-exception.yaml");
        }
    }
}
