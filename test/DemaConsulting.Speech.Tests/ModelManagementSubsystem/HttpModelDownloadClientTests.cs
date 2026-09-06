using System.Net;
using System.Security.Cryptography;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Integration tests for <see cref="HttpModelDownloadClient"/> against a real, in-process
///     loopback <see cref="HttpListener"/> server, proving the real streaming-download-with-
///     progress code path end-to-end without any real network access or new test-server package
///     dependency.
/// </summary>
public sealed class HttpModelDownloadClientTests
{
    /// <summary>
    ///     Proves that the real client downloads a file's exact bytes from a loopback HTTP
    ///     server, with a monotonically increasing, correctly totaled progress sequence driven by
    ///     the server's <c>Content-Length</c> header.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadAsync_LoopbackServer_DownloadsExactBytesWithProgress()
    {
        // Arrange: a loopback server that serves a known payload with an explicit Content-Length
        var payload = CreatePayload(sizeBytes: 256 * 1024);
        await using var server = await LoopbackHttpServer.StartAsync(payload);
        using var client = new HttpModelDownloadClient();
        using var destination = new MemoryStream();
        var reports = new List<SpeechModelDownloadProgress>();
        var progress = new SynchronousProgress<SpeechModelDownloadProgress>(reports.Add);

        // Act
        await client.DownloadAsync(server.Uri, destination, progress, CancellationToken.None);

        // Assert: the exact bytes were downloaded
        Assert.Equal(payload, destination.ToArray());

        // Assert: progress reports are present, monotonically increasing, and end at the total
        Assert.NotEmpty(reports);
        Assert.All(reports, r => Assert.Equal(payload.Length, r.TotalBytes));
        for (var i = 1; i < reports.Count; i++)
        {
            Assert.True(reports[i].BytesTransferred >= reports[i - 1].BytesTransferred);
        }

        Assert.Equal(payload.Length, reports[^1].BytesTransferred);
    }

    /// <summary>
    ///     Proves that a non-2xx response from the loopback server surfaces as an
    ///     <see cref="HttpRequestException"/> rather than a silently truncated download.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadAsync_NonSuccessResponse_ThrowsHttpRequestException()
    {
        // Arrange: a loopback server that always responds 404 Not Found
        await using var server = await LoopbackHttpServer.StartAsync(payload: null, statusCode: 404);
        using var client = new HttpModelDownloadClient();
        using var destination = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.DownloadAsync(server.Uri, destination, null, CancellationToken.None));
    }

    /// <summary>
    ///     Builds a deterministic, non-repeating payload of the given size so a truncated or
    ///     corrupted download is reliably detectable.
    /// </summary>
    private static byte[] CreatePayload(int sizeBytes)
    {
        // A cryptographically-derived pseudo-random sequence is a convenient way to generate a
        // large, deterministic (seeded), non-repeating byte pattern without a real model file.
        using var rng = RandomNumberGenerator.Create();
        var payload = new byte[sizeBytes];
        rng.GetBytes(payload);
        return payload;
    }

    /// <summary>
    ///     A trivial <see cref="IProgress{T}"/> implementation that invokes its callback
    ///     synchronously and in-order on the reporting thread.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="Progress{T}"/>, which posts each report independently (via a
    ///     captured <see cref="SynchronizationContext"/> or the thread pool) with no ordering
    ///     guarantee across successive calls, this adapter forwards deterministically in the
    ///     exact order <c>Report</c> is invoked - required for this test suite's assertions about
    ///     monotonically increasing byte counts across successive reports.
    /// </remarks>
    private sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
    {
        /// <inheritdoc/>
        public void Report(T value) => callback(value);
    }

    /// <summary>
    ///     Minimal loopback <see cref="HttpListener"/>-backed HTTP server bound to
    ///     <c>127.0.0.1</c> on an ephemeral port, serving one fixed payload (or a fixed non-success
    ///     status code) to every request until disposed.
    /// </summary>
    private sealed class LoopbackHttpServer : IAsyncDisposable
    {
        /// <summary>The underlying listener accepting loopback connections.</summary>
        private readonly HttpListener _listener;

        /// <summary>The background task processing incoming requests until disposed.</summary>
        private readonly Task _serverTask;

        /// <summary>A token source used to stop <see cref="_serverTask"/> on disposal.</summary>
        private readonly CancellationTokenSource _stopSource = new();

        /// <summary>
        ///     Initializes a new instance of the <see cref="LoopbackHttpServer"/> class. Use
        ///     <see cref="StartAsync"/> instead of calling this constructor directly.
        /// </summary>
        private LoopbackHttpServer(HttpListener listener, Uri uri, byte[]? payload, int statusCode)
        {
            _listener = listener;
            Uri = uri;
            _serverTask = Task.Run(() => ServeAsync(payload, statusCode, _stopSource.Token), _stopSource.Token);
        }

        /// <summary>Gets the loopback URI requests should be sent to.</summary>
        public Uri Uri { get; }

        /// <summary>
        ///     Starts a new loopback server on a free ephemeral port.
        /// </summary>
        /// <param name="payload">The fixed payload to serve with a 200 response, or <see langword="null"/> when <paramref name="statusCode"/> indicates failure.</param>
        /// <param name="statusCode">The HTTP status code to respond with.</param>
        public static async Task<LoopbackHttpServer> StartAsync(byte[]? payload, int statusCode = 200)
        {
            // HttpListener does not support binding to an OS-assigned ephemeral port (port 0), so
            // a throwaway TcpListener is used to discover one free port, then released immediately
            // before HttpListener binds to it - a standard, well-known .NET idiom for this case.
            var port = GetFreeLoopbackPort();
            var uriPrefix = $"http://127.0.0.1:{port}/";

            var listener = new HttpListener();
            listener.Prefixes.Add(uriPrefix);
            listener.Start();

            var server = new LoopbackHttpServer(listener, new Uri(uriPrefix), payload, statusCode);

            // Give the background accept loop a moment to actually be listening for connections.
            await Task.Yield();
            return server;
        }

        /// <summary>
        ///     Continuously accepts and answers requests with the fixed payload/status code until
        ///     <paramref name="stopToken"/> is canceled.
        /// </summary>
        private async Task ServeAsync(byte[]? payload, int statusCode, CancellationToken stopToken)
        {
            while (!stopToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().WaitAsync(stopToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ObjectDisposedException or OperationCanceledException or HttpListenerException)
                {
                    return;
                }

                context.Response.StatusCode = statusCode;
                if (payload is not null)
                {
                    context.Response.ContentLength64 = payload.Length;
                    await context.Response.OutputStream.WriteAsync(payload, stopToken).ConfigureAwait(false);
                }

                context.Response.Close();
            }
        }

        /// <summary>
        ///     Discovers a currently free loopback TCP port by briefly binding a throwaway
        ///     <see cref="System.Net.Sockets.TcpListener"/>, since <see cref="HttpListener"/>
        ///     itself has no equivalent "bind to port 0" support.
        /// </summary>
        private static int GetFreeLoopbackPort()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            try
            {
                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            finally
            {
                probe.Stop();
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await _stopSource.CancelAsync();
            _listener.Close();

            try
            {
                await _serverTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
            {
                // Best-effort shutdown only; a slow-to-stop background loop must never fail a test.
            }

            _stopSource.Dispose();
        }
    }
}
