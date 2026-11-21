using System.Globalization;
using System.Runtime.InteropServices;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;

/// <summary>
///     The streaming <see cref="IRecognitionBackend"/> for the Nemotron ONNX model: runs incoming
///     audio through the silence-run limiter, dither, log-mel feature extraction, the cache-aware
///     encoder (in 56-frame chunks with a 9-frame pre-encode context), and the RNN-T greedy
///     decoder, and reports provisional and final text.
/// </summary>
/// <remarks>
///     <b>Flow</b>: <see cref="AcceptSamples"/> only buffers - samples pass through
///     <see cref="SilenceRunLimiter"/> one limiter frame at a time, then <see cref="DitherNoise"/>,
///     then <see cref="NemotronFeatureExtractor"/>; <see cref="TryDecode"/> runs every complete
///     chunk through the encoder and decodes its output frames one by one, reporting the
///     utterance text so far as a provisional result whenever it changes. Nothing here is
///     thread-safe, matching the backend contract.
///     <para>
///     <b>Endpointing</b> (decode clock): every decoded encoder frame is reported to a
///     <see cref="NemotronEndpointDetector"/> - a token marks speech, a blank frame lengthens the
///     trailing silence by the frame's input duration (80 ms for the real model). Quiet audio the
///     limiter removed is credited to the detector at the feature-frame position where it was
///     removed, so a pause counts its full input duration in the right order relative to the
///     tokens around it. When a rule fires the utterance text is reported as final and cleared;
///     the encoder caches, decoder state and feature stream are deliberately <i>kept</i>, so
///     speech that resumes is decoded with its full left context and no word is lost or replayed.
///     The maximum-length rule splits only between words: the last (possibly incomplete) word is
///     carried into the next utterance. Silence alone never produces a result. Because the model
///     state is kept, the model may emit the closing punctuation of a finalized sentence after
///     its endpoint; such punctuation at the start of a segment is dropped from the text.
///     </para>
///     <para>
///     <b>Silence padding</b>: a chunk is only ever completed with <i>real silence features</i> -
///     dithered digital silence run through the feature extractor - never with all-zero log-mel
///     rows, which the model hears as loud sound and answers with invented words. Padding happens
///     when a quiet run reaches the limiter's cap after speech (no more audio arrives, so the
///     pending partial chunk would otherwise wait) and at flush. Padding frames never advance the
///     endpoint clocks.
///     </para>
///     <para>
///     <b>Flush</b> (<see cref="TryFlush"/>): decodes pending chunks, feeds the limiter's partial
///     frame, completes the partial chunk with silence plus <see cref="NemotronEngineOptions.FlushTailChunks"/>
///     whole chunks of silence (so the encoder's look-ahead releases the last words), decodes
///     them, and returns every final of the flushed audio joined into one. <see cref="Reset"/>
///     restores the complete start-of-stream state.
///     </para>
/// </remarks>
internal sealed class OnnxNemotronRecognitionEngine : IRecognitionBackend
{
    /// <summary>Frames in one encoder input: pre-encode context plus the new chunk.</summary>
    private const int EncoderFrames = NemotronEngineOptions.PreEncodeFrames + NemotronEngineOptions.ChunkFrames;

    /// <summary>
    ///     The sentence-closing punctuation (besides the Unicode closing brackets and quotes) that
    ///     never opens a segment: Latin, ellipsis, and the CJK and full-width forms.
    /// </summary>
    private const string SentenceClosingPunctuation = ".,;:!?\u2026\u3001\u3002\uFF01\uFF0C\uFF0E\uFF1A\uFF1B\uFF1F";

    /// <summary>The encoder.</summary>
    private readonly INemotronEncoder _encoder;

    /// <summary>The greedy decoder.</summary>
    private readonly NemotronRnntGreedyDecoder _decoder;

    /// <summary>The vocabulary used to detokenize.</summary>
    private readonly NemotronVocabulary _vocabulary;

    /// <summary>The constants in use.</summary>
    private readonly NemotronEngineOptions _options;

    /// <summary>The silence-run limiter.</summary>
    private readonly SilenceRunLimiter _limiter;

