using System.Diagnostics;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.SttEvaluation;

/// <summary>
///     One recognition result reported while a recording was streamed, stamped with the audio-clock
///     position at which it was reported.
/// </summary>
/// <param name="SamplePosition">
///     The number of samples fed to the backend when the result was reported (the audio clock).
///     Results reported by the end-of-stream flush carry the total number of fed samples.
/// </param>
/// <param name="IsFinal">Whether the result is a finalized (committed) segment rather than a provisional hypothesis.</param>
/// <param name="Text">The result text after the model's own text normalization.</param>
internal sealed record SttResultEvent(long SamplePosition, bool IsFinal, string Text);

/// <summary>
///     Everything one streamed transcription produced: every result with its audio-clock position,
///     the amount of audio fed and the wall-clock time the backend took.
/// </summary>
/// <remarks>
///     The derived properties are the metrics the evaluation reports: the transcript is the finals
///     joined in order (as a host that only keeps committed text sees it), the segment count is the
///     number of non-empty finals, and the latencies are measured on the audio clock so they do not
///     depend on the speed of the machine. Immutable and thread-safe.
/// </remarks>
/// <param name="Events">Every reported result in order, provisional and final.</param>
/// <param name="SampleRate">The sample rate of the fed audio, in Hz.</param>
/// <param name="SpeechSamples">The number of samples of the recording itself (excluding added trailing silence).</param>
/// <param name="FedSamples">The total number of samples fed, including added trailing silence.</param>
/// <param name="ProcessingTime">The wall-clock time spent in the backend calls (accept, decode, flush).</param>
internal sealed record SttTranscript(
    IReadOnlyList<SttResultEvent> Events,
    int SampleRate,
    long SpeechSamples,
    long FedSamples,
    TimeSpan ProcessingTime)
{
    /// <summary>Gets the finalized results with non-empty text, in order: the committed segments.</summary>
    public IReadOnlyList<SttResultEvent> Finals { get; } =
        Events.Where(e => e.IsFinal && !string.IsNullOrWhiteSpace(e.Text)).ToList();

    /// <summary>Gets the transcript: the text of every committed segment, trimmed and joined with single spaces.</summary>
    public string Text => string.Join(' ', Finals.Select(e => e.Text.Trim()));

    /// <summary>Gets the number of committed (non-empty final) segments.</summary>
    public int SegmentCount => Finals.Count;

    /// <summary>Gets the duration of the recording itself, in seconds.</summary>
    public double SpeechSeconds => (double)SpeechSamples / SampleRate;

    /// <summary>
    ///     Gets the real-time factor: processing time divided by the duration of all fed audio
    ///     (below 1 means faster than real time). Zero when nothing was fed.
    /// </summary>
    public double RealTimeFactor => FedSamples == 0 ? 0.0 : ProcessingTime.TotalSeconds * SampleRate / FedSamples;

    /// <summary>
    ///     Gets the audio-clock time, in seconds from the start of the recording, at which the first
    ///     result with text (provisional or final) was reported; <see langword="null"/> when no
    ///     result had text.
    /// </summary>
    public double? FirstResultSeconds =>
        Events.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Text)) is { } first
            ? (double)first.SamplePosition / SampleRate
            : null;

    /// <summary>
    ///     Gets the audio-clock time, in seconds, from the end of the recording to the report of the
    ///     last committed segment (negative when it was committed before the recording ended);
    ///     <see langword="null"/> when nothing was committed.
    /// </summary>
    public double? FinalLagSeconds =>
        Finals.LastOrDefault() is { } last
            ? (double)(last.SamplePosition - SpeechSamples) / SampleRate
            : null;
}

/// <summary>
///     Streams a recording through a recognition backend the way a live recognition session does
///     and records every result it reports.
/// </summary>
/// <remarks>
///     The evaluation must exercise the engine through the same call pattern the library uses in
///     production, or its numbers would not describe what a user gets. This mirrors
///     <c>RecognitionSession</c>: each block is accepted and then decoded until the backend reports
///     nothing new (at most <see cref="MaxResultsPerBlock"/> results per block), every result's text
///     passes through the model's <c>NormalizeText(text, isFinal)</c> hook, and at the end the
///     backend is drained (at most <see cref="MaxResultsAtFlush"/> results) and flushed once with
///     <see cref="IRecognitionBackend.TryFlush"/>, as a session does when it stops. Blocks are
///     <see cref="BlockMilliseconds"/> long, a typical live-capture callback (the capture device lets
///     the audio host choose its buffer size). The backend is not reset; callers reset it between
///     independent recordings. Stateless; the backend itself is not thread-safe, so a backend must
///     not be shared between concurrent calls.
/// </remarks>
internal static class SttTranscriber
{
    /// <summary>The length of one fed block, in milliseconds.</summary>
    public const int BlockMilliseconds = 20;

