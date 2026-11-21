package SpeechOnnxPiper_SpeechModelCatalogPiperExtensions {
    private import SoftwareItemMetadata::*;

    part def SpeechModelCatalogPiperExtensions {
        doc /* Public AddPiperModels() extension method that registers the one ONNX-Runtime-backed Piper model with a
             * SpeechModelCatalog (optionally with preferred execution provider names) and returns the same catalog
             * for chaining. Documented as a subsystem unit (no separate unit-level companion files), following the
             * convention of SpeechSherpa's SpeechModelCatalogSherpaExtensions and SpeechOnnxKokoro's
             * SpeechModelCatalogKokoroExtensions. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.Piper/SpeechModelCatalogPiperExtensions.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.Piper.Tests/SpeechModelCatalogPiperExtensionsTests.cs");
            designFiles = ("docs/design/speech-onnx-piper/model-management-subsystem.md");
            verificationFiles = ("docs/verification/speech-onnx-piper/model-management-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-onnx-piper/model-management-subsystem.yaml");
        }

        // Registers the Piper model in the speech model catalog.
        dependency SpeechModelCatalogPiperExtensions to Speech_SpeechModelCatalog::SpeechModelCatalog;
    }
}
