package SpeechOnnxNemotronStt {
    doc
    /*
     * SpeechOnnxNemotronStt is the DemaConsulting.Speech.Onnx.NemotronStt library that supplies a
     * concrete, ONNX-Runtime-backed NVIDIA Nemotron 3.5 ASR streaming speech-recognition model
     * and inference engine for the Speech library through its IRecognitionModel and
     * IRecognitionBackend extension seams. Speech must never depend on it. It builds on the
     * SpeechOnnx execution-provider selector and runs the model's encoder, decoder, and joint
     * graphs directly through ONNX Runtime, owning the feature extraction, decoding, and
     * leading-silence mitigation itself.
     */

    part def SpeechOnnxNemotronSttSystem {
        doc /* The SpeechOnnxNemotronStt library as a whole. */

        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt.yaml */

        part modelManagementSubsystem : ModelManagementSubsystem;
        part recognitionSubsystem : RecognitionSubsystem;
    }
}
