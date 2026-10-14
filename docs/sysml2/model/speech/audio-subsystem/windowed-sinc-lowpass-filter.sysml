package Speech {
    part def WindowedSincLowpassFilter {
        doc /* Shared, direction-agnostic windowed-sinc Hamming lowpass FIR primitive used by both
             * resamplers' downsampling anti-aliasing stage. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/WindowedSincLowpassFilter.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem/windowed-sinc-lowpass-filter.md */
        comment verificationRef /* Verification: docs/verification/speech/audio-subsystem/windowed-sinc-lowpass-filter.md */
        comment reqRef /* Requirements: docs/reqstream/speech/audio-subsystem/windowed-sinc-lowpass-filter.yaml */
    }
}
