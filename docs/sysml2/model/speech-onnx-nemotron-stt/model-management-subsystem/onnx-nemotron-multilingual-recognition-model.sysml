package SpeechOnnxNemotronStt {
    part def OnnxNemotronMultilingualRecognitionModel {
        doc /* Real IRecognitionModel for NVIDIA Nemotron 3.5 ASR streaming 0.6B (int4 ONNX
             * export). One catalog id with a language ChoiceParameter (en-US, en-GB); declares
             * the eleven-file checksum-verified download descriptor; CreateBackend builds the
             * encoder session through OnnxExecutionProviderSelector and the decoder and joint
             * CPU sessions, and constructs the OnnxNemotronRecognitionEngine. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.NemotronStt/ModelManagementSubsystem/OnnxNemotronMultilingualRecognitionModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/ModelManagementSubsystem/OnnxNemotronMultilingualRecognitionModelTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt/model-management-subsystem/onnx-nemotron-multilingual-recognition-model.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt/model-management-subsystem/onnx-nemotron-multilingual-recognition-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt/model-management-subsystem/onnx-nemotron-multilingual-recognition-model.yaml */
    }
}
