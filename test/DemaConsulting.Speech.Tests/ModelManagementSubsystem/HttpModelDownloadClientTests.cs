using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;
using Microsoft.AspNetCore.Builder;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Integration tests for <see cref="HttpModelDownloadClient"/> against an in-process
///     <see cref="WireMockServer"/>, proving the real streaming-download-with-progress and
///     mirror-authentication code paths end-to-end without any real network access.
/// </summary>
public sealed class HttpModelDownloadClientTests
{
    /// <summary>
    ///     Proves that the real client downloads a file's exact bytes from a WireMock-stubbed
    ///     server, with a monotonically increasing, correctly totaled progress sequence driven by
    ///     the server's <c>Content-Length</c> header.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadAsync_StubbedServer_DownloadsExactBytesWithProgress()
    {
        // Arrange: a stubbed server that serves a known payload with an explicit Content-Length.
        // Kestrel (WireMock.Net's self-hosted server) otherwise always responds with
        // Transfer-Encoding: chunked and strips any explicit Content-Length header, so
        // PreWireMockMiddlewareInit buffers the body and sets Response.ContentLength itself
        // before anything is written, forcing a real Content-Length response.
        var payload = CreatePayload(sizeBytes: 256 * 1024);
        using var server = WireMockServer.Start(new WireMockServerSettings
        {
            PreWireMockMiddlewareInit = appBuilderObj =>
            {
                var appBuilder = (IApplicationBuilder)appBuilderObj;
                appBuilder.Use(async (context, next) =>
                {
                    var originalBody = context.Response.Body;
                    await using var buffer = new MemoryStream();
                    context.Response.Body = buffer;
                    await next();
                    context.Response.Body = originalBody;
                    context.Response.ContentLength = buffer.Length;
                    buffer.Seek(0, SeekOrigin.Begin);
                    await buffer.CopyToAsync(originalBody);
                });
            },
        });
        server
            .Given(Request.Create().WithPath("/model.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload));

        using var client = new HttpModelDownloadClient();
        using var destination = new MemoryStream();
        var reports = new List<SpeechModelDownloadProgress>();
        var progress = new SynchronousProgress<SpeechModelDownloadProgress>(reports.Add);
        var sourceUri = new Uri($"{server.Urls[0]}/model.bin");

        // Act
        await client.DownloadAsync(sourceUri, destination, progress, CancellationToken.None);

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
        Assert.All(reports, r =>
        {
            Assert.Equal(0, r.FileIndex);
            Assert.Equal(1, r.FileCount);
        });
    }

    /// <summary>
    ///     Proves that a successful download leaves the caller-owned destination stream open and
    ///     usable, since the caller (not the client) owns its lifetime.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadAsync_Success_LeavesDestinationOpen()
    {
        // Arrange
        var payload = CreatePayload(sizeBytes: 16);
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/model.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload));

        using var client = new HttpModelDownloadClient();
        using var destination = new MemoryStream();
        var sourceUri = new Uri($"{server.Urls[0]}/model.bin");

        // Act
        await client.DownloadAsync(sourceUri, destination, null, CancellationToken.None);

        // Assert
        Assert.True(destination.CanWrite);
        Assert.Equal(payload, destination.ToArray());
    }

    /// <summary>
    ///     Proves that an already-canceled token surfaces as an
    ///     <see cref="OperationCanceledException"/> and writes nothing to the destination.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/model.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(CreatePayload(sizeBytes: 16)));

        using var client = new HttpModelDownloadClient();
        using var destination = new MemoryStream();
        var sourceUri = new Uri($"{server.Urls[0]}/model.bin");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var exception = await Record.ExceptionAsync(
            () => client.DownloadAsync(sourceUri, destination, null, cancellation.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Equal(0, destination.Length);
    }

    /// <summary>
    ///     Proves that a non-2xx response from the server surfaces as an
    ///     <see cref="HttpRequestException"/> rather than a silently truncated download.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadAsync_NonSuccessResponse_ThrowsHttpRequestException()
    {
        // Arrange: a stubbed server that always responds 404 Not Found
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/missing.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        using var client = new HttpModelDownloadClient();
        using var destination = new MemoryStream();
        var sourceUri = new Uri($"{server.Urls[0]}/missing.bin");

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.DownloadAsync(sourceUri, destination, null, CancellationToken.None));
    }

