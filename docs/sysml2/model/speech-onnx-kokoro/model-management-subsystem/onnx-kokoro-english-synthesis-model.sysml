package SpeechOnnxKokoro {
    part def OnnxKokoroEnglishSynthesisModel {
        doc /* Real, concrete ISynthesisModel backing the bare ONNX export
             * onnx-community/Kokoro-82M-v1.0-ONNX (Apache-2.0), run directly through ONNX
             * Runtime with no bundled native inference engine. Declares 29 American/British
             * English voices as a ChoiceParameter, owns a 30-file download descriptor (the ONNX
             * graph plus every voice's style-vector file, none requiring extraction), builds its
             * ONNX Runtime session through the sibling SpeechOnnx system's
             * OnnxExecutionProviderSelector (supplying OnnxKokoroSynthesisEngine.RunProbeInference
             * as the session-validation probe for each accelerated candidate), and owns a
             * confirmed voice-to-speaker-id mapping via ISynthesisModel.ResolveSpeakerId. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Kokoro/ModelManagementSubsystem/OnnxKokoroEnglishSynthesisModel.cs */
        comment designRef /* Design: docs/design/speech-onnx-kokoro/model-management-subsystem/onnx-kokoro-english-synthesis-model.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/model-management-subsystem/onnx-kokoro-english-synthesis-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/model-management-subsystem/onnx-kokoro-english-synthesis-model.yaml */
    }
}
