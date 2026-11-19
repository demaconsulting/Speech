package SpeechOnnxPiper {
    part def OnnxPiperSynthesisEngine {
        doc /* Production IAnnotatedSynthesisBackend that runs the Piper voice's ONNX graph directly through
             * ONNX Runtime (inputs input, input_lengths, scales and sid) over the phoneme ids of the
             * PiperPhonemePipeline: the voice's own noise scales, the length scale divided by the requested
             * speed, the speaker clamped into the voice's range, one run per utterance with the audio joined,
             * empty audio for a text with nothing to say, the count and list of symbols the voice's table
             * lacks, the probe inference for execution-provider selection, and ownership of the session. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Piper/SynthesisSubsystem/OnnxPiperSynthesisEngine.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.Piper.Tests/SynthesisSubsystem/OnnxPiperSynthesisEngineTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-piper/synthesis-subsystem/onnx-piper-synthesis-engine.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-piper/synthesis-subsystem/onnx-piper-synthesis-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-piper/synthesis-subsystem/onnx-piper-synthesis-engine.yaml */
    }
}
