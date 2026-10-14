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
    /// <param name="url">The mirror's base URL, or <see langword="null"/>/empty/whitespace for no mirror.</param>
    /// <param name="user">The HTTP Basic username, or <see langword="null"/>/empty/whitespace for none.</param>
    /// <param name="password">The HTTP Basic password, or <see langword="null"/>/empty/whitespace for none.</param>
    /// <param name="bearerToken">The bearer token, or <see langword="null"/>/empty/whitespace for none.</param>
    /// <returns>
    ///     A new <see cref="DownloadMirror"/> when <paramref name="url"/> is not blank (null, empty,
    ///     or whitespace-only); otherwise <see langword="null"/> (every model downloads from its
    ///     own declared public URI).
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="user"/>, <paramref name="password"/>, or
    ///     <paramref name="bearerToken"/> is supplied without <paramref name="url"/> (a
    ///     credential with nowhere to apply almost always indicates a missing or misspelled
    ///     mirror URL rather than an intentional no-op), when <paramref name="user"/> is supplied
    ///     without <paramref name="password"/> (or vice versa), when <paramref name="url"/> is
    ///     not a valid absolute URI, or for any other configuration <see cref="DownloadMirror"/>'s
    ///     own constructor rejects (for example a non-HTTPS mirror combined with a credential, on
    ///     a non-loopback host).
    /// </exception>
    public static DownloadMirror? Create(string? url, string? user, string? password, string? bearerToken)
    {
        // A blank field (empty or whitespace-only, for example a cleared text box) is the same as
        // no value for every field.
        user = string.IsNullOrWhiteSpace(user) ? null : user;
        password = string.IsNullOrWhiteSpace(password) ? null : password;
        bearerToken = string.IsNullOrWhiteSpace(bearerToken) ? null : bearerToken;

        if (string.IsNullOrWhiteSpace(url))
        {
            if (user is not null || password is not null || bearerToken is not null)
            {
                throw new ArgumentException(
                    "Mirror user, mirror password, and mirror bearer token (and their " +
                    "DEMASPEECH_MIRROR_* environment variables) require a mirror URL; none of " +
                    "them has any effect without a mirror URL to apply them to.");
            }

            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var baseUri))
        {
            // The rejected value is deliberately not echoed: a mistyped URL can still carry a pasted credential.
            throw new ArgumentException("The mirror URL is not a valid absolute URL.");
        }

        // Checked here rather than left to DownloadMirror, whose message quotes the scheme: a
        // pasted "user:password@host" parses with the user name as its scheme.
        if (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("The mirror URL must use the http or https scheme.");
        }

        if ((user is null) != (password is null))
        {
            throw new ArgumentException("Mirror user and mirror password must both be supplied together.");
        }

        var credentials = user is not null ? new NetworkCredential(user, password) : null;
        return new DownloadMirror(baseUri, credentials, bearerToken);
    }
}
