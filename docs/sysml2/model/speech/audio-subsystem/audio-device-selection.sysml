package Speech_AudioDeviceSelection {
    private import SoftwareItemMetadata::*;
    private import Speech_AudioDeviceDescription::*;
    private import ScalarValues::*;

    part def AudioDeviceSelection {
        doc /* Immutable, persistable reference to a chosen audio device identified by name alone, with never-throwing
             * resolution against a probe's current enumeration. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/AudioDeviceSelection.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/AudioDeviceSelectionTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/audio-device-selection.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/audio-device-selection.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/audio-device-selection.yaml");
        }

        attribute deviceName : String [0..1];

        // Resolves the saved device name against enumerated device descriptions.
        dependency AudioDeviceSelection to AudioDeviceDescription;
    }
}
