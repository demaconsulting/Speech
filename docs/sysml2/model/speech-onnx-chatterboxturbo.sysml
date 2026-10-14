package SpeechOnnxChatterboxTurbo {
    doc
    /*
     * SpeechOnnxChatterboxTurbo is the DemaConsulting.Speech.Onnx.ChatterboxTurbo library that
     * supplies a concrete, ONNX-Runtime-backed Chatterbox Turbo (English, reference-clip voice
     * cloning) text-to-speech model and inference engine for the Speech library through its
     * ISynthesisModel extension seam. Speech must never depend on it. The system owns its own
     * byte-level BPE tokenizer and its audio-tag rendering policy.
     */

    part def SpeechOnnxChatterboxTurboSystem {
        doc /* The SpeechOnnxChatterboxTurbo library as a whole. */

        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests/SpeechOnnxChatterboxTurboTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-chatterboxturbo.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-chatterboxturbo.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-chatterboxturbo.yaml */

        part modelManagementSubsystem : ModelManagementSubsystem;
        part synthesisSubsystem : SynthesisSubsystem;
        part microsoftMlOnnxRuntime : SpeechOnnx::MicrosoftMlOnnxRuntime;
    }
}
