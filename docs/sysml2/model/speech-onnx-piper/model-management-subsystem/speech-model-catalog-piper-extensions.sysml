package SpeechOnnxPiper {
    part def SpeechModelCatalogPiperExtensions {
        doc /* Public AddPiperModels() extension method that registers the one ONNX-Runtime-backed Piper
             * model with a SpeechModelCatalog (optionally with preferred execution provider names) and
             * returns the same catalog for chaining. Documented as a subsystem unit (no separate unit-level
             * companion files), following the convention of SpeechSherpa's
             * SpeechModelCatalogSherpaExtensions and SpeechOnnxKokoro's SpeechModelCatalogKokoroExtensions. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Piper/SpeechModelCatalogPiperExtensions.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.Piper.Tests/SpeechModelCatalogPiperExtensionsTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-piper/model-management-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-onnx-piper/model-management-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-piper/model-management-subsystem.yaml (consolidated at subsystem level) */
    }
}
