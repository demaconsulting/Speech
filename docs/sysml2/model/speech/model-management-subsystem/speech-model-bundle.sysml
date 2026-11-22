package Speech_SpeechModelBundle {
    private import ScalarValues::*;
    private import SoftwareItemMetadata::*;
    private import Speech_SpeechModelDownloadDescriptor::*;
    private import Speech_SpeechModelDownloadFile::*;
    private import Speech_ZipEntryPathValidator::*;

    part def SpeechModelBundle {
        doc /* Public declaration of a model published as one or more SHA-256-pinned zip bundles on the project's
             * models release: the bundle URI and zip pin, its naming-rule file name and name, the compiled per-file
             * pins of the unmodified vendor files it carries (Contents), and the static CreateDescriptor and
             * InstallAsync members a bundled model uses as its download descriptor and install hook. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/ModelManagementSubsystem/SpeechModelBundle.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelBundleTests.cs");
            designFiles = ("docs/design/speech/model-management-subsystem/speech-model-bundle.md");
            verificationFiles = ("docs/verification/speech/model-management-subsystem/speech-model-bundle.md");
            requirementFiles = ("docs/reqstream/speech/model-management-subsystem/speech-model-bundle.yaml");
        }

        attribute uri : String;
        attribute sha256Checksum : String;
        attribute fileName : String;
        attribute name : String;

        // Declares the zip as a pinned download file, carries the vendor pins as a descriptor, and checks vendor
        // paths for portability. The installer engine is referenced by qualified name to avoid an import cycle.
        dependency SpeechModelBundle to SpeechModelDownloadDescriptor;
        dependency SpeechModelBundle to SpeechModelDownloadFile;
        dependency SpeechModelBundle to ZipEntryPathValidator;
        dependency SpeechModelBundle to Speech_SpeechModelBundleInstaller::SpeechModelBundleInstaller;
    }
}
