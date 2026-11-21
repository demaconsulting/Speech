package SpeechSherpa_SpeechModelCatalogSherpaExtensions {
    private import SoftwareItemMetadata::*;

    part def SpeechModelCatalogSherpaExtensions {
        doc /* Public AddSherpaModels() extension method that registers the four sherpa-onnx models with a
             * SpeechModelCatalog and returns the same catalog for chaining. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Sherpa/SpeechModelCatalogSherpaExtensions.cs");
            testFiles = ("test/DemaConsulting.Speech.Sherpa.Tests/ModelManagementSubsystem/SpeechModelCatalogSherpaExtensionsTests.cs");
            designFiles = ("docs/design/speech-sherpa/model-management-subsystem.md");
            verificationFiles = ("docs/verification/speech-sherpa/model-management-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-sherpa/model-management-subsystem.yaml");
        }

        // Registers the four sherpa-onnx models in the speech model catalog.
        dependency SpeechModelCatalogSherpaExtensions to Speech_SpeechModelCatalog::SpeechModelCatalog;
    }
}