    /// <summary>The most results drained after one block (mirrors <c>RecognitionSession</c>'s per-frame bound).</summary>
    public const int MaxResultsPerBlock = 32;

    /// <summary>The most results drained before the final flush (mirrors <c>RecognitionSession</c>'s final-drain bound).</summary>
    public const int MaxResultsAtFlush = 1024;

    /// <summary>
    ///     Streams <paramref name="samples"/>, optionally followed by digital silence, through
    ///     <paramref name="backend"/> and flushes it, recording every result.
    /// </summary>
    /// <param name="backend">The loaded backend to feed. Must not be <see langword="null"/>.</param>
    /// <param name="samples">The recording's mono samples at <paramref name="sampleRate"/>. Must not be <see langword="null"/>.</param>
    /// <param name="sampleRate">The sample rate of <paramref name="samples"/>, which must be the backend's rate, in Hz. Must be at least 50.</param>
    /// <param name="normalizeText">
    ///     The model's text hook applied to every result (normally <c>IRecognitionModel.NormalizeText(text, isFinal)</c>),
    ///     or <see langword="null"/> to keep the backend's text unchanged.
    /// </param>
    /// <param name="trailingSilenceSeconds">Seconds of digital silence fed after the recording, before the flush. Must not be negative.</param>
    /// <returns>The recorded results and the timing of the run.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="backend"/> or <paramref name="samples"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="sampleRate"/> is below 50 (a 20 ms block would be empty) or
    ///     <paramref name="trailingSilenceSeconds"/> is negative.
    /// </exception>
    /// <remarks>Any exception thrown by the backend propagates unchanged.</remarks>
    public static SttTranscript Transcribe(
        IRecognitionBackend backend,
        float[] samples,
        int sampleRate,
        Func<string, bool, string>? normalizeText = null,
        double trailingSilenceSeconds = 0.0)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 1000 / BlockMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegative(trailingSilenceSeconds);

        var blockSize = sampleRate * BlockMilliseconds / 1000;
        var silence = new float[(long)Math.Round(trailingSilenceSeconds * sampleRate)];
        var events = new List<SttResultEvent>();
        var position = 0L;
        var stopwatch = Stopwatch.StartNew();

        // Stream the recording, then the trailing silence, block by block
        foreach (var audio in new[] { samples, silence })
        {
            for (var offset = 0; offset < audio.Length; offset += blockSize)
            {
                var length = Math.Min(blockSize, audio.Length - offset);
                backend.AcceptSamples(audio.AsSpan(offset, length));
                position += length;
                Drain(backend, MaxResultsPerBlock, position, normalizeText, events);
            }
        }

        // End of stream: drain what is still queued, then flush the trailing audio once
        Drain(backend, MaxResultsAtFlush, position, normalizeText, events);
        if (backend.TryFlush(out var flushed) && flushed is not null)
        {
            events.Add(ToEvent(flushed, position, normalizeText));
        }

        stopwatch.Stop();
        return new SttTranscript(events, sampleRate, samples.Length, position, stopwatch.Elapsed);
    }

    /// <summary>
    ///     Decodes until the backend reports nothing new or <paramref name="maxResults"/> results
    ///     were taken, recording each one at <paramref name="position"/>.
    /// </summary>
    /// <param name="backend">The backend to drain.</param>
    /// <param name="maxResults">The most results to take.</param>
    /// <param name="position">The audio-clock position to stamp on each result.</param>
    /// <param name="normalizeText">The model's text hook, or <see langword="null"/>.</param>
    /// <param name="events">The list the results are appended to.</param>
    private static void Drain(
        IRecognitionBackend backend,
        int maxResults,
        long position,
        Func<string, bool, string>? normalizeText,
        List<SttResultEvent> events)
    {
        for (var i = 0; i < maxResults; i++)
        {
            if (!backend.TryDecode(out var result) || result is null)
            {
                return;
            }

            events.Add(ToEvent(result, position, normalizeText));
        }
    }

    /// <summary>Converts one backend result into a recorded event, applying the model's text hook.</summary>
    /// <param name="result">The backend result.</param>
    /// <param name="position">The audio-clock position of the report.</param>
    /// <param name="normalizeText">The model's text hook, or <see langword="null"/>.</param>
    /// <returns>The recorded event.</returns>
    private static SttResultEvent ToEvent(
        SpeechRecognitionResult result,
        long position,
        Func<string, bool, string>? normalizeText)
    {
        var text = normalizeText is null ? result.Text : normalizeText(result.Text, result.IsFinal);
        return new SttResultEvent(position, result.IsFinal, text);
    }
}