    /// <summary>The dither source.</summary>
    private readonly DitherNoise _dither;

    /// <summary>The feature extractor.</summary>
    private readonly NemotronFeatureExtractor _extractor = new();

    /// <summary>The decode-clock endpoint detector.</summary>
    private readonly NemotronEndpointDetector _detector;

    /// <summary>The limiter output for the current slice.</summary>
    private readonly List<float> _limited = [];

    /// <summary>One hop of silence, re-dithered for every padding hop.</summary>
    private readonly float[] _silence = new float[NemotronFeatureExtractor.HopLength];

    /// <summary>The encoder input: pre-encode context rows followed by the chunk rows.</summary>
    private readonly float[] _chunkInput = new float[EncoderFrames * NemotronFeatureExtractor.MelBands];

    /// <summary>The pre-encode context carried to the next chunk (the previous chunk's last rows).</summary>
    private readonly float[] _preEncode = new float[NemotronEngineOptions.PreEncodeFrames * NemotronFeatureExtractor.MelBands];

    /// <summary>The token ids of the utterance in progress.</summary>
    private readonly List<int> _tokens = [];

    /// <summary>Results waiting to be returned.</summary>
    private readonly Queue<SpeechRecognitionResult> _results = new();

    /// <summary>Quiet input removed by the limiter, by the feature-frame position it was removed at, oldest first.</summary>
    private readonly List<DroppedSilence> _droppedSilence = [];

    /// <summary>The feature-frame ranges made of synthetic padding, oldest first.</summary>
    private readonly List<PaddingRange> _paddingRanges = [];

    /// <summary>The number of input samples the limiter analyzes at a time (one limiter frame).</summary>
    private readonly int _sliceSamples;

    /// <summary>The quiet-run length (the limiter's quiet cap) after which a partial chunk is padded.</summary>
    private readonly long _padAfterQuietSamples;

    /// <summary>The text last reported as a provisional result.</summary>
    private string _lastReported = string.Empty;

    /// <summary>The number of feature frames handed to the encoder since the last reset (the absolute index of the next chunk).</summary>
    private long _framesDequeued;

    /// <summary>Whether the last entry of <see cref="_paddingRanges"/> is still open (padding is the latest audio fed).</summary>
    private bool _paddingOpen;

    /// <summary>Whether loud audio has been seen since the last reset (padding is never applied to leading silence).</summary>
    private bool _speechSeen;

    /// <summary>Whether real audio reached the feature extractor since the last flush tail (or reset).</summary>
    private bool _realAudioSinceTail;

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxNemotronRecognitionEngine"/> class.
    /// </summary>
    /// <param name="encoder">The encoder; ownership transfers to this instance.</param>
    /// <param name="decoder">The greedy decoder; ownership transfers to this instance.</param>
    /// <param name="vocabulary">The vocabulary.</param>
    /// <param name="options">The constants, or <see langword="null"/> for the documented defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when a collaborator is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <see cref="NemotronEngineOptions.FlushTailChunks"/> is negative, an endpoint
    ///     rule is negative, or the dither amplitude is invalid.
    /// </exception>
    public OnnxNemotronRecognitionEngine(
        INemotronEncoder encoder,
        NemotronRnntGreedyDecoder decoder,
        NemotronVocabulary vocabulary,
        NemotronEngineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(encoder);
        ArgumentNullException.ThrowIfNull(decoder);
        ArgumentNullException.ThrowIfNull(vocabulary);

        // Validate the configuration before taking any collaborator into use
        _options = options ?? new NemotronEngineOptions();
        if (_options.FlushTailChunks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _options.FlushTailChunks,
                "The number of flush tail chunks must not be negative.");
        }

        _detector = new NemotronEndpointDetector(_options.Endpoint, NemotronFeatureExtractor.SampleRate);
        _dither = new DitherNoise(_options.DitherAmplitude, _options.DitherSeed);

