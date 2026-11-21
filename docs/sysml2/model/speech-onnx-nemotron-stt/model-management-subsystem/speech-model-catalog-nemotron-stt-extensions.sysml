package SpeechOnnxNemotronStt_SpeechModelCatalogNemotronSttExtensions {
    private import SoftwareItemMetadata::*;

    part def SpeechModelCatalogNemotronSttExtensions {
        doc /* Public AddNemotronSttModels() extension method that registers the one ONNX-Runtime-backed Nemotron
             * model with a SpeechModelCatalog and returns the same catalog for chaining. Documented as a subsystem
             * unit (no separate unit-level companion files), mirroring SpeechOnnxKokoro's
             * SpeechModelCatalogKokoroExtensions convention. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.NemotronStt/SpeechModelCatalogNemotronSttExtensions.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/SpeechModelCatalogNemotronSttExtensionsTests.cs");
            designFiles = ("docs/design/speech-onnx-nemotron-stt/model-management-subsystem.md");
            verificationFiles = ("docs/verification/speech-onnx-nemotron-stt/model-management-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-onnx-nemotron-stt/model-management-subsystem.yaml");
        }

        // Registers the Nemotron recognition model in the speech model catalog.
        dependency SpeechModelCatalogNemotronSttExtensions to Speech_SpeechModelCatalog::SpeechModelCatalog;
    }
}
