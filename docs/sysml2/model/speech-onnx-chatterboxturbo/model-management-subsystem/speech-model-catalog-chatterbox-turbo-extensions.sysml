package SpeechOnnxChatterboxTurbo_SpeechModelCatalogChatterboxTurboExtensions {
    private import SoftwareItemMetadata::*;

    part def SpeechModelCatalogChatterboxTurboExtensions {
        doc /* Public AddChatterboxTurboModels() extension method that registers the one ONNX-Runtime-backed
             * Chatterbox Turbo model with a SpeechModelCatalog and returns the same catalog for chaining. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SpeechModelCatalogChatterboxTurboExtensions.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/ModelManagementSubsystem/SpeechModelCatalogChatterboxTurboExtensionsTests.cs");
            designFiles = ("docs/design/speech-onnx-chatterboxturbo/model-management-subsystem/speech-model-catalog-chatterbox-turbo-extensions.md");
            verificationFiles = ("docs/verification/speech-onnx-chatterboxturbo/model-management-subsystem/speech-model-catalog-chatterbox-turbo-extensions.md");
            requirementFiles = ("docs/reqstream/speech-onnx-chatterboxturbo/model-management-subsystem/speech-model-catalog-chatterbox-turbo-extensions.yaml");
        }

        // Registers the Chatterbox Turbo model, with its reference voice library, in the speech model
        // catalog.
        dependency SpeechModelCatalogChatterboxTurboExtensions to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
        dependency SpeechModelCatalogChatterboxTurboExtensions to Speech_SpeechModelCatalog::SpeechModelCatalog;
    }
}
