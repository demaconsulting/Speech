package Speech {
    part def WindowedSincLowpassFilter {
        doc /* Shared, direction-agnostic windowed-sinc Hamming lowpass FIR primitive used by both
             * resamplers' downsampling anti-aliasing stage. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/AudioSubsystem/WindowedSincLowpassFilter.cs */
        comment designRef /* Design: docs/design/speech/audio-subsystem.md (documented as a supporting DSP primitive) */
        comment verificationRef /* Verification: docs/verification/speech/recognition-subsystem.md and docs/verification/speech/synthesis-subsystem.md (exercised indirectly through both resamplers' anti-aliasing tests; no dedicated audio-subsystem.md entry) */
    }
}
