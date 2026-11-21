using System.Net;
using System.Text;
using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ClusteringSubsystem;

/// <summary>
///     Unit tests for <see cref="ClusterCorpusCache"/> and <see cref="ClusterSourceList"/>: the source-list rules, the
///     mirror-only fetch with its rate limit and retries, pin verification and the pin writer. The network is a fake.
/// </summary>
public sealed class ClusterCorpusCacheTests
{
    /// <summary>
    ///     Creates a cache over the fixture's default folder and fake mirror.
    /// </summary>
    /// <param name="fixture">The fixture.</param>
    /// <param name="fetcher">A fetcher replacing the fixture's mirror, or null.</param>
    /// <param name="mirror">The mirror host override, or null.</param>
    /// <returns>The cache.</returns>
    private static ClusterCorpusCache CacheOf(ClusteringFixture fixture, Func<Uri, CancellationToken, Stream>? fetcher = null, string? mirror = null) =>
        new(fixture.CacheDirectory, fetcher ?? fixture.Environment.Fetcher, fixture.Sleeps.Add, TimeSpan.FromSeconds(2), mirror);

    /// <summary>
    ///     Loads the fixture's source list.
    /// </summary>
    /// <param name="fixture">The fixture.</param>
    /// <returns>The list.</returns>
    private static ClusterSourceList Sources(ClusteringFixture fixture) =>
        ClusterSourceList.Load(fixture.Data.PathOf(ClusterSourceList.FileName));

    /// <summary>Proves a valid list is parsed with its rows in file order and its used books in ordinal id order.</summary>
    [Fact]
    public void ClusterSourceList_Load_ValidList_ParsesRows()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.WriteSources((9, "used", null, null), (10, "excluded-dev", null, null), (11, "used", null, null));

        // Act
        var list = Sources(fixture);

