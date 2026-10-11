package SpeechOnnxChatterboxTurbo {
    part def OnnxChatterboxTurboSynthesisEngine {
        doc /* The ISynthesisBackend implementation: prompt assembly, bounded autoregressive
             * generation, voice caching and decoding, over an IChatterboxTurboNetworks seam. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/OnnxChatterboxTurboSynthesisEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SynthesisSubsystem/OnnxChatterboxTurboSynthesisEngineTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-chatterboxturbo/synthesis-subsystem/onnx-chatterboxturbo-synthesis-engine.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-chatterboxturbo/synthesis-subsystem/onnx-chatterboxturbo-synthesis-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-chatterboxturbo/synthesis-subsystem/onnx-chatterboxturbo-synthesis-engine.yaml */
    }
}
