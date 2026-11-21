using System.Globalization;
using System.Net;
using System.Text;
using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

/// <summary>
///     A temporary repository layout for the <c>cluster</c> command: a valid data folder at <c>data/text-en</c>, tiny
///     in-code books served by a fake mirror that counts its requests, and a fake pause that records every wait.
/// </summary>
/// <remarks>
///     Nothing touches the network: the fetcher answers from an in-memory map of URLs and answers an unknown URL with
///     HTTP 404. The books are generated from fixed word lists so every run sees the same corpus. Disposal deletes the
///     folder.
/// </remarks>
internal sealed class ClusteringFixture : IDisposable
{
    /// <summary>The allowed mirror the fixture's URLs use.</summary>
    public const string Host = "gutenberg.pglaf.org";

    /// <summary>The e-book numbers of the fixture's three books.</summary>
    public static readonly int[] Numbers = [101, 202, 303];

    /// <summary>Animal words of the generated text.</summary>
    private static readonly string[] Animals = ["dog", "cat", "horse", "cow", "sheep", "goat"];

    /// <summary>Verb words of the generated text.</summary>
    private static readonly string[] Verbs = ["runs", "eats", "sleeps", "jumps", "walks"];

    /// <summary>Place words of the generated text.</summary>
    private static readonly string[] Places = ["field", "barn", "garden", "river", "hill"];

    /// <summary>
    ///     Creates the fixture with the three books on the fake mirror and a source list without pins.
    /// </summary>
    public ClusteringFixture()
    {
        Data = new DatasetFixture(Path.Combine("data", "text-en"));
        foreach (var number in Numbers)
        {
            AddBook(number, BookBody(number));
        }

        WriteSources(Numbers.Select(static number => (number, "used", (string?)null, (long?)null)).ToArray());
    }

    /// <summary>Gets the data fixture; its root is <c>data/text-en</c> below <see cref="BaseDirectory"/>.</summary>
    public DatasetFixture Data { get; }

    /// <summary>Gets the folder that plays the repository.</summary>
    public string BaseDirectory => Data.BaseDirectory;

    /// <summary>Gets the default cache folder.</summary>
    public string CacheDirectory => Path.Combine(Data.Root, "external", "cluster-corpus");

    /// <summary>Gets the files the fake mirror serves.</summary>
    public Dictionary<Uri, byte[]> Mirror { get; } = [];

    /// <summary>Gets the URLs requested so far, in order.</summary>
    public List<Uri> Requests { get; } = [];

    /// <summary>Gets the pauses requested so far, in order.</summary>
    public List<TimeSpan> Sleeps { get; } = [];

    /// <summary>Gets or sets the status the fake mirror answers every request with, or null to serve the files.</summary>
    public HttpStatusCode? FailWith { get; set; }

    /// <summary>Gets the seams for the command.</summary>
    public ClusterEnvironment Environment => new(Fetch, Sleeps.Add);

    /// <summary>
    ///     Gets the text URL of a book.
    /// </summary>
    /// <param name="number">The e-book number.</param>
    /// <returns>The URL on the fixture's mirror.</returns>
    public static Uri TextUrl(int number) => new($"https://{Host}/cache/epub/{number}/pg{number}.txt");

    /// <summary>
    ///     Gets the metadata URL of a book.
    /// </summary>
    /// <param name="number">The e-book number.</param>
    /// <returns>The URL on the fixture's mirror.</returns>
    public static Uri RdfUrl(int number) => new($"https://{Host}/cache/epub/{number}/pg{number}.rdf");

    /// <summary>
    ///     Wraps a body in a downloaded e-book's header and footer.
    /// </summary>
    /// <param name="body">The body text.</param>
    /// <returns>The download bytes with CRLF line endings, as the mirror serves them.</returns>
    public static byte[] RawBook(string body) => Encoding.UTF8.GetBytes(
        ("The Project Gutenberg eBook of A Test Book\n\nThis eBook is for the use of anyone anywhere.\n\n" +
         "*** START OF THE PROJECT GUTENBERG EBOOK A TEST BOOK ***\n\nProduced by A. Volunteer\n\n" +
         body +
         "\n\n*** END OF THE PROJECT GUTENBERG EBOOK A TEST BOOK ***\n\nSection 1. General Terms of Use\n").Replace("\n", "\r\n", StringComparison.Ordinal));