        // Assert
        Assert.Equal(["gutenberg-9", "gutenberg-10", "gutenberg-11"], list.Sources.Select(static source => source.Id));
        Assert.Equal(["gutenberg-11", "gutenberg-9"], list.UsedInCorpusOrder.Select(static source => source.Id));
        Assert.Equal(GutenbergBody.Sha256Hex(list.Bytes), list.Sha256);
        Assert.False(list.Sources[0].IsPinned);
    }

    /// <summary>Proves a URL on the main web site, a plain-HTTP URL, a pin on a book outside the corpus and unsorted rows are refused.</summary>
    [Fact]
    public void ClusterSourceList_Load_InvalidRows_Throw()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        var path = fixture.Data.PathOf(ClusterSourceList.FileName);
        var valid = File.ReadAllText(path);

        // Act / Assert: the main site, plain HTTP, a pin on an excluded book, and descending numbers
        File.WriteAllText(path, valid.Replace("https://gutenberg.pglaf.org/", "https://www.gutenberg.org/", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => ClusterSourceList.Load(path));
        File.WriteAllText(path, valid.Replace("https://", "http://", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => ClusterSourceList.Load(path));
        fixture.WriteSources((101, "excluded-dev", new string('a', 64), 10));
        Assert.Throws<InvalidDataException>(() => ClusterSourceList.Load(path));
        fixture.WriteSources((202, "used", null, null), (101, "used", null, null));
        Assert.Throws<InvalidDataException>(() => ClusterSourceList.Load(path));
    }

    /// <summary>Proves new pins fill only rows without one and leave every other byte unchanged.</summary>
    [Fact]
    public void ClusterSourceList_WithPins_FillsOnlyUnpinnedRows()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        var pinned = new string('b', 64);
        fixture.WriteSources((101, "used", pinned, 7), (202, "used", null, null));
        var list = Sources(fixture);
        var pins = new Dictionary<string, (string, long)>(StringComparer.Ordinal)
        {
            ["gutenberg-101"] = (new string('c', 64), 9),
            ["gutenberg-202"] = (new string('d', 64), 12),
        };

        // Act
        var text = Encoding.UTF8.GetString(list.WithPins(pins));

        // Assert
        Assert.Contains($"\t{pinned}\t7\tused\n", text, StringComparison.Ordinal);
        Assert.Contains($"\t{new string('d', 64)}\t12\tused\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('c', 64), text, StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetString(list.Bytes).Replace("-\t-\tused", $"{new string('d', 64)}\t12\tused", StringComparison.Ordinal), text);
    }

    /// <summary>Proves unpinned books are fetched (text and metadata) with the delay between requests but not before the first.</summary>
    [Fact]
    public void ClusterCorpusCache_Fetch_UnpinnedBooks_FetchesPoliteAndPins()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        var cache = CacheOf(fixture);

        // Act
        var results = cache.Fetch(Sources(fixture), TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert: two requests per book, each later one after the delay, and a pin per book equal to the extracted body
        Assert.All(results, static result => Assert.Equal("pinned", result.Outcome));
        Assert.Equal(6, fixture.Requests.Count);
        Assert.Equal(6, cache.Requests);
        Assert.Equal(Enumerable.Repeat(TimeSpan.FromSeconds(2), 5), fixture.Sleeps);
        Assert.All(fixture.Requests, static uri => Assert.Equal(ClusteringFixture.Host, uri.Host));
        var expected = GutenbergBody.Extract(ClusteringFixture.RawBook(ClusteringFixture.BookBody(101)));
        Assert.Equal(expected.Sha256, results.Single(static result => result.SourceId == "gutenberg-101").Body!.Sha256);
    }

    /// <summary>Proves a pinned body already in the cache makes no request at all.</summary>
    [Fact]
    public void ClusterCorpusCache_Fetch_PinnedAndCached_MakesNoRequest()
    {
        // Arrange: fetch and pin once
        using var fixture = new ClusteringFixture();
        fixture.Prepare();
        fixture.Requests.Clear();

        // Act
        var results = CacheOf(fixture).Fetch(Sources(fixture), TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(results, static result => Assert.Equal("cached", result.Outcome));
        Assert.Empty(fixture.Requests);
    }

    /// <summary>Proves a tampered cached body is refused when read and restored from the raw download without a request.</summary>
    [Fact]
    public void ClusterCorpusCache_TamperedBody_RefusedThenRestored()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.Prepare();
        var sources = Sources(fixture);
        var cache = CacheOf(fixture);
        File.AppendAllText(cache.BodyPath(sources.Sources[0]), "tampered\n");
        fixture.Requests.Clear();

        // Act / Assert: reading refuses, fetching restores from the raw file
        Assert.Throws<InvalidDataException>(() => cache.ReadBodies(sources));
        var results = cache.Fetch(sources, TextWriter.Null, TestContext.Current.CancellationToken);
        Assert.Equal("cached", results[0].Outcome);
        Assert.Empty(fixture.Requests);
        Assert.Equal(3, cache.ReadBodies(sources).Count);
    }

    /// <summary>Proves a mirror text that no longer matches its pin fails that book and keeps the pin.</summary>
    [Fact]
    public void ClusterCorpusCache_Fetch_MirrorTextDiffersFromPin_Fails()
    {
        // Arrange: a pin that no download matches
        using var fixture = new ClusteringFixture();
        fixture.WriteSources((101, "used", new string('e', 64), 100));

        // Act
        var results = CacheOf(fixture).Fetch(Sources(fixture), TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("failed", results[0].Outcome);
        Assert.Contains("differs from the pin", results[0].Problem, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(fixture.CacheDirectory, "bodies", "gutenberg-101.txt")));
    }

    /// <summary>Proves a refusal (HTTP 429) stops the whole run at once, without retries.</summary>
    [Fact]
    public void ClusterCorpusCache_Fetch_TooManyRequests_StopsRun()
    {
        // Arrange
        using var fixture = new ClusteringFixture { FailWith = HttpStatusCode.TooManyRequests };

        // Act / Assert
        Assert.Throws<IOException>(() => CacheOf(fixture).Fetch(Sources(fixture), TextWriter.Null, TestContext.Current.CancellationToken));
        Assert.Single(fixture.Requests);
    }

    /// <summary>Proves a transient failure is retried after 5 and 15 seconds and then succeeds.</summary>
    [Fact]
    public void ClusterCorpusCache_Fetch_TransientFailure_RetriesWithBackoff()
    {
        // Arrange: the first two requests fail with HTTP 503
        using var fixture = new ClusteringFixture();
        fixture.WriteSources((101, "used", null, null));
        var calls = 0;
        Stream Flaky(Uri uri, CancellationToken token) => ++calls <= 2
            ? throw new HttpRequestException("HTTP 503", null, HttpStatusCode.ServiceUnavailable)
            : new MemoryStream(fixture.Mirror[uri]);

        // Act
        var results = CacheOf(fixture, Flaky).Fetch(Sources(fixture), TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert: delay, backoff 5 s, delay, backoff 15 s, delay, then the text request after the delay
        Assert.Equal("pinned", results[0].Outcome);
        Assert.Equal(4, calls);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)], fixture.Sleeps);
    }

    /// <summary>Proves a cancellation while a response body is being read ends the read at once: no retry, no pause and no file.</summary>
    [Fact]
    public void ClusterCorpusCache_Fetch_CallerCanceledDuringBodyRead_StopsWithoutWritingFile()
    {
        // Arrange: the body sends one chunk, then stalls until the caller cancels during the next read
        using var fixture = new ClusteringFixture();
        fixture.WriteSources((101, "used", null, null));
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        Stream Stalling(Uri uri, CancellationToken token)
        {
            calls++;
            return new StallingBodyStream(cancellation);
        }

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => CacheOf(fixture, Stalling).Fetch(Sources(fixture), TextWriter.Null, cancellation.Token));
        Assert.Equal(1, calls);
        Assert.Empty(fixture.Sleeps);
        Assert.False(File.Exists(Path.Combine(fixture.CacheDirectory, "bodies", "gutenberg-101.txt")));
    }

    /// <summary>Proves a cancellation raised by the fetcher for a canceled caller propagates at once: no retry, no pause and no IOException.</summary>
    [Fact]
    public void ClusterCorpusCache_Fetch_CallerCanceledDuringRequest_PropagatesWithoutRetry()
    {
        // Arrange: the fetcher sees the caller cancel and throws the cancellation the HTTP client would
        using var fixture = new ClusteringFixture();
        fixture.WriteSources((101, "used", null, null));
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        Stream Canceling(Uri uri, CancellationToken token)
        {
            calls++;
            cancellation.Cancel();
            throw new TaskCanceledException("canceled", null, token);
        }

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => CacheOf(fixture, Canceling).Fetch(Sources(fixture), TextWriter.Null, cancellation.Token));
        Assert.Equal(1, calls);
        Assert.Empty(fixture.Sleeps);
    }

    /// <summary>Proves a cancellation during a retry pause stops the run before the next request, and is not turned into an IOException.</summary>
    [Fact]
    public void ClusterCorpusCache_Fetch_CallerCanceledDuringBackoff_StopsBeforeNextRequest()
    {
        // Arrange: every request fails transiently, and the caller cancels while the cache waits to retry
        using var fixture = new ClusteringFixture();
        fixture.WriteSources((101, "used", null, null));
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        Stream Failing(Uri uri, CancellationToken token)
        {
            calls++;
            throw new HttpRequestException("HTTP 503", null, HttpStatusCode.ServiceUnavailable);
        }

        var sleeps = new List<TimeSpan>();
        var cache = new ClusterCorpusCache(fixture.CacheDirectory, Failing, wait => { sleeps.Add(wait); cancellation.Cancel(); }, TimeSpan.FromSeconds(2));

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => cache.Fetch(Sources(fixture), TextWriter.Null, cancellation.Token));
        Assert.Equal(1, calls);
        Assert.Equal([ClusterCorpusCache.RetryWaits[0]], sleeps);
    }

    /// <summary>Proves the default pause (no injected sleeper) waits on the caller's token, so a cancellation ends a 5-second backoff at once.</summary>
    [Fact]
    public void ClusterCorpusCache_Fetch_DefaultPauseCanceled_EndsAtOnce()
    {
        // Arrange: every request fails transiently, and the caller cancels shortly after the backoff starts
        using var fixture = new ClusteringFixture();
        fixture.WriteSources((101, "used", null, null));
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        Stream Failing(Uri uri, CancellationToken token)
        {
            calls++;
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));
            throw new IOException("connection reset");
        }

        var cache = new ClusterCorpusCache(fixture.CacheDirectory, Failing, null, TimeSpan.FromSeconds(2));
        var clock = System.Diagnostics.Stopwatch.StartNew();

        // Act / Assert: well before the first 5-second backoff would have ended
        Assert.ThrowsAny<OperationCanceledException>(() => cache.Fetch(Sources(fixture), TextWriter.Null, cancellation.Token));
        Assert.Equal(1, calls);
        Assert.True(clock.Elapsed < ClusterCorpusCache.RetryWaits[0], $"the pause took {clock.Elapsed}");
    }

    /// <summary>Proves a canceled task without a canceled caller (the HTTP client's timeout) is still retried after the backoff.</summary>
    [Fact]
    public void ClusterCorpusCache_Fetch_TimeoutWithoutCallerCancellation_Retries()
    {
        // Arrange: the first request times out
        using var fixture = new ClusteringFixture();
        fixture.WriteSources((101, "used", null, null));
        var calls = 0;
        Stream TimingOut(Uri uri, CancellationToken token) => ++calls == 1
            ? throw new TaskCanceledException("the request timed out")
            : new MemoryStream(fixture.Mirror[uri]);

        // Act
        var results = CacheOf(fixture, TimingOut).Fetch(Sources(fixture), TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert: the metadata request is retried after 5 s, then the text is fetched
        Assert.Equal("pinned", results[0].Outcome);
        Assert.Equal(3, calls);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)], fixture.Sleeps);
    }

    /// <summary>Proves a missing file (HTTP 404) fails only that book, without retries.</summary>
    [Fact]
    public void ClusterCorpusCache_Fetch_NotFound_FailsBookWithoutRetry()
    {
        // Arrange: the mirror lacks the text of 101
        using var fixture = new ClusteringFixture();
        fixture.Mirror.Remove(ClusteringFixture.TextUrl(101));

        // Act
        var results = CacheOf(fixture).Fetch(Sources(fixture), TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("failed", results.Single(static result => result.SourceId == "gutenberg-101").Outcome);
        Assert.Equal(2, results.Count(static result => result.Outcome == "pinned"));
        Assert.Single(fixture.Requests, static uri => uri == ClusteringFixture.TextUrl(101));
    }

    /// <summary>Proves the mirror option replaces the host and a host off the allow-list is refused.</summary>
    [Fact]
    public void ClusterCorpusCache_Mirror_ReplacesHostAndRefusesOthers()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        fixture.WriteSources((101, "used", null, null));
        var seen = new List<Uri>();
        Stream Record(Uri uri, CancellationToken token)
        {
            seen.Add(uri);
            return new MemoryStream(fixture.Mirror[new UriBuilder(uri) { Host = ClusteringFixture.Host }.Uri]);
        }

        // Act
        CacheOf(fixture, Record, "aleph.gutenberg.org").Fetch(Sources(fixture), TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(seen, static uri => Assert.Equal("aleph.gutenberg.org", uri.Host));
        Assert.Throws<ArgumentException>(() => new ClusterCorpusCache(fixture.CacheDirectory, mirror: "www.gutenberg.org"));
    }

    /// <summary>Proves an unpinned book cannot be read as a verified body.</summary>
    [Fact]
    public void ClusterCorpusCache_ReadBodies_UnpinnedBook_Throws()
    {
        // Arrange
        using var fixture = new ClusteringFixture();
        var cache = CacheOf(fixture);
        cache.Fetch(Sources(fixture), TextWriter.Null, TestContext.Current.CancellationToken);

        // Act / Assert
        var exception = Assert.Throws<InvalidDataException>(() => cache.ReadBodies(Sources(fixture)));
        Assert.Contains("--write-pins", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves the shared client of the mirror fetcher is built on a handler that never follows a redirect.</summary>
    [Fact]
    public void GutenbergMirrorFetcher_CreateHandler_Default_DoesNotFollowRedirects()
    {
        // Act
        using var handler = GutenbergMirrorFetcher.CreateHandler();

        // Assert
        Assert.False(handler.AllowAutoRedirect);
    }

    /// <summary>Proves a success answer returns the body and the request carries the identifying user agent.</summary>
    [Fact]
    public void GutenbergMirrorFetcher_Open_Success_ReturnsBodyWithUserAgent()
    {
        // Arrange
        var url = ClusteringFixture.TextUrl(101);
        var body = Encoding.UTF8.GetBytes("a body\n");
        using var handler = new FakeMirrorHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        using var client = new HttpClient(handler);

        // Act
        using var stream = GutenbergMirrorFetcher.Open(client, url, TestContext.Current.CancellationToken);
        using var read = new MemoryStream();
        stream.CopyTo(read);

        // Assert
        Assert.Equal(body, read.ToArray());
        Assert.Equal([url], handler.Requests);
        Assert.Equal([GutenbergMirrorFetcher.UserAgent], handler.UserAgents);
    }

    /// <summary>Proves a redirect from the mirror to the main web site is refused after one request and never followed.</summary>
    [Fact]
    public void GutenbergMirrorFetcher_Open_RedirectToMainSite_RefusedWithoutFollowing()
    {
        // Arrange: the mirror answers HTTP 302 to the same file on the main web site
        var url = ClusteringFixture.TextUrl(101);
        var mainSite = new Uri("https://www.gutenberg.org/cache/epub/101/pg101.txt");
        using var handler = new FakeMirrorHandler(request => request.RequestUri == url
            ? Redirect(mainSite)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) });
        using var client = new HttpClient(handler);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => GutenbergMirrorFetcher.Open(client, url, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("redirect", exception.Message, StringComparison.Ordinal);
        Assert.Contains("www.gutenberg.org", exception.Message, StringComparison.Ordinal);
        Assert.Equal([url], handler.Requests);
        Assert.DoesNotContain(handler.Requests, static uri => uri.Host == "www.gutenberg.org");
    }

    /// <summary>Proves a mirror that redirects a book's text fails only that book, after one request and without a retry.</summary>
    [Fact]
    public void ClusterCorpusCache_Fetch_MirrorRedirects_FailsBookWithoutRetry()
    {
        // Arrange: the real fetcher over a fake handler that serves the fixture's mirror but redirects the text of 101
        using var fixture = new ClusteringFixture();
        var redirected = ClusteringFixture.TextUrl(101);
        using var handler = new FakeMirrorHandler(request =>
            request.RequestUri == redirected
                ? Redirect(new Uri("https://www.gutenberg.org/cache/epub/101/pg101.txt"))
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(fixture.Mirror[request.RequestUri!]) });
        using var client = new HttpClient(handler);
        var cache = CacheOf(fixture, (uri, token) => GutenbergMirrorFetcher.Open(client, uri, token));

        // Act
        var results = cache.Fetch(Sources(fixture), TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert: 101 failed on the redirect, the others are pinned, and no retry and no request off the mirror happened
        var failed = results.Single(static result => result.SourceId == "gutenberg-101");
        Assert.Equal("failed", failed.Outcome);
        Assert.Contains("redirect", failed.Problem, StringComparison.Ordinal);
        Assert.Equal(2, results.Count(static result => result.Outcome == "pinned"));
        Assert.Single(handler.Requests, uri => uri == redirected);
        Assert.Equal(6, handler.Requests.Count);
        Assert.All(handler.Requests, static uri => Assert.Equal(ClusteringFixture.Host, uri.Host));
        Assert.DoesNotContain(ClusterCorpusCache.RetryWaits[0], fixture.Sleeps);
    }

    /// <summary>
    ///     Creates an HTTP 302 answer.
    /// </summary>
    /// <param name="location">The redirect target.</param>
    /// <returns>The answer.</returns>
    private static HttpResponseMessage Redirect(Uri location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = location;
        return response;
    }

    /// <summary>
    ///     A fake HTTP handler that answers every request through a callback and records the URL and user agent of each.
    /// </summary>
    /// <remarks>
    ///     Like any handler other than the sockets handler it never follows a redirect itself, so a 3xx reaches the
    ///     fetcher unchanged; nothing touches the network.
    /// </remarks>
    /// <param name="answer">Builds the answer to a request.</param>
    private sealed class FakeMirrorHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        /// <summary>Gets the URLs requested so far, in order.</summary>
        public List<Uri> Requests { get; } = [];

        /// <summary>Gets the user agent of each request so far, in order.</summary>
        public List<string> UserAgents { get; } = [];

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            UserAgents.Add(request.Headers.UserAgent.ToString());
            return Task.FromResult(answer(request));
        }
    }

    /// <summary>
    ///     A fake response body that returns one chunk, then cancels the caller and stalls until the read is canceled.
    /// </summary>
    /// <remarks>
    ///     Only the cancelable read path can end the stall; a read that ignores the token would never return.
    /// </remarks>
    /// <param name="caller">The caller's cancellation source, canceled on the second read.</param>
    private sealed class StallingBodyStream(CancellationTokenSource caller) : Stream
    {
        /// <summary>The number of reads so far.</summary>
        private int _reads;

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (++_reads == 1)
            {
                buffer.Span[0] = (byte)'x';
                return 1;
            }

            await caller.CancelAsync();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("the body must be read through the cancelable path");

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
