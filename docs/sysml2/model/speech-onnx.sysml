package SpeechOnnx {
    doc
    /*
     * SpeechOnnx is the DemaConsulting.Speech.Onnx library that supplies generic ONNX Runtime
     * infrastructure - today, execution-provider probing and selection - reusable by any
     * ONNX-Runtime-backed model package built on top of it (for example the sibling
     * DemaConsulting.Speech.Onnx.Kokoro package). It declares zero models itself.
     */

    part def SpeechOnnxSystem {
        doc /* The SpeechOnnx library as a whole. */

        comment designRef /* Design: docs/design/speech-onnx.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx.yaml */

        part onnxRuntimeSubsystem : OnnxRuntimeSubsystem;
    }
}
