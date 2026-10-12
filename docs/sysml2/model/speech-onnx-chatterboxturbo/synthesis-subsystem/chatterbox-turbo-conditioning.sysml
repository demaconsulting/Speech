package SpeechOnnxChatterboxTurbo {
    part def ChatterboxTurboConditioning {
        doc /* Supporting record holding the reference-voice conditioning embeddings produced once per
             * (voice, mood) by the speech encoder and reused for every utterance spoken with that clip. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.ChatterboxTurbo/SynthesisSubsystem/ChatterboxTurboConditioning.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SynthesisSubsystem/OnnxChatterboxTurboSynthesisEngineTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-chatterboxturbo/synthesis-subsystem/onnx-chatterboxturbo-networks.md (documented as a supporting type) */
    }
}
