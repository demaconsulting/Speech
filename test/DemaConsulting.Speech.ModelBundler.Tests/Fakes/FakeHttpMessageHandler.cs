using System.Net;

namespace DemaConsulting.Speech.ModelBundler.Tests.Fakes;

/// <summary>
///     An in-memory HTTP server for download tests: serves registered bodies by URI, answers 404 for anything else,
///     and records every request, so no test reaches the network.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    /// <summary>The registered bodies, by absolute URI.</summary>
    private readonly Dictionary<string, byte[]> _bodies = new(StringComparer.Ordinal);

    /// <summary>Gets the absolute URIs requested, in order.</summary>
    public List<string> Requests { get; } = [];

    /// <summary>Registers the body served for a URI.</summary>
    /// <param name="uri">The URI.</param>
    /// <param name="body">The bytes returned with status 200.</param>
    public void Serve(Uri uri, byte[] body) => _bodies[uri.AbsoluteUri] = body;

    /// <summary>Answers a request from the registered bodies.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>200 with the body, or 404.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!.AbsoluteUri;
        Requests.Add(uri);
        var response = _bodies.TryGetValue(uri, out var body)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
            : new HttpResponseMessage(HttpStatusCode.NotFound);
        return Task.FromResult(response);
    }
}
