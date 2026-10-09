using System.Net;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo;

/// <summary>
///     Builds a <see cref="DownloadMirror"/> from raw strings, shared by
///     <see cref="AppLaunchOptions"/> (launch-time <c>--mirror-*</c> arguments) and
///     <see cref="ModelCatalogSubsystem.ModelCatalogViewModel"/> (the in-app mirror-settings
///     panel), so both entry points validate mirror configuration identically instead of
///     maintaining two copies of the same rules.
/// </summary>
internal static class MirrorOptionsFactory
{
    /// <summary>
    ///     Builds a <see cref="DownloadMirror"/> from the given raw values.
    /// </summary>
    /// <param name="url">The mirror's base URL, or <see langword="null"/>/empty for no mirror.</param>
    /// <param name="user">The HTTP Basic username, or <see langword="null"/>.</param>
    /// <param name="password">The HTTP Basic password, or <see langword="null"/>.</param>
    /// <param name="bearerToken">The bearer token, or <see langword="null"/>.</param>
    /// <returns>
    ///     A new <see cref="DownloadMirror"/> when <paramref name="url"/> was supplied; otherwise
    ///     <see langword="null"/> (every model downloads from its own declared public URI).
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="user"/> is supplied without <paramref name="password"/>
    ///     (or vice versa), when <paramref name="url"/> is not a valid absolute URI, or for any
    ///     other configuration <see cref="DownloadMirror"/>'s own constructor rejects (for
    ///     example a non-HTTPS mirror combined with a credential, on a non-loopback host).
    /// </exception>
    public static DownloadMirror? Create(string? url, string? user, string? password, string? bearerToken)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var baseUri))
        {
            throw new ArgumentException($"Mirror URL '{url}' is not a valid absolute URL.");
        }

        if (user is null != password is null)
        {
            throw new ArgumentException("Mirror user and mirror password must both be supplied together.");
        }

        var credentials = user is not null ? new NetworkCredential(user, password) : null;
        return new DownloadMirror(baseUri, credentials, bearerToken);
    }
}
