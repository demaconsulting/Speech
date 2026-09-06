package Speech {
    part def AudioFrameResampler {
        doc /* Pure, immutable converter that collapses interleaved multi-channel capture audio to
             * mono by channel averaging and converts its sample rate by linear interpolation, so
             * audio captured at any device format can be fed to a model at its declared rate. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/RecognitionSubsystem/AudioFrameResampler.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/RecognitionSubsystem/AudioFrameResamplerTests.cs */
        comment designRef /* Design: docs/design/speech/recognition-subsystem/sherpa-onnx-speech-recognizer.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem/sherpa-onnx-speech-recognizer.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/recognition-subsystem/sherpa-onnx-speech-recognizer.yaml */
    }
}
