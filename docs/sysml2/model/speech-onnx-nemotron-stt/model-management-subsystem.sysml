package SpeechOnnxNemotronStt {
    part def ModelManagementSubsystem {
        doc /* The AddNemotronSttModels() catalog registration entry point, the one concrete
             * ONNX-Runtime-backed OnnxNemotronMultilingualRecognitionModel implementing Speech's
             * IRecognitionModel contract, and the NemotronVocabulary unit that derives the
             * language id and blank id and detokenizes emitted token ids. */

        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt/model-management-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt/model-management-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt/model-management-subsystem.yaml */

        part SpeechModelCatalogNemotronSttExtensions : SpeechModelCatalogNemotronSttExtensions;
        part onnxNemotronMultilingualRecognitionModel : OnnxNemotronMultilingualRecognitionModel;
        part nemotronVocabulary : NemotronVocabulary;
    }

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