    /// <summary>
    ///     Generates the body of a fixture book: paragraphs of simple sentences over three word groups.
    /// </summary>
    /// <param name="number">The e-book number; it seeds the generator.</param>
    /// <returns>The body text.</returns>
    public static string BookBody(int number)
    {
        var state = (uint)number;
        var builder = new StringBuilder();
        for (var paragraph = 0; paragraph < 6; paragraph++)
        {
            for (var sentence = 0; sentence < 5; sentence++)
            {
                var animal = Pick(Animals, ref state);
                var verb = Pick(Verbs, ref state);
                var place = Pick(Places, ref state);
                builder.Append(CultureInfo.InvariantCulture, $"The {animal} {verb} in the {place}. ");
            }

            builder.Append("\n\n");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    ///     Serves a book on the fake mirror.
    /// </summary>
    /// <param name="number">The e-book number.</param>
    /// <param name="body">The body text.</param>
    public void AddBook(int number, string body)
    {
        Mirror[TextUrl(number)] = RawBook(body);
        Mirror[RdfUrl(number)] = Encoding.UTF8.GetBytes($"<rdf>{number}</rdf>\n");
    }

    /// <summary>
    ///     Writes <c>cluster-sources.tsv</c>.
    /// </summary>
    /// <param name="rows">The rows: e-book number, status and optional pin.</param>
    public void WriteSources(params (int Number, string Status, string? Sha, long? Bytes)[] rows)
    {
        var builder = new StringBuilder(string.Join('\t', ClusterSourceList.Header)).Append('\n');
        foreach (var (number, status, sha, bytes) in rows)
        {
            builder.Append(CultureInfo.InvariantCulture, $"gutenberg-{number}\tcore-open\tA Test Book {number}\tA. Writer (1800-1870)\t1850\t")
                .Append(TextUrl(number)).Append("\tPublic domain in the USA (fixture)\tfixture\t")
                .Append(sha ?? "-").Append('\t').Append(bytes?.ToString(CultureInfo.InvariantCulture) ?? "-").Append('\t').Append(status).Append('\n');
        }

        Data.Write(ClusterSourceList.FileName, builder.ToString());
    }

    /// <summary>
    ///     Runs the <c>cluster</c> command with the repository folder as the current directory.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The exit code, standard output and standard error.</returns>
    public (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = ClusterCommand.Run(args, output, error, BaseDirectory, BaseDirectory, Environment);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>
    ///     Fetches and pins every book, then writes the audit's removals, so the corpus is fetched, pinned and audited.
    /// </summary>
    public void Prepare()
    {
        var fetch = Run("--fetch", "--write-pins", "--delay-ms", "1000");
        var audit = Run("--audit", "--write", "--threads", "2");
        if (fetch.ExitCode != 0 || audit.ExitCode != 0)
        {
            throw new InvalidOperationException($"preparing the fixture failed: {fetch.Output}{fetch.Error}{audit.Output}{audit.Error}");
        }
    }

    /// <inheritdoc />
    public void Dispose() => Data.Dispose();

    /// <summary>
    ///     Picks a word with a small fixed integer pseudo-random generator.
    /// </summary>
    /// <param name="words">The words.</param>
    /// <param name="state">The generator state.</param>
    /// <returns>The word.</returns>
    private static string Pick(string[] words, ref uint state)
    {
        state = unchecked((state * 1664525u) + 1013904223u);
        return words[(state >> 16) % (uint)words.Length];
    }

    /// <summary>
    ///     The fake mirror.
    /// </summary>
    /// <param name="uri">The URL.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>The served bytes.</returns>
    private MemoryStream Fetch(Uri uri, CancellationToken cancellation)
    {
        Requests.Add(uri);
        if (FailWith is { } status)
        {
            throw new HttpRequestException($"HTTP {(int)status}", null, status);
        }

        return Mirror.TryGetValue(uri, out var bytes)
            ? new MemoryStream(bytes, false)
            : throw new HttpRequestException("HTTP 404", null, HttpStatusCode.NotFound);
    }
}
