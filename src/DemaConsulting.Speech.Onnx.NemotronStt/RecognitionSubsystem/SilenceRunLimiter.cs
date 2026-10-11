namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     The tunable constants of <see cref="SilenceRunLimiter"/>. The defaults are the documented
///     production values; tests use other values to exercise edge cases.
/// </summary>
/// <param name="FrameSamples">Samples per analysis frame; 320 is 20 ms at 16 kHz.</param>
/// <param name="MaxQuietFrames">
///     The longest run of consecutive quiet frames passed through; later quiet frames of the
///     same run are dropped. 20 frames is 400 ms.
/// </param>
/// <param name="CalibrationFrames">
///     The number of leading frames analyzed to estimate the noise floor; 20 frames is 400 ms.
///     Audio is passed through (subject to the quiet-run cap using
///     <paramref name="DefaultThreshold"/>) while calibrating.
/// </param>
/// <param name="FloorPercentile">
///     The percentile (0..1) of the calibration frames' RMS values taken as the noise floor. A
///     low percentile is robust to speech that begins inside the calibration window.
/// </param>
/// <param name="ThresholdMultiplier">The noise floor is multiplied by this to give the quiet threshold (K, 3 to 4).</param>
/// <param name="MinThreshold">The lowest adaptive threshold, so digital silence cannot make the threshold vanish.</param>
/// <param name="MaxThreshold">
///     The highest adaptive threshold, so loud or speech-filled calibration audio cannot raise
///     the threshold above the level of quiet speech and disable the gate for speech.
/// </param>
/// <param name="DefaultThreshold">The RMS threshold used until calibration completes, and always when <paramref name="Adaptive"/> is <see langword="false"/>.</param>
/// <param name="Adaptive">Whether the threshold adapts to the calibrated noise floor.</param>
internal sealed record SilenceRunLimiterOptions(
    int FrameSamples = 320,
    int MaxQuietFrames = 20,
    int CalibrationFrames = 20,
    double FloorPercentile = 0.15,
    double ThresholdMultiplier = 3.5,
    double MinThreshold = 0.0015,
    double MaxThreshold = 0.006,
    double DefaultThreshold = 0.003,
    bool Adaptive = true);

/// <summary>
///     Streaming, stateful limiter that truncates long runs of quiet audio (silence or steady
///     low-level noise) to a fixed maximum duration.
/// </summary>
/// <remarks>
///     <b>Why this exists</b>: the streaming Nemotron encoder degrades after a long stretch of
///     silence at the start of a stream (the real leading-silence failure that motivated this
///     unit: speech following several seconds of silence was lost or garbled). Capping each quiet
///     run to 400 ms keeps the encoder's cache state in a regime it handles, while the 400 ms
///     that are kept preserve natural pauses between words.
///     <para>
///     <b>Operation</b>: audio is split into 20 ms frames. A frame is <i>quiet</i> when its RMS is
///     below the threshold. A run of consecutive quiet frames is passed through until it reaches
///     400 ms; further quiet frames of the same run are dropped. The first loud frame ends the
///     run and is always passed. A trailing partial frame is carried to the next call and
///     processed when it completes (or on <see cref="Flush"/>), so the result is identical
///     however the input is chunked.
///     </para>
///     <para>
///     <b>Adaptive threshold</b>: a fixed threshold cannot suit both a digitally silent input and
///     a noisy microphone, so the first 400 ms are analyzed: the noise floor is a robust low
///     percentile (15th) of the 20 frame RMS values, and the threshold becomes
///     <c>clamp(floor * 3.5, 0.0015, 0.006)</c>. The minimum keeps exact digital silence from
///     producing a zero threshold; the maximum guarantees that speech (frame RMS typically above
///     0.01) always counts as loud, even when the input begins with speech and the floor is
///     therefore high. Until calibration completes the default threshold (0.003) applies, so
///     audio flows without added latency during calibration.
///     </para>
///     <para>
///     Calibration is deliberately internal to the recognition engine rather than surfaced as a
///     session state: the session's <c>Starting</c> state means the capture device is starting,
///     is entered before any audio exists, and calibration is content-driven (it re-arms on every
///     <see cref="Reset"/>), so reporting it would blur the state's meaning and need a new generic
///     extension point for a backend detail with no user-visible latency.
///     </para>
/// </remarks>
internal sealed class SilenceRunLimiter
{
    /// <summary>The configured constants.</summary>
    private readonly SilenceRunLimiterOptions _options;

    /// <summary>The partially filled analysis frame carried between calls.</summary>
    private readonly float[] _partial;

    /// <summary>The RMS values collected while calibrating.</summary>
    private readonly List<double> _calibration = [];

