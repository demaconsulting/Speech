package SpeechCli_DoctorCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechCli_CliModelCatalogFactory::*;
    private import SpeechCli_Context::*;
    private import SpeechCli_ICliModelCatalog::*;
    private import SpeechCli_PathHelpers::*;

    part def DoctorCommand {
        doc /* Implements doctor: reports native runtime resolvability, model store writability/disk space, audio
             * device counts, and installed-versus-known model counts as a pass/fail checklist, treating only model
             * store writability as a hard failure. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Cli/Commands/DeviceCommandsSubsystem/DoctorCommand.cs");
            testFiles = ("test/DemaConsulting.Speech.Cli.Tests/Commands/DeviceCommandsSubsystem/DoctorCommandTests.cs");
            designFiles = ("docs/design/speech-cli/device-commands-subsystem/doctor-command.md");
            verificationFiles = ("docs/verification/speech-cli/device-commands-subsystem/doctor-command.md");
            requirementFiles = ("docs/reqstream/speech-cli/device-commands-subsystem/doctor-command.yaml");
        }

        // Checks the model store, audio probes (with fallbacks if unavailable) and the catalog to report
        // health.
        dependency DoctorCommand to CliModelCatalogFactory;
        dependency DoctorCommand to Context;
        dependency DoctorCommand to ICliModelCatalog;
        dependency DoctorCommand to PathHelpers;
        dependency DoctorCommand to Speech_AudioDeviceFactory::AudioDeviceFactory;
        dependency DoctorCommand to Speech_IAudioCaptureDeviceProbe::IAudioCaptureDeviceProbe;
        dependency DoctorCommand to Speech_IAudioPlaybackDeviceProbe::IAudioPlaybackDeviceProbe;
        dependency DoctorCommand to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency DoctorCommand to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency DoctorCommand to Speech_SpeechModelStore::SpeechModelStore;
        dependency DoctorCommand to Speech_SpeechModelStoreOptions::SpeechModelStoreOptions;
        dependency DoctorCommand to Speech_UnavailableAudioCaptureDeviceProbe::UnavailableAudioCaptureDeviceProbe;
        dependency DoctorCommand to Speech_UnavailableAudioPlaybackDeviceProbe::UnavailableAudioPlaybackDeviceProbe;
    }
}
