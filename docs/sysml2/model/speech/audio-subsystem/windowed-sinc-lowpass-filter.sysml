package Speech_WindowedSincLowpassFilter {
    private import SoftwareItemMetadata::*;

    part def WindowedSincLowpassFilter {
        doc /* Shared, direction-agnostic windowed-sinc Hamming lowpass FIR primitive used by both resamplers'
             * downsampling anti-aliasing stage. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/AudioSubsystem/WindowedSincLowpassFilter.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/AudioSubsystem/WindowedSincLowpassFilterTests.cs");
            designFiles = ("docs/design/speech/audio-subsystem/windowed-sinc-lowpass-filter.md");
            verificationFiles = ("docs/verification/speech/audio-subsystem/windowed-sinc-lowpass-filter.md");
            requirementFiles = ("docs/reqstream/speech/audio-subsystem/windowed-sinc-lowpass-filter.yaml");
        }

        // Uses tensor primitives to run its vectorized FIR filtering math.
        dependency WindowedSincLowpassFilter to OtsDependencies::SystemNumericsTensors;
    }
}