    /// <summary>The number of samples currently in <see cref="_partial"/>.</summary>
    private int _partialCount;

    /// <summary>The current quiet/loud RMS threshold.</summary>
    private double _threshold;

    /// <summary>The length, in input samples, of the current run of quiet frames (passed and dropped).</summary>
    private long _quietRunSamples;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SilenceRunLimiter"/> class.
    /// </summary>
    /// <param name="options">The constants to use, or <see langword="null"/> for the documented defaults.</param>
    public SilenceRunLimiter(SilenceRunLimiterOptions? options = null)
    {
        _options = options ?? new SilenceRunLimiterOptions();
        _partial = new float[_options.FrameSamples];
        Reset();
    }

    /// <summary>Gets the current RMS threshold below which a frame is quiet.</summary>
    public double Threshold => _threshold;

    /// <summary>Gets a value indicating whether the adaptive calibration has completed (always true when not adaptive).</summary>
    public bool IsCalibrated { get; private set; }

    /// <summary>Gets the length, in input samples, of the quiet run currently in progress (zero after a loud frame).</summary>
    public long QuietRunSamples => _quietRunSamples;

    /// <summary>Gets the total number of input samples dropped since the last <see cref="Reset"/>.</summary>
    public long DroppedSamples { get; private set; }

    /// <summary>Gets the total number of input samples in loud frames since the last <see cref="Reset"/>.</summary>
    public long LoudSamples { get; private set; }

    /// <summary>
    ///     Restores the start-of-stream state and re-arms calibration.
    /// </summary>
    public void Reset()
    {
        _partialCount = 0;
        _calibration.Clear();
        _threshold = _options.DefaultThreshold;
        _quietRunSamples = 0;
        DroppedSamples = 0;
        LoudSamples = 0;
        IsCalibrated = !_options.Adaptive;
    }

    /// <summary>
    ///     Processes one block of samples, appending the samples that pass to <paramref name="output"/>.
    /// </summary>
    /// <param name="input">The input samples.</param>
    /// <param name="output">Receives the passed samples; existing content is preserved.</param>
    public void Process(ReadOnlySpan<float> input, List<float> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var frameSamples = _options.FrameSamples;
        var offset = 0;
        while (offset < input.Length)
        {
            var take = Math.Min(frameSamples - _partialCount, input.Length - offset);
            input.Slice(offset, take).CopyTo(_partial.AsSpan(_partialCount));
            _partialCount += take;
            offset += take;

            if (_partialCount == frameSamples)
            {
                ProcessFrame(frameSamples, output);
                _partialCount = 0;
            }
        }
    }

    /// <summary>
    ///     Processes the trailing partial frame, if any, appending what passes to <paramref name="output"/>.
    /// </summary>
    /// <param name="output">Receives the passed samples.</param>
    public void Flush(List<float> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (_partialCount > 0)
        {
            ProcessFrame(_partialCount, output);
            _partialCount = 0;
        }
    }

    /// <summary>Classifies the first <paramref name="length"/> samples of the working frame and passes or drops them.</summary>
    private void ProcessFrame(int length, List<float> output)
    {
        double sum = 0;
        for (var i = 0; i < length; i++)
        {
            sum += (double)_partial[i] * _partial[i];
        }

        var rms = Math.Sqrt(sum / length);

        // Calibration frames are classified with the default threshold; the learned threshold
        // applies from the frame after calibration completes.
        var quiet = rms < _threshold;
        if (!IsCalibrated)
        {
            _calibration.Add(rms);
            if (_calibration.Count >= _options.CalibrationFrames)
            {
                CompleteCalibration();
            }
        }

        if (!quiet)
        {
            _quietRunSamples = 0;
            LoudSamples += length;
            output.AddRange(new ArraySegment<float>(_partial, 0, length));
            return;
        }

        var passQuiet = _quietRunSamples < (long)_options.MaxQuietFrames * _options.FrameSamples;
        _quietRunSamples += length;
        if (passQuiet)
        {
            output.AddRange(new ArraySegment<float>(_partial, 0, length));
        }
        else
        {
            DroppedSamples += length;
        }
    }

    /// <summary>Derives the adaptive threshold from the collected calibration RMS values.</summary>
    private void CompleteCalibration()
    {
        _calibration.Sort();
        var index = (int)(_options.FloorPercentile * (_calibration.Count - 1));
        var floor = _calibration[index];
        _threshold = Math.Clamp(
            floor * _options.ThresholdMultiplier,
            _options.MinThreshold,
            _options.MaxThreshold);
        IsCalibrated = true;
        _calibration.Clear();
    }
}
