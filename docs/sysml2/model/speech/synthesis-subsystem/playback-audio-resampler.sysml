package Speech {
    part def PlaybackAudioResampler {
        doc /* Pure, immutable converter that resamples single-channel synthesized audio from
             * the synthesis engine's sample rate to the playback device's sample rate by linear
             * interpolation, then upmixes it across the playback device's required channel
             * count, mirroring AudioFrameResampler's capture-direction conversion. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/PlaybackAudioResampler.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/PlaybackAudioResamplerTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.yaml */
    }
}
