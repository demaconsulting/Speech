package SpeechOnnxKokoro_SpeechModelCatalogKokoroExtensions {
    private import SoftwareItemMetadata::*;

    part def SpeechModelCatalogKokoroExtensions {
        doc /* Public AddKokoroModels() extension method that registers the one ONNX-Runtime-backed Kokoro model with
             * a SpeechModelCatalog and returns the same catalog for chaining. Documented as a subsystem unit (no
             * separate unit-level companion files), mirroring SpeechSherpa's SpeechModelCatalogSherpaExtensions
             * convention. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.Kokoro/SpeechModelCatalogKokoroExtensions.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.Kokoro.Tests/SpeechModelCatalogKokoroExtensionsTests.cs");
            designFiles = ("docs/design/speech-onnx-kokoro/model-management-subsystem.md");
            verificationFiles = ("docs/verification/speech-onnx-kokoro/model-management-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-onnx-kokoro/model-management-subsystem.yaml");
        }

        // Registers the Kokoro model in the speech model catalog.
        dependency SpeechModelCatalogKokoroExtensions to Speech_SpeechModelCatalog::SpeechModelCatalog;
    }
}