    /// <summary>
    ///     Proves that, with no mirror configured, no <c>Authorization</c> header is sent at all -
    ///     the exact pre-mirror request shape, confirming mirror authentication support is fully
    ///     opt-in.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadAsync_NoMirrorAuth_SendsNoAuthorizationHeader()
    {
        // Arrange
        var payload = CreatePayload(sizeBytes: 16);
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/model.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload).WithHeader("Content-Length", payload.Length.ToString()));

        using var client = new HttpModelDownloadClient();
        using var destination = new MemoryStream();
        var sourceUri = new Uri($"{server.Urls[0]}/model.bin");

        // Act
        await client.DownloadAsync(sourceUri, destination, null, CancellationToken.None);

        // Assert
        var request = Assert.Single(server.LogEntries);
        var requestMessage = request.RequestMessage;
        Assert.NotNull(requestMessage);
        Assert.NotNull(requestMessage.Headers);
        Assert.False(requestMessage.Headers.ContainsKey("Authorization"));
    }

    /// <summary>
    ///     Proves that a <see cref="HttpModelDownloadClient"/> constructed with a
    ///     <see cref="DownloadMirror"/> declaring <see cref="DownloadMirror.BearerToken"/> causes
    ///     every request it issues to carry a Bearer <c>Authorization</c> header.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadAsync_BearerTokenMirror_SendsBearerAuthorizationHeader()
    {
        // Arrange
        var payload = CreatePayload(sizeBytes: 16);
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/model.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload).WithHeader("Content-Length", payload.Length.ToString()));

        var sourceUri = new Uri($"{server.Urls[0]}/model.bin");
        var mirror = new DownloadMirror(sourceUri, bearerToken: "secret-token");
        using var client = new HttpModelDownloadClient(httpClient: null, mirror);
        using var destination = new MemoryStream();

        // Act
        await client.DownloadAsync(sourceUri, destination, null, CancellationToken.None);

        // Assert
        var request = Assert.Single(server.LogEntries);
        var requestMessage = request.RequestMessage;
        Assert.NotNull(requestMessage);
        Assert.NotNull(requestMessage.Headers);
        Assert.Equal("Bearer secret-token", requestMessage.Headers["Authorization"].Single());
        Assert.Equal(payload, destination.ToArray());
    }

    /// <summary>
    ///     Proves that a <see cref="HttpModelDownloadClient"/> constructed with a
    ///     <see cref="DownloadMirror"/> declaring <see cref="DownloadMirror.Credentials"/> sends a
    ///     preemptive HTTP Basic <c>Authorization</c> header carrying the exact configured
    ///     username/password on every request, through the same client instance - never a
    ///     second, separately configured one.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadAsync_CredentialsMirror_SendsBasicAuthorizationHeader()
    {
        // Arrange
        var payload = CreatePayload(sizeBytes: 16);
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/model.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload).WithHeader("Content-Length", payload.Length.ToString()));

        var sourceUri = new Uri($"{server.Urls[0]}/model.bin");
        var mirror = new DownloadMirror(sourceUri, credentials: new NetworkCredential("mirror-user", "mirror-pass"));
        using var client = new HttpModelDownloadClient(httpClient: null, mirror);
        using var destination = new MemoryStream();

        // Act
        await client.DownloadAsync(sourceUri, destination, null, CancellationToken.None);

        // Assert: the request carried the expected Basic credentials, and the real bytes were
        // downloaded.
        var request = Assert.Single(server.LogEntries);
        var requestMessage = request.RequestMessage;
        Assert.NotNull(requestMessage);
        Assert.NotNull(requestMessage.Headers);
        var authorizationHeader = requestMessage.Headers["Authorization"].Single();
        Assert.NotNull(authorizationHeader);
        Assert.StartsWith("Basic ", authorizationHeader, StringComparison.Ordinal);
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorizationHeader["Basic ".Length..]));
        Assert.Equal("mirror-user:mirror-pass", decoded);
        Assert.Equal(payload, destination.ToArray());
    }

    /// <summary>
    ///     Proves that a <see cref="HttpModelDownloadClient"/> constructed with a credentialed
    ///     <see cref="DownloadMirror"/> never sends the mirror's <c>Authorization</c> header to a
    ///     <see cref="HttpModelDownloadClient.DownloadAsync"/> URI outside the mirror's own <see cref="DownloadMirror.BaseUri"/>
    ///     - even one on the exact same host - since a caller can hold this public type directly
    ///     (bypassing <see cref="SpeechModelDownloader"/>'s own URI rewriting) and ask it to fetch
    ///     an unrelated file.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadAsync_NonMirrorUriWithMirrorConfigured_SendsNoAuthorizationHeader()
    {
        // Arrange: the mirror is scoped to "/mirror", but the request targets a sibling path
        // "/mirror-other" that merely shares a literal string prefix with the mirror's base path.
        var payload = CreatePayload(sizeBytes: 16);
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/mirror-other/model.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload).WithHeader("Content-Length", payload.Length.ToString()));

        var mirrorBaseUri = new Uri($"{server.Urls[0]}/mirror");
        var mirror = new DownloadMirror(mirrorBaseUri, bearerToken: "secret-token");
        using var client = new HttpModelDownloadClient(httpClient: null, mirror);
        using var destination = new MemoryStream();
        var nonMirrorUri = new Uri($"{server.Urls[0]}/mirror-other/model.bin");

        // Act
        await client.DownloadAsync(nonMirrorUri, destination, null, CancellationToken.None);

        // Assert
        var request = Assert.Single(server.LogEntries);
        var requestMessage = request.RequestMessage;
        Assert.NotNull(requestMessage);
        Assert.NotNull(requestMessage.Headers);
        Assert.False(requestMessage.Headers.ContainsKey("Authorization"));
        Assert.Equal(payload, destination.ToArray());
    }

    /// <summary>
    ///     Proves that a <see cref="DownloadMirror"/> configured with a trailing-slash
    ///     <see cref="DownloadMirror.BaseUri"/> (e.g. <c>https://mirror/models/</c>) still applies
    ///     its authentication to a request targeting the exact same path spelled without the
    ///     trailing slash (<c>https://mirror/models</c>), and vice versa - both spellings denote
    ///     the same base resource, so the mirror-scope check must not require the two paths to be
    ///     byte-for-byte identical.
    /// </summary>
    [Theory]
    [InlineData("/mirror/", "/mirror")]
    [InlineData("/mirror", "/mirror/")]
    public async Task HttpModelDownloadClient_DownloadAsync_MirrorBaseUriTrailingSlashMismatch_SendsAuthorizationHeader(
        string mirrorBasePath, string requestPath)
    {
        // Arrange
        var payload = CreatePayload(sizeBytes: 16);
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath(requestPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload).WithHeader("Content-Length", payload.Length.ToString()));

        var mirrorBaseUri = new Uri($"{server.Urls[0]}{mirrorBasePath}");
        var mirror = new DownloadMirror(mirrorBaseUri, bearerToken: "secret-token");
        using var client = new HttpModelDownloadClient(httpClient: null, mirror);
        using var destination = new MemoryStream();
        var requestUri = new Uri($"{server.Urls[0]}{requestPath}");

        // Act
        await client.DownloadAsync(requestUri, destination, null, CancellationToken.None);

        // Assert
        var request = Assert.Single(server.LogEntries);
        var requestMessage = request.RequestMessage;
        Assert.NotNull(requestMessage);
        Assert.NotNull(requestMessage.Headers);
        var authorizationHeader = requestMessage.Headers["Authorization"].Single();
        Assert.Equal("Bearer secret-token", authorizationHeader);
        Assert.Equal(payload, destination.ToArray());
    }

    /// <summary>
    ///     Proves that <see cref="HttpClient"/>'s default handler strips the mirror's
    ///     <c>Authorization</c> header from the follow-up request when the initial request
    ///     receives an HTTP redirect, even when the redirect target is still within the
    ///     configured mirror's scope - the documented guarantee
    ///     <see cref="HttpModelDownloadClient"/>'s own remarks rely on to justify never
    ///     re-applying mirror authentication to a redirected request.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadAsync_MirrorRedirect_StripsAuthorizationHeaderOnRedirectedRequest()
    {
        // Arrange
        var payload = CreatePayload(sizeBytes: 16);
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/mirror/model.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(302).WithHeader("Location", "/mirror/redirected.bin"));
        server
            .Given(Request.Create().WithPath("/mirror/redirected.bin").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(payload).WithHeader("Content-Length", payload.Length.ToString()));

        var mirrorBaseUri = new Uri($"{server.Urls[0]}/mirror");
        var mirror = new DownloadMirror(mirrorBaseUri, bearerToken: "secret-token");
        using var client = new HttpModelDownloadClient(httpClient: null, mirror);
        using var destination = new MemoryStream();
        var sourceUri = new Uri($"{server.Urls[0]}/mirror/model.bin");

        // Act
        await client.DownloadAsync(sourceUri, destination, null, CancellationToken.None);

        // Assert
        Assert.Equal(2, server.LogEntries.Count());
        var initialRequestMessage = server.LogEntries.First(entry => entry.RequestMessage!.Path == "/mirror/model.bin").RequestMessage;
        var redirectedRequestMessage = server.LogEntries.First(entry => entry.RequestMessage!.Path == "/mirror/redirected.bin").RequestMessage;
        Assert.NotNull(initialRequestMessage);
        Assert.NotNull(redirectedRequestMessage);
        Assert.NotNull(initialRequestMessage.Headers);
        Assert.NotNull(redirectedRequestMessage.Headers);
        var expectedAuthorization = new AuthenticationHeaderValue("Bearer", "secret-token").ToString();
        Assert.Equal(expectedAuthorization, initialRequestMessage.Headers["Authorization"].Single());
        Assert.False(redirectedRequestMessage.Headers.ContainsKey("Authorization"));
        Assert.Equal(payload, destination.ToArray());
    }

    /// <summary>
    ///     Proves that a resume request asks for the remaining bytes with a <c>Range</c> header and, on a matching
    ///     <c>206 Partial Content</c>, appends them after the bytes already held, reporting progress that counts the
    ///     held bytes against the complete length.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadFromOffsetAsync_PartialContent_Appends()
    {
        // Arrange: the destination already holds the first 10 bytes
        var payload = CreatePayload(sizeBytes: 64);
        var handler = new FakeRangeHttpMessageHandler(payload);
        using var httpClient = new HttpClient(handler);
        using var client = new HttpModelDownloadClient(httpClient);
        using var destination = new MemoryStream();
        await destination.WriteAsync(payload.AsMemory(0, 10), CancellationToken.None);
        var reports = new List<SpeechModelDownloadProgress>();

        // Act
        await client.DownloadFromOffsetAsync(
            new Uri("https://example.invalid/model.bin"), destination, 10, new SynchronousProgress<SpeechModelDownloadProgress>(reports.Add), CancellationToken.None);

        // Assert
        Assert.Equal(payload, destination.ToArray());
        var range = Assert.Single(handler.ReceivedRanges);
        Assert.Equal("bytes=10-", range?.ToString());
        Assert.Equal(10, reports[0].BytesTransferred);
        Assert.Equal(payload.Length, reports[^1].BytesTransferred);
        Assert.All(reports, report => Assert.Equal(payload.Length, report.TotalBytes));
    }

    /// <summary>
    ///     Proves that a server that ignores <c>Range</c> and answers <c>200 OK</c> makes the client discard the held
    ///     bytes and write the whole body.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadFromOffsetAsync_FullContent_Rewrites()
    {
        // Arrange: the held bytes are stale garbage the full body must replace
        var payload = CreatePayload(sizeBytes: 64);
        var handler = new FakeRangeHttpMessageHandler(payload) { IgnoreRange = true };
        using var httpClient = new HttpClient(handler);
        using var client = new HttpModelDownloadClient(httpClient);
        using var destination = new MemoryStream();
        await destination.WriteAsync(new byte[100], CancellationToken.None);

        // Act
        await client.DownloadFromOffsetAsync(new Uri("https://example.invalid/model.bin"), destination, 100, null, CancellationToken.None);

        // Assert
        Assert.Equal(payload, destination.ToArray());
        Assert.Single(handler.ReceivedRanges);
    }

    /// <summary>
    ///     Proves that a <c>416 Range Not Satisfiable</c> answer makes the client truncate the held bytes and download
    ///     the file once more from the start without <c>Range</c>.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadFromOffsetAsync_RangeNotSatisfiable_Restarts()
    {
        // Arrange: more bytes are held than the file has, so the range cannot be satisfied
        var payload = CreatePayload(sizeBytes: 64);
        var handler = new FakeRangeHttpMessageHandler(payload);
        using var httpClient = new HttpClient(handler);
        using var client = new HttpModelDownloadClient(httpClient);
        using var destination = new MemoryStream();
        await destination.WriteAsync(new byte[80], CancellationToken.None);

        // Act
        await client.DownloadFromOffsetAsync(new Uri("https://example.invalid/model.bin"), destination, 80, null, CancellationToken.None);

        // Assert: one ranged request, then exactly one plain request
        Assert.Equal(payload, destination.ToArray());
        Assert.Equal(2, handler.ReceivedRanges.Count);
        Assert.NotNull(handler.ReceivedRanges[0]);
        Assert.Null(handler.ReceivedRanges[1]);
    }

    /// <summary>
    ///     Proves that a <c>206</c> answer for a different range than requested makes the client truncate the held
    ///     bytes and download the file once more from the start without <c>Range</c>.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadFromOffsetAsync_ContentRangeMismatch_Restarts()
    {
        // Arrange: the first answer is a 206 starting at byte 0 instead of the requested byte 10
        var payload = CreatePayload(sizeBytes: 64);
        var requests = new List<RangeHeaderValue?>();
        var handler = new DelegatingStubHandler(request =>
        {
            requests.Add(request.Headers.Range);
            if (request.Headers.Range is not null)
            {
                var mismatched = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(payload) };
                mismatched.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, payload.Length - 1, payload.Length);
                return mismatched;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
        });
        using var httpClient = new HttpClient(handler);
        using var client = new HttpModelDownloadClient(httpClient);
        using var destination = new MemoryStream();
        await destination.WriteAsync(payload.AsMemory(0, 10), CancellationToken.None);

        // Act
        await client.DownloadFromOffsetAsync(new Uri("https://example.invalid/model.bin"), destination, 10, null, CancellationToken.None);

        // Assert
        Assert.Equal(payload, destination.ToArray());
        Assert.Equal(2, requests.Count);
        Assert.Null(requests[1]);
    }

    /// <summary>
    ///     Proves that a fresh download (offset zero) sends no <c>Range</c> header.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadFromOffsetAsync_ZeroOffset_SendsNoRangeHeader()
    {
        // Arrange
        var payload = CreatePayload(sizeBytes: 32);
        var handler = new FakeRangeHttpMessageHandler(payload);
        using var httpClient = new HttpClient(handler);
        using var client = new HttpModelDownloadClient(httpClient);
        using var destination = new MemoryStream();

        // Act
        await client.DownloadFromOffsetAsync(new Uri("https://example.invalid/model.bin"), destination, 0, null, CancellationToken.None);

        // Assert
        Assert.Equal(payload, destination.ToArray());
        Assert.Null(Assert.Single(handler.ReceivedRanges));
    }

    /// <summary>
    ///     Proves that an error answer to a resume request throws <see cref="HttpRequestException"/> with the status,
    ///     exactly as a plain download does, and leaves the held bytes in place for a later attempt.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadFromOffsetAsync_NotFound_ThrowsAndKeepsHeldBytes()
    {
        // Arrange
        var handler = new DelegatingStubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var httpClient = new HttpClient(handler);
        using var client = new HttpModelDownloadClient(httpClient);
        using var destination = new MemoryStream();
        await destination.WriteAsync(new byte[10], CancellationToken.None);

        // Act
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.DownloadFromOffsetAsync(new Uri("https://example.invalid/model.bin"), destination, 10, null, CancellationToken.None));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(10, destination.Length);
    }

    /// <summary>
    ///     Proves that a resume request to the configured mirror carries the mirror's authentication along with the
    ///     <c>Range</c> header.
    /// </summary>
    [Fact]
    public async Task HttpModelDownloadClient_DownloadFromOffsetAsync_MirrorUri_SendsAuthorizationWithRange()
    {
        // Arrange
        var payload = CreatePayload(sizeBytes: 32);
        HttpRequestMessage? received = null;
        var handler = new DelegatingStubHandler(request =>
        {
            received = request;
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(payload[4..]) };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(4, payload.Length - 1, payload.Length);
            return response;
        });
        using var httpClient = new HttpClient(handler);
        var mirror = new DownloadMirror(new Uri("https://mirror.invalid/models"), bearerToken: "secret-token");
        using var client = new HttpModelDownloadClient(httpClient, mirror);
        using var destination = new MemoryStream();
        await destination.WriteAsync(payload.AsMemory(0, 4), CancellationToken.None);

        // Act
        await client.DownloadFromOffsetAsync(new Uri("https://mirror.invalid/models/m/model.bin"), destination, 4, null, CancellationToken.None);

        // Assert
        Assert.NotNull(received);
        Assert.Equal("Bearer secret-token", received.Headers.Authorization?.ToString());
        Assert.Equal("bytes=4-", received.Headers.Range?.ToString());
        Assert.Equal(payload, destination.ToArray());
    }

    /// <summary>
    ///     A test <see cref="HttpMessageHandler"/> that answers every request with a caller-supplied function.
    /// </summary>
    /// <param name="respond">Produces the response for a request.</param>
    private sealed class DelegatingStubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    /// <summary>
    ///     Builds a random, non-repeating payload of the given size so a truncated or
    ///     corrupted download is reliably detectable.
    /// </summary>
    private static byte[] CreatePayload(int sizeBytes)
    {
        // Cryptographically random bytes are a convenient way to generate a large, non-repeating
        // byte pattern without a real model file.
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
}
