package Speech {
    part def SherpaOnnxSpeechSynthesizer {
        doc /* Real streaming synthesis pipeline: normalizes and tag-parses input text, Layer 2
             * renders it into an ordered SpeechPlan, chunks and synthesizes each segment ahead
             * of playback, and plays segments in order on the playback device while the next
             * segment synthesizes. Resolves the speaker id passed to each segment's Generate
             * call from the constructor-supplied parameterValues bag via
             * ISynthesisModel.ResolveSpeakerId once per segment, replacing a previously
             * hard-coded speakerId: 0, kept correctly independent of the per-segment
             * ParameterOverrides speed/volume mechanism. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/SherpaOnnxSpeechSynthesizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SherpaOnnxSpeechSynthesizerTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.yaml */
    }
}