        _encoder = encoder;
        _decoder = decoder;
        _vocabulary = vocabulary;
        _limiter = new SilenceRunLimiter(_options.Limiter);
        var limiterOptions = _options.Limiter ?? new SilenceRunLimiterOptions();
        _sliceSamples = limiterOptions.FrameSamples;
        _padAfterQuietSamples = (long)limiterOptions.MaxQuietFrames * limiterOptions.FrameSamples;
    }

    /// <summary>Gets the model's required input sample rate, in hertz.</summary>
    public static int SampleRate => NemotronFeatureExtractor.SampleRate;

    /// <summary>Gets the absolute feature-frame index of the next frame the extractor will emit.</summary>
    private long FeaturePosition => _framesDequeued + _extractor.FrameCount;

    /// <inheritdoc/>
    public void AcceptSamples(ReadOnlySpan<float> monoSamples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Feed one limiter frame at a time: each slice is then either passed or dropped as a
        // whole, so removed quiet audio is recorded exactly between the audio before and after it
        for (var offset = 0; offset < monoSamples.Length; offset += _sliceSamples)
        {
            AcceptSlice(monoSamples.Slice(offset, Math.Min(_sliceSamples, monoSamples.Length - offset)));
        }
    }

    /// <inheritdoc/>
    public bool TryDecode(out SpeechRecognitionResult? result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_results.Count == 0)
        {
            Pump();
        }

        return _results.TryDequeue(out result);
    }

    /// <inheritdoc/>
    public bool TryFlush(out SpeechRecognitionResult? result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Decode what is complete, then feed the limiter's partial frame
        Pump();
        var droppedBefore = _limiter.DroppedSamples;
        _limited.Clear();
        _limiter.Flush(_limited);
        FeedAudio();
        RecordDroppedSilence(_limiter.DroppedSamples - droppedBefore);

        // Complete the partial chunk and add the tail chunks with silence features, once per
        // stretch of real audio, so the encoder's look-ahead releases the last words
        if (_realAudioSinceTail)
        {
            AppendSilenceTail();
            _realAudioSinceTail = false;
        }

        while (_extractor.FrameCount >= NemotronEngineOptions.ChunkFrames)
        {
            DecodeChunk();
        }

        // Whatever text remains is final; the next audio starts a new segment
        CreditDroppedSilence(_framesDequeued);
        FinalizeUtterance();

        // Earlier provisional results are superseded; the final results are joined so text
        // finalized by an endpoint inside the flushed audio is not lost
        var finals = new List<string>();
        while (_results.TryDequeue(out var queued))
        {
            if (queued.IsFinal)
            {
                finals.Add(queued.Text);
            }
        }

        result = finals.Count > 0 ? new SpeechRecognitionResult(string.Join(' ', finals), IsFinal: true) : null;
        return result is not null;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Session reset: everything returns to the start-of-stream state, including the model
        _limiter.Reset();
        _dither.Reset();
        _results.Clear();
        _encoder.Reset();
        _decoder.Reset();
        _extractor.Reset();
        _detector.Reset();
        Array.Clear(_preEncode);
        _tokens.Clear();
        _droppedSilence.Clear();
        _paddingRanges.Clear();
        _paddingOpen = false;
        _lastReported = string.Empty;
        _framesDequeued = 0;
        _speechSeen = false;
        _realAudioSinceTail = false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _decoder.Dispose();
        _encoder.Dispose();
    }

    /// <summary>Limits and buffers one slice (at most one limiter frame) and pads a partial chunk after a capped quiet run.</summary>
    /// <param name="slice">The input samples.</param>
    private void AcceptSlice(ReadOnlySpan<float> slice)
    {
        // Pass the slice through the limiter; what passes goes to the extractor, what is
        // dropped is recorded at the current feature position
        var loudBefore = _limiter.LoudSamples;
        var droppedBefore = _limiter.DroppedSamples;
        _limited.Clear();
        _limiter.Process(slice, _limited);
        FeedAudio();
        RecordDroppedSilence(_limiter.DroppedSamples - droppedBefore);

        if (_limiter.LoudSamples > loudBefore)
        {
            _speechSeen = true;
        }

        // Once the limiter drops a quiet run no further audio arrives, so complete the partial
        // chunk with silence now rather than leaving the last words waiting in it
        if (_speechSeen && _limiter.QuietRunSamples >= _padAfterQuietSamples)
        {
            PadPartialChunk();
        }
    }

    /// <summary>Dithers the limiter's output and hands it to the feature extractor as real audio.</summary>
    private void FeedAudio()
    {
        if (_limited.Count == 0)
        {
            return;
        }

        EndPadding();
        var span = CollectionsMarshal.AsSpan(_limited);
        _dither.Apply(span);
        _extractor.Accept(span);
        _realAudioSinceTail = true;
    }

    /// <summary>Records quiet input the limiter removed, at the current feature position.</summary>
    /// <param name="samples">The number of removed samples (zero records nothing).</param>
    private void RecordDroppedSilence(long samples)
    {
        if (samples <= 0)
        {
            return;
        }

        // Consecutive removals at one position (nothing fed between them) are merged
        var position = FeaturePosition;
        if (_droppedSilence.Count > 0 && _droppedSilence[^1].Position == position)
        {
            _droppedSilence[^1] = _droppedSilence[^1] with { Samples = _droppedSilence[^1].Samples + samples };
        }
        else
        {
            _droppedSilence.Add(new DroppedSilence(position, samples));
        }
    }

    /// <summary>
    ///     Credits to the endpoint detector the removed quiet audio recorded at or before
    ///     <paramref name="position"/> (every feature frame before it has been decoded), checking
    ///     the endpoint rules after each credit.
    /// </summary>
    /// <param name="position">The absolute feature-frame index decoding has reached.</param>
    private void CreditDroppedSilence(long position)
    {
        while (_droppedSilence.Count > 0 && _droppedSilence[0].Position <= position)
        {
            _detector.AdvanceSilence(_droppedSilence[0].Samples);
            _droppedSilence.RemoveAt(0);
            CheckEndpoint();
        }
    }

    /// <summary>
    ///     Completes a partly filled chunk with silence features. The silence stands in for the
    ///     quiet audio the limiter drops, which is credited to the endpoint clocks separately.
    /// </summary>
    private void PadPartialChunk()
    {
        var remainder = _extractor.FrameCount % NemotronEngineOptions.ChunkFrames;
        if (remainder == 0)
        {
            return;
        }

        AppendSilenceUntil(_extractor.FrameCount - remainder + NemotronEngineOptions.ChunkFrames);
    }

    /// <summary>
    ///     Completes the partial chunk and appends <see cref="NemotronEngineOptions.FlushTailChunks"/>
    ///     whole chunks, all as silence features.
    /// </summary>
    private void AppendSilenceTail()
    {
        var pending = _extractor.FrameCount;
        var whole = (pending + NemotronEngineOptions.ChunkFrames - 1) / NemotronEngineOptions.ChunkFrames;
        AppendSilenceUntil((whole + _options.FlushTailChunks) * NemotronEngineOptions.ChunkFrames);
    }

    /// <summary>
    ///     Feeds dithered digital silence straight into the feature extractor (bypassing the
    ///     limiter) until <paramref name="targetFrames"/> frames are queued, marking it as padding.
    /// </summary>
    /// <param name="targetFrames">The queued frame count to reach.</param>
    private void AppendSilenceUntil(int targetFrames)
    {
        if (_extractor.FrameCount >= targetFrames)
        {
            return;
        }

        BeginPadding();
        while (_extractor.FrameCount < targetFrames)
        {
            Array.Clear(_silence);
            _dither.Apply(_silence);
            _extractor.Accept(_silence);
        }
    }

    /// <summary>Opens a padding range at the current feature position, unless one is already open.</summary>
    private void BeginPadding()
    {
        if (!_paddingOpen)
        {
            _paddingRanges.Add(new PaddingRange(FeaturePosition, long.MaxValue));
            _paddingOpen = true;
        }
    }

    /// <summary>Closes the open padding range at the current feature position, because real audio follows.</summary>
    private void EndPadding()
    {
        if (!_paddingOpen)
        {
            return;
        }

        _paddingOpen = false;
        var position = FeaturePosition;
        if (_paddingRanges[^1].Start >= position)
        {
            _paddingRanges.RemoveAt(_paddingRanges.Count - 1);
        }
        else
        {
            _paddingRanges[^1] = _paddingRanges[^1] with { End = position };
        }
    }

    /// <summary>Determines whether the feature frame at <paramref name="position"/> is padding, forgetting ranges already passed.</summary>
    /// <param name="position">The absolute feature-frame index; calls must not go backwards.</param>
    /// <returns><see langword="true"/> when the frame lies in a padding range.</returns>
    private bool IsPadding(long position)
    {
        while (_paddingRanges.Count > 0 && _paddingRanges[0].End <= position)
        {
            _paddingRanges.RemoveAt(0);
        }

        return _paddingRanges.Count > 0 && _paddingRanges[0].Start <= position;
    }

    /// <summary>Decodes every complete chunk, credits removed silence, checks the endpoint, and reports changed text.</summary>
    private void Pump()
    {
        while (_extractor.FrameCount >= NemotronEngineOptions.ChunkFrames)
        {
            DecodeChunk();
        }

        CreditDroppedSilence(_framesDequeued);
        ReportProvisional();
    }

    /// <summary>
    ///     Runs the next queued chunk through the encoder and decodes its output frames one at a
    ///     time, keeping the endpoint detector in step with the stream.
    /// </summary>
    private void DecodeChunk()
    {
        const int mels = NemotronFeatureExtractor.MelBands;
        const int preFloats = NemotronEngineOptions.PreEncodeFrames * mels;
        const int chunkFloats = NemotronEngineOptions.ChunkFrames * mels;

        // Assemble the encoder input: the carried pre-encode rows, then the chunk's rows
        _preEncode.CopyTo(_chunkInput, 0);
        var chunk = _chunkInput.AsSpan(preFloats, chunkFloats);
        _extractor.Dequeue(NemotronEngineOptions.ChunkFrames, chunk);
        chunk[(chunkFloats - preFloats)..].CopyTo(_preEncode);
        var chunkStart = _framesDequeued;
        _framesDequeued += NemotronEngineOptions.ChunkFrames;

        var output = _encoder.Encode(_chunkInput, EncoderFrames, out var outputFrames);
        if (outputFrames > 0)
        {
            // Each output frame stands for an equal share of the chunk's input duration
            var hidden = _encoder.HiddenSize;
            var frameSamples = (long)NemotronEngineOptions.ChunkFrames * NemotronFeatureExtractor.HopLength / outputFrames;
            for (var k = 0; k < outputFrames; k++)
            {
                // Removed silence recorded before this frame's features comes first
                var featureStart = chunkStart + ((long)k * NemotronEngineOptions.ChunkFrames / outputFrames);
                CreditDroppedSilence(featureStart);

                // Decode the frame and tell the detector whether it held speech; closing
                // punctuation that opens a segment belongs to the segment already finalized
                var segmentWasEmpty = _tokens.Count == 0;
                var emitted = _decoder.Decode(output.Slice(k * hidden, hidden), 1, hidden, _tokens) > 0;
                if (emitted && segmentWasEmpty)
                {
                    emitted = DropLeadingClosingPunctuation();
                }

                if (IsPadding(featureStart))
                {
                    _detector.AdvancePaddingFrame(emitted);
                }
                else
                {
                    _detector.AdvanceFrame(frameSamples, emitted);
                }

                CheckEndpoint();
            }
        }

        // Silence removed right at the chunk end belongs before the next chunk's speech
        CreditDroppedSilence(_framesDequeued);
    }

    /// <summary>
    ///     Removes the closing-punctuation tokens that open the segment just started, because they
    ///     end the sentence of the segment already finalized.
    /// </summary>
    /// <returns><see langword="true"/> when tokens remain (the frame still held speech); otherwise <see langword="false"/>.</returns>
    /// <remarks>
    ///     The model keeps its state across endpoints and often emits a sentence's closing
    ///     punctuation (".", ",", "?") a little after the endpoint has already finalized that
    ///     sentence. Kept, it would become a final of its own (".") or open the next segment
    ///     (", and then"). Dropped tokens still advanced the prediction network, so the model's
    ///     context is unchanged; they only leave the reported text and do not count as speech for
    ///     the endpoint detector. Call only when the segment was empty before the frame was decoded.
    /// </remarks>
    private bool DropLeadingClosingPunctuation()
    {
        var count = 0;
        while (count < _tokens.Count && IsClosingPunctuation(_tokens[count]))
        {
            count++;
        }

        _tokens.RemoveRange(0, count);
        return _tokens.Count > 0;
    }

    /// <summary>Determines whether a token's text consists only of sentence-closing punctuation.</summary>
    /// <param name="token">The token id.</param>
    /// <returns><see langword="true"/> for tokens such as ".", "," or ")"; <see langword="false"/> for words and opening punctuation.</returns>
    private bool IsClosingPunctuation(int token)
    {
        var text = _vocabulary.Detokenize([token]);
        return text.Length > 0 && text.All(c =>
            SentenceClosingPunctuation.Contains(c) ||
            char.GetUnicodeCategory(c) is UnicodeCategory.ClosePunctuation or UnicodeCategory.FinalQuotePunctuation);
    }

    /// <summary>Applies the endpoint detector's decision to the utterance in progress.</summary>
    private void CheckEndpoint()
    {
        var reason = _detector.Check();
        if (reason == NemotronEndpointReason.None)
        {
            return;
        }

        if (_tokens.Count == 0)
        {
            // Silence alone never produces a result; just start a new segment
            _detector.Reset();
            return;
        }

        if (reason == NemotronEndpointReason.MaxUtteranceLength)
        {
            SplitAtLastWord();
            return;
        }

        ReportProvisional();
        FinalizeUtterance();
    }

    /// <summary>
    ///     Ends an over-long utterance between words: the tokens before the last word start are
    ///     reported as final and the last (possibly still growing) word is carried into the next
    ///     utterance, so no word is cut in two or lost.
    /// </summary>
    private void SplitAtLastWord()
    {
        var split = _tokens.FindLastIndex(_vocabulary.StartsWord);
        if (split > 0)
        {
            ReportFinal(_tokens.GetRange(0, split));
            _tokens.RemoveRange(0, split);
            _lastReported = string.Empty;
        }

        _detector.Reset(carriesSpeech: true);
    }

    /// <summary>Reports the whole utterance as final (when it has text), clears it, and starts a new segment; the model state is kept.</summary>
    private void FinalizeUtterance()
    {
        ReportFinal(_tokens);
        _tokens.Clear();
        _lastReported = string.Empty;
        _detector.Reset();
    }

    /// <summary>Queues a provisional result when the utterance text has changed.</summary>
    private void ReportProvisional()
    {
        if (_tokens.Count == 0)
        {
            return;
        }

        var text = _vocabulary.Detokenize(_tokens);
        if (text.Length > 0 && !string.Equals(text, _lastReported, StringComparison.Ordinal))
        {
            _lastReported = text;
            _results.Enqueue(new SpeechRecognitionResult(text, IsFinal: false));
        }
    }

    /// <summary>Queues the text of <paramref name="tokens"/> as a final result when it is not empty.</summary>
    /// <param name="tokens">The token ids to finalize.</param>
    private void ReportFinal(IReadOnlyList<int> tokens)
    {
        var text = tokens.Count == 0 ? string.Empty : _vocabulary.Detokenize(tokens);
        if (text.Length > 0)
        {
            _results.Enqueue(new SpeechRecognitionResult(text, IsFinal: true));
        }
    }

    /// <summary>Quiet input removed by the limiter: <paramref name="Samples"/> input samples removed just before feature frame <paramref name="Position"/>.</summary>
    /// <param name="Position">The absolute feature-frame index the removal precedes.</param>
    /// <param name="Samples">The number of removed input samples.</param>
    private readonly record struct DroppedSilence(long Position, long Samples);

    /// <summary>A run of padding feature frames, <paramref name="Start"/> inclusive to <paramref name="End"/> exclusive.</summary>
    /// <param name="Start">The absolute index of the first padding frame.</param>
    /// <param name="End">The absolute index after the last padding frame (<see cref="long.MaxValue"/> while open).</param>
    private readonly record struct PaddingRange(long Start, long End);
}
