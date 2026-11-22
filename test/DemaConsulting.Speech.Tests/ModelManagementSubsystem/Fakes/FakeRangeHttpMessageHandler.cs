using System.Net;
using System.Net.Http.Headers;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

/// <summary>
///     An in-memory <see cref="HttpMessageHandler"/> that serves one fixed file the way a real release-asset host
///     does: honoring a single <c>Range: bytes=N-</c> request with <c>206 Partial Content</c>, answering an offset at or
///     past the end with <c>416</c>, and otherwise returning the whole file with <c>200 OK</c>. Lets resume behavior be
///     tested through the real <c>HttpModelDownloadClient</c> with no network access.
/// </summary>
/// <param name="payload">The file the fake server serves.</param>
internal sealed class FakeRangeHttpMessageHandler(byte[] payload) : HttpMessageHandler
{
    /// <summary>Gets the <c>Range</c> header of every request received, in order (<see langword="null"/> when absent).</summary>
    public List<RangeHeaderValue?> ReceivedRanges { get; } = [];

    /// <summary>
    ///     Gets or sets a one-shot fault: when set, the next response body delivers this many bytes and then fails with
    ///     an <see cref="IOException"/>, as a dropped connection does. Reset to <see langword="null"/> once used.
    /// </summary>
    public long? FailAfterBytes { get; set; }

    /// <summary>
    ///     Gets or sets a one-shot custom fault: when set, the next response body delivers the given number of bytes
    ///     and then throws the exception the factory returns (the factory may first, for example, cancel the caller's
    ///     token). Reset to <see langword="null"/> once used.
    /// </summary>
    public (long AfterBytes, Func<Exception> Fault)? FaultAfterBytes { get; set; }

    /// <summary>Gets or sets a value indicating whether the server ignores <c>Range</c> and always returns the whole file.</summary>
    public bool IgnoreRange { get; set; }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ReceivedRanges.Add(request.Headers.Range);

        // Decide which slice of the file to serve
        var start = 0L;
        var partial = false;
        if (!IgnoreRange && request.Headers.Range?.Ranges.FirstOrDefault()?.From is { } from)
        {
            if (from >= payload.Length)
            {
                var notSatisfiable = new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable) { Content = new ByteArrayContent([]) };
                notSatisfiable.Content.Headers.ContentRange = new ContentRangeHeaderValue(payload.Length);
                return Task.FromResult(notSatisfiable);
            }

            start = from;
            partial = true;
        }

        // Serve the slice, optionally through a stream that faults or interrupts part-way
        var body = payload[(int)start..];
        Stream bodyStream = new MemoryStream(body);
        if (FailAfterBytes is { } failAfter)
        {
            FailAfterBytes = null;
            bodyStream = new FaultingStream(body, failAfter, () => new IOException("Simulated connection reset."));
        }
        else if (FaultAfterBytes is { } fault)
        {
            FaultAfterBytes = null;
            bodyStream = new FaultingStream(body, fault.AfterBytes, fault.Fault);
        }

        var response = new HttpResponseMessage(partial ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
        {
            Content = new StreamContent(bodyStream),
        };
        response.Content.Headers.ContentLength = body.Length;
        if (partial)
        {
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, payload.Length - 1, payload.Length);
        }

        return Task.FromResult(response);
    }

    /// <summary>
    ///     A read-only stream over some bytes that throws a fault once a given number of bytes has been delivered.
    /// </summary>
    /// <param name="bytes">The bytes to deliver.</param>
    /// <param name="faultAfter">The number of bytes delivered before the fault is thrown.</param>
    /// <param name="fault">Creates the exception to throw (and may perform a side effect first).</param>
    private sealed class FaultingStream(byte[] bytes, long faultAfter, Func<Exception> fault) : Stream
    {
        /// <summary>The number of bytes delivered so far.</summary>
        private long _position;

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => bytes.Length;

        /// <inheritdoc/>
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            // Deliver bytes only up to the fault point, then fail the read as a dropped connection would
            if (_position >= faultAfter)
            {
                throw fault();
            }

            var available = (int)Math.Min(Math.Min(count, bytes.Length - _position), faultAfter - _position);
            if (available <= 0)
            {
                return 0;
            }

            Array.Copy(bytes, _position, buffer, offset, available);
            _position += available;
            return available;
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            // Read-only stream: nothing to flush.
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
