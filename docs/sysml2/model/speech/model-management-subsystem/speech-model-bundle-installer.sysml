package Speech_SpeechModelBundleInstaller {
    private import SoftwareItemMetadata::*;
    private import Speech_SpeechModelBundle::*;
    private import Speech_SpeechModelDownloadFile::*;
    private import Speech_ZipEntryPathValidator::*;

    part def SpeechModelBundleInstaller {
        doc /* Internal extraction and verification engine behind SpeechModelBundle.InstallAsync: validates every
             * entry name of a staged bundle zip before writing anything, parses model.json (schema version 1),
             * requires the entries to be exactly the declared vendor files plus the license material, copies each
             * vendor file with a size cap while hashing it and checks it against model.json and the compiled pin,
             * keeps the metadata under licenses/{bundle}/, and deletes the zip. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelBundleInstaller.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelBundleInstallerTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-bundle-installer.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-bundle-installer.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-bundle-installer.yaml");
        }

        // Extracts the bundle the declaration describes, guards every entry name, and normalizes the compiled
        // vendor paths for comparison with model.json.
        dependency SpeechModelBundleInstaller to SpeechModelBundle;
        dependency SpeechModelBundleInstaller to SpeechModelDownloadFile;
        dependency SpeechModelBundleInstaller to ZipEntryPathValidator;
    }
}
