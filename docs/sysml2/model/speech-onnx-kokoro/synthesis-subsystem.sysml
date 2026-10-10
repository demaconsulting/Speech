package SpeechOnnxKokoro {
    part def SynthesisSubsystem {
        doc /* The real ONNX-Runtime-backed implementation of Speech's public ISynthesisBackend
             * seam over Kokoro v1.0's bare ONNX graph, owning phonemization entirely in managed
             * code. */

        comment designRef /* Design: docs/design/speech-onnx-kokoro/synthesis-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-kokoro/synthesis-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-kokoro/synthesis-subsystem.yaml */

        part onnxKokoroSynthesisEngine : OnnxKokoroSynthesisEngine;
    }
}
