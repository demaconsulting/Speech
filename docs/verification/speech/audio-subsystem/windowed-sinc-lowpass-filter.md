### WindowedSincLowpassFilter

#### Verification Approach

`WindowedSincLowpassFilter` has no dedicated test class. It is a stateless internal DSP primitive
verified indirectly, with real (unmocked) filter code, through the anti-aliasing tests of the two
resamplers that consume it: `AudioFrameResamplerTests.cs` (capture direction) and
`PlaybackAudioResamplerTests.cs` (playback direction). Kernel-construction argument validation is
exercised only through those callers; see Acceptance Criteria for the stated boundary.

#### Test Environment

No additional setup beyond the standard test runner; pure in-memory `float` arrays.

#### Acceptance Criteria

Verification passes when a tone above the target Nyquist frequency is measurably attenuated by
both resamplers, and a short input during downsampling does not throw. Direct unit tests of
`BuildLowpassKernel` argument validation (even tap count, out-of-range cutoff) are not present;
those guards are defensive and are reachable only from fixed internal call sites.

#### Test Scenarios

##### Capture Direction: Above-Nyquist Tone Is Attenuated

**Test**: `AudioFrameResampler_Resample_AboveTargetNyquistTone_IsAttenuated`
(`test/DemaConsulting.Speech.Tests/RecognitionSubsystem/AudioFrameResamplerTests.cs`)

Stimulus: a sine tone above the target Nyquist frequency is downsampled. Expected: the output
amplitude is much lower than an unfiltered decimation would produce.

##### Playback Direction: Above-Nyquist Tone Is Attenuated

**Test**: `PlaybackAudioResampler_Resample_AboveTargetNyquistTone_IsAttenuated`
(`test/DemaConsulting.Speech.Tests/SynthesisSubsystem/PlaybackAudioResamplerTests.cs`)

Stimulus: the same above-Nyquist tone is downsampled by the playback resampler. Expected: the
same attenuation, demonstrating the shared filter behaves identically in both directions.

##### Short Input During Downsampling Does Not Throw

**Tests**: `AudioFrameResampler_Resample_ShortInputDuringDownsampling_DoesNotThrow`,
`PlaybackAudioResampler_Resample_ShortInputDuringDownsampling_DoesNotThrow`

Stimulus: an input shorter than the filter kernel. Expected: edge replication keeps the filter
well-defined and no exception is thrown.
