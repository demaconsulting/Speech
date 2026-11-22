### NemotronFeatureExtractor Verification

#### Verification Approach

Verified by deterministic in-memory unit tests: reference-value comparisons against independently
computed NeMo front-end features, a frame-count check, streaming and chunking equivalence checks,
and flush, silence, and reset checks. The extractor has no collaborators, so nothing is mocked.

The reference values (`TestData/nemotron-features-golden.tsv`) cover four deterministic synthetic
signals - a two-tone signal, a 100 Hz to 7000 Hz chirp, digital silence, and a 100-sample tone
shorter than one analysis window - every frame and all 128 bands. NeMo itself was not installed or
run: the values come from an independent implementation of the documented NeMo
`FilterbankFeatures` inference formula written for this repository with PyTorch and librosa in
64-bit floating point (0.97 pre-emphasis; short-time Fourier transform with a 512-point FFT,
160-sample hop, 400-sample symmetric Hann window, and constant zero center padding; power
spectrum; Slaney-normalized mel filterbank of 128 bands from 0 Hz to 8000 Hz; natural log with a
2^-24 guard). The recipe and the signal definitions are recorded in `TestData/NOTICE.md`; only the
resulting values are committed. The reference therefore pins the extractor to the formula as
documented, not to a run of NeMo; each of the three front-end choices (zero padding, symmetric
window, 2^-24 guard) changes the values by far more than the tolerance, so each is guarded.

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`NemotronFeatureExtractorTests.cs`), on every target framework. The reference file is copied
beside the test assembly; no network or audio hardware is used.

#### Acceptance Criteria

Every band of every frame of each reference signal is within 1e-3 of the reference value, with the
same number of frames; N accepted samples yield N / 160 + 1 frames after a flush (1 sample gives 1
frame, 159 give 1, 160 give 2, 1600 give 11); frames become available while streaming without a
flush; chunked input equals one-shot input; no audio produces no frames; digital silence yields
log(2^-24) in every band; and reset reproduces identical features.

#### Test Scenarios

##### Reference signals match the NeMo formula

**Scenario**: each of the signals `two-tone`, `chirp`, `silence`, and `short` is extracted in one
piece and flushed.

**Expected**: the frame count matches and every value is within 1e-3 of the reference.

**Test**: `NemotronFeatureExtractor_Extract_GoldenSignals_MatchNeMoReference`

##### The frame count follows the NeMo layout

**Scenario**: digital silence of 1, 159, 160, and 1600 samples, extracted and flushed.

**Expected**: 1, 1, 2, and 11 frames.

**Test**: `NemotronFeatureExtractor_Flush_InputLength_ProducesNeMoFrameCount`

##### Streaming input emits frames without a flush

**Scenario**: 416 samples are accepted (frame 1 needs samples up to 160 + 256).

**Expected**: frames 0 and 1 are available without a flush.

**Test**: `NemotronFeatureExtractor_Accept_StreamingInput_EmitsFramesWithoutFlush`

##### Chunked equals one-shot

**Scenario**: the two-tone signal in pieces of 1, 97, and 500 samples.

**Test**: `Extract_Chunked_EqualsOneShot`

##### No audio produces no frames

**Test**: `Flush_NoAudio_ProducesNoFrames`

##### Silence is the log guard

**Expected**: every value of 0.1 s of digital silence is log(2^-24).

**Test**: `Extract_Silence_IsLogEpsilon`

##### Reset reproduces features

**Test**: `Reset_AfterUse_ReproducesFeatures`
