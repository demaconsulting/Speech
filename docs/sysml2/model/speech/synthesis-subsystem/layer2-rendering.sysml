package Speech {
    part def Layer2Rendering {
        doc /* IModelCapabilityProfile and its default DefaultModelCapabilityProfile
             * implementation: decide, per model and per TaggedTextSpan, whether an audio tag is
             * realized as a model-native control token, approximated as a timed silence or
             * numeric parameter override, or silently stripped, producing an ordered SpeechPlan
             * of SpeechSegments. A plain-text chunk carries a chunk-boundary-triggered pause -
             * distinct from the explicit [pause]/[long-pause] tag-triggered pauses above - when
             * its text ends in a genuine ellipsis, so long dictated narration trails off
             * naturally instead of cutting abruptly into the next chunk. The default profile
             * is public and offers a RenderText chunk-then-render step and public pause
             * durations so a model package can build its own profile on it; it carries the
             * annotations and sentence breaks of each plain-text span onto the segments that
             * contain them, re-based across stripped, native, pause and parameter-mapped tags,
             * and a SpeechSegment holds validated, text-relative annotations. A sentence break
             * carried by a tag span (between consecutive tags) is applied at the buffer position
             * where that tag is processed. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/IModelCapabilityProfile.cs, DefaultModelCapabilityProfile.cs, SpeechSegment.cs, SpeechPlan.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/DefaultModelCapabilityProfileTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/layer2-rendering.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/layer2-rendering.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/layer2-rendering.yaml */
    }
}
