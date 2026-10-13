package SpeechOnnxChatterboxTurbo {
    part def ChatterboxTurboConditioning {
        doc /* Supporting record holding the reference-voice conditioning embeddings produced once per
             * (voice, mood) by the speech encoder and reused for every utterance spoken with that clip. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/ChatterboxTurboConditioning.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SynthesisSubsystem/OnnxChatterboxTurboNetworksTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-chatterboxturbo/synthesis-subsystem/chatterbox-turbo-conditioning.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-chatterboxturbo/synthesis-subsystem/chatterbox-turbo-conditioning.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-chatterboxturbo/synthesis-subsystem/chatterbox-turbo-conditioning.yaml */
    }
}
