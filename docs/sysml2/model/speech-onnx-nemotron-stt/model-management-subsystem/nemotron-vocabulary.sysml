package SpeechOnnxNemotronStt {
    part def NemotronVocabulary {
        doc /* Loads the model's vocab.txt, derives the encoder language id from the locale
             * marker tokens and the RNN-T blank token id, and converts emitted token ids into
             * normalized text. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.NemotronStt/ModelManagementSubsystem/NemotronVocabulary.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/ModelManagementSubsystem/NemotronVocabularyTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt/model-management-subsystem/nemotron-vocabulary.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt/model-management-subsystem/nemotron-vocabulary.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt/model-management-subsystem/nemotron-vocabulary.yaml */
    }
}
