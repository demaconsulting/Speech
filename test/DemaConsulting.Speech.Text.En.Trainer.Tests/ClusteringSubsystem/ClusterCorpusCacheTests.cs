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

    /// <summary>Proves bodies are read from a snapshot only when each matches its pin, and an absent or changed body is refused.</summary>
    [Fact]
    public void ClusterCorpusCache_ReadBodiesFromSnapshot_VerifiesPins()
    {
        // Arrange: a pinned corpus, a good snapshot, one without a book and one with a changed body
        using var fixture = new ClusteringFixture();
        fixture.Prepare();
        var sources = Sources(fixture);
        var bodies = CacheOf(fixture).ReadBodies(sources).Select(static body => (body.Source.Id, Encoding.UTF8.GetBytes(body.Text))).ToList();
        var good = Path.Combine(fixture.BaseDirectory, "good.tar");
        var missing = Path.Combine(fixture.BaseDirectory, "missing.tar");
        var changed = Path.Combine(fixture.BaseDirectory, "changed.tar");
        CorpusSnapshot.Build(good, sources.Bytes, bodies);
        CorpusSnapshot.Build(missing, sources.Bytes, bodies.Skip(1).ToList());
        CorpusSnapshot.Build(changed, sources.Bytes, [(bodies[0].Id, Encoding.UTF8.GetBytes("changed\n")), .. bodies.Skip(1)]);

        // Act
        var read = ClusterCorpusCache.ReadBodiesFromSnapshot(sources, good);

        // Assert
        Assert.Equal(bodies.Select(static body => body.Id), read.Select(static body => body.Source.Id));
        Assert.Equal(Encoding.UTF8.GetString(bodies[1].Item2), read[1].Text);
        Assert.Throws<InvalidDataException>(() => ClusterCorpusCache.ReadBodiesFromSnapshot(sources, missing));
        Assert.Throws<InvalidDataException>(() => ClusterCorpusCache.ReadBodiesFromSnapshot(sources, changed));
    }
}
