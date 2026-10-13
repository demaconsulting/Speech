### PlaybackAudioResampler

#### Verification Approach

`PlaybackAudioResampler` is verified as a pure function over plain float arrays using a
tolerance-based comparer, so its output can be asserted directly rather than observed through the
session.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: None
- **Test doubles**: None

#### Acceptance Criteria

The unit is considered verified when equal rates copy the input unchanged, upsampling and
downsampling produce the documented sample counts, an above-target-Nyquist tone is attenuated on
downsampling, short and empty inputs are handled, upmix replicates each frame across all channels
(and rejects a non-positive channel count), and `Convert` composes resample then upmix, skipping
the upmix for a single target channel.

#### Test Scenarios

All tests are in `test/DemaConsulting.Speech.Tests/SynthesisSubsystem/PlaybackAudioResamplerTests.cs`.

##### Rate Conversion

**Stimulus**: Mono audio resampled at equal, higher and lower target rates; a tone above the
target Nyquist; very short and empty input.

**Expected**: Identity copy; proportional sample counts; the tone is attenuated; no exception for
short input; empty in, empty out.

**Tests**: `PlaybackAudioResampler_Resample_EqualRates_CopiesUnchanged`,
`PlaybackAudioResampler_Resample_Upsample_ProducesInterpolatedSamples`,
`PlaybackAudioResampler_Resample_Downsample_ReducesSampleCount`,
`PlaybackAudioResampler_Resample_AboveTargetNyquistTone_IsAttenuated`,
`PlaybackAudioResampler_Resample_ShortInputDuringDownsampling_DoesNotThrow`,
`PlaybackAudioResampler_Resample_EmptyInput_ReturnsEmpty`

##### Upmix

**Stimulus**: Mono input upmixed to one, several channels, from empty input, and to a
non-positive channel count.

**Expected**: Single channel unchanged; each frame replicated across channels; empty in, empty out;
`ArgumentOutOfRangeException` for the invalid count.

**Tests**: `PlaybackAudioResampler_UpmixToChannels_SingleChannel_CopiesUnchanged`,
`PlaybackAudioResampler_UpmixToChannels_MultipleChannels_ReplicatesEachFrame`,
`PlaybackAudioResampler_UpmixToChannels_EmptyInput_ReturnsEmpty`,
`PlaybackAudioResampler_UpmixToChannels_NonPositiveChannelCount_ThrowsArgumentOutOfRangeException`

##### Composition

**Stimulus**: `Convert` with a different target rate and channel count, and with a single target
channel.

**Expected**: Resample then upmix; for one channel, the resampled result without an upmix copy.

**Tests**: `PlaybackAudioResampler_Convert_DifferentRateAndChannels_ResamplesThenUpmixes`,
`PlaybackAudioResampler_Convert_SingleChannelTarget_ReturnsResampledWithoutUpmix`

##### Argument Validation

**Stimulus**: `Resample` with a non-positive source or target rate; the constructor with a
non-positive argument.

**Expected**: `ArgumentOutOfRangeException` in each case.

**Tests**: `PlaybackAudioResampler_Resample_NonPositiveRate_ThrowsArgumentOutOfRangeException`,
`PlaybackAudioResampler_Constructor_NonPositiveArgument_ThrowsArgumentOutOfRangeException`
