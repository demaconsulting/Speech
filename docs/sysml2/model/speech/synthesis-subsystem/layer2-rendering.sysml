package Speech {
    part def Layer2Rendering {
        doc /* IModelCapabilityProfile and its default DefaultModelCapabilityProfile
             * implementation: decide, per model and per TaggedTextSpan, whether an audio tag is
             * realized as a model-native control token, approximated as a timed silence or
             * numeric parameter override, or silently stripped, producing an ordered SpeechPlan
             * of SpeechSegments. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/IModelCapabilityProfile.cs, DefaultModelCapabilityProfile.cs, SpeechSegment.cs, SpeechPlan.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/DefaultModelCapabilityProfileTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.md (documented with the pipeline it serves) */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.md (documented with the pipeline it serves) */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/sherpa-onnx-speech-synthesizer.yaml */
    }
}
