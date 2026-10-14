package SpeechOnnxNemotronStt {
    part def ModelManagementSubsystem {
        doc /* The AddNemotronSttModels() catalog registration entry point, the one concrete
             * ONNX-Runtime-backed OnnxNemotronMultilingualRecognitionModel implementing Speech's
             * IRecognitionModel contract, and the NemotronVocabulary unit that derives the
             * language id and blank id and detokenizes emitted token ids. */

        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt/model-management-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt/model-management-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt/model-management-subsystem.yaml */

        part speechModelCatalogNemotronSttExtensions : SpeechModelCatalogNemotronSttExtensions;
        part onnxNemotronMultilingualRecognitionModel : OnnxNemotronMultilingualRecognitionModel;
        part nemotronVocabulary : NemotronVocabulary;
    }
}
