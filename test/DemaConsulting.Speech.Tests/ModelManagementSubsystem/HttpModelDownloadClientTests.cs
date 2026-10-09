using System.Net;
using System.Security.Cryptography;
using System.Text;
using DemaConsulting.Speech.ModelManagementSubsystem;
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
        // PostWireMockMiddlewareInit buffers the body and sets Response.ContentLength itself
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
}
