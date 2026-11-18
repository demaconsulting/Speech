package SpeechOnnxPiper {
    part def SynthesisSubsystem {
        doc /* The real ONNX-Runtime-backed implementation of Speech's public ISynthesisBackend and
             * IAnnotatedSynthesisBackend seams over the Piper voice's ONNX graph: the phoneme pipeline that
             * turns text and the annotations of the English text analysis into framed phoneme ids
             * (PiperPhonemePipeline) and the engine that runs the graph over them (OnnxPiperSynthesisEngine). */

        comment designRef /* Design: docs/design/speech-onnx-piper/synthesis-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-piper/synthesis-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-piper/synthesis-subsystem.yaml */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.Piper.Tests/SynthesisSubsystem/SynthesisSubsystemTests.cs */

        part onnxPiperSynthesisEngine : OnnxPiperSynthesisEngine;
        part piperPhonemePipeline : PiperPhonemePipeline;
    }
}
