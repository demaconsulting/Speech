package SpeechOnnxNemotronStt {
    part def SpeechModelCatalogNemotronSttExtensions {
        doc /* Public AddNemotronSttModels() extension method that registers the one
             * ONNX-Runtime-backed Nemotron model with a SpeechModelCatalog and returns the same
             * catalog for chaining. Documented as a subsystem unit (no separate unit-level
             * companion files), mirroring SpeechOnnxKokoro's SpeechModelCatalogKokoroExtensions
             * convention. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.NemotronStt/SpeechModelCatalogNemotronSttExtensions.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/SpeechModelCatalogNemotronSttExtensionsTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt/model-management-subsystem.md (documented as a subsystem unit) */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt/model-management-subsystem.md (documented as a subsystem unit) */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt/model-management-subsystem.yaml (consolidated at subsystem level) */
    }
}
