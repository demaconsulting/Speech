using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Demo;

/// <summary>
///     Parses this demo's own small set of launch-time command-line options, mirroring
///     <c>DemaConsulting.Speech.Cli</c>'s <c>--models-dir</c>/<c>--mirror-*</c> global options so
///     a user behind a network policy that blocks a model's public download host (for example
///     <c>huggingface.co</c>) can redirect every model download to an internal mirror the same
///     way with either tool.
/// </summary>
/// <remarks>
///     This demo application has no in-app settings dialog: it is a developer reference
///     application normally started from a shell (<c>dotnet run</c> or a built executable), so
///     launch-time arguments - read once, before <see cref="App.OnFrameworkInitializationCompleted"/>
///     composes the library catalog - are the simplest option that is still discoverable (via
///     <c>--help</c>) and scriptable, rather than a persisted settings file nothing in this demo
///     currently reads or writes.
/// </remarks>
public sealed class AppLaunchOptions
{
    /// <summary>Gets the models directory override supplied via <c>--models-dir</c>, or <see langword="null"/>.</summary>
    public string? ModelsDir { get; private init; }

    /// <summary>Gets the internal download mirror base URL supplied via <c>--mirror-url</c>, or <see langword="null"/>.</summary>
    public string? MirrorUrl { get; private init; }

    /// <summary>Gets the HTTP Basic username supplied via <c>--mirror-user</c>, or <see langword="null"/>.</summary>
    public string? MirrorUser { get; private init; }

    /// <summary>Gets the HTTP Basic password supplied via <c>--mirror-password</c>, or <see langword="null"/>.</summary>
    public string? MirrorPassword { get; private init; }

    /// <summary>Gets the bearer token supplied via <c>--mirror-bearer-token</c>, or <see langword="null"/>.</summary>
    public string? MirrorBearerToken { get; private init; }

    /// <summary>Gets a value indicating whether <c>--help</c> or <c>-h</c> was supplied.</summary>
    public bool Help { get; private init; }

    /// <summary>
    ///     Parses <paramref name="args"/> into a new <see cref="AppLaunchOptions"/>.
    /// </summary>
    /// <param name="args">The process's command-line arguments. Must not be <see langword="null"/>.</param>
    /// <returns>The parsed options.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when an option requiring a value is the last argument, or an unrecognized
    ///     argument is supplied.
    /// </exception>
    public static AppLaunchOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? modelsDir = null;
        string? mirrorUrl = null;
        string? mirrorUser = null;
        string? mirrorPassword = null;
        string? mirrorBearerToken = null;
        var help = false;

        var i = 0;
        while (i < args.Length)
        {
            var arg = args[i++];
            switch (arg)
            {
                case "-h":
                case "-?":
                case "--help":
                    help = true;
                    break;

                case "--models-dir":
                    modelsDir = RequireValue(arg, args, ref i);
                    break;

                case "--mirror-url":
                    mirrorUrl = RequireValue(arg, args, ref i);
                    break;

                case "--mirror-user":
                    mirrorUser = RequireValue(arg, args, ref i);
                    break;

                case "--mirror-password":
                    mirrorPassword = RequireValue(arg, args, ref i);
                    break;

                case "--mirror-bearer-token":
                    mirrorBearerToken = RequireValue(arg, args, ref i);
                    break;

                default:
                    throw new ArgumentException($"Unrecognized argument '{arg}'.", nameof(args));
            }
        }

        return new AppLaunchOptions
        {
            ModelsDir = modelsDir,
            MirrorUrl = mirrorUrl,
            MirrorUser = mirrorUser,
            MirrorPassword = mirrorPassword,
            MirrorBearerToken = mirrorBearerToken,
            Help = help,
        };
    }

    /// <summary>
    ///     Reads the value following a value-taking option, advancing <paramref name="index"/>.
    /// </summary>
    /// <param name="option">The option name, used only for the error message.</param>
    /// <param name="args">The full argument list.</param>
    /// <param name="index">The index of the next unread argument; advanced past the consumed value.</param>
    /// <returns>The option's value.</returns>
    /// <exception cref="ArgumentException">Thrown when no value follows <paramref name="option"/>.</exception>
    private static string RequireValue(string option, string[] args, ref int index)
    {
        if (index >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.", nameof(args));
        }

        return args[index++];
    }

    /// <summary>
    ///     Resolves <see cref="ModelsDir"/> into <see cref="SpeechModelStoreOptions"/> for
    ///     <see cref="SpeechModelCatalog"/>'s constructor.
    /// </summary>
    /// <returns>
    ///     A new <see cref="SpeechModelStoreOptions"/> when <see cref="ModelsDir"/> was supplied;
    ///     otherwise <see langword="null"/> (the library's default per-user storage root).
    /// </returns>
    public SpeechModelStoreOptions? CreateStoreOptions() =>
        string.IsNullOrEmpty(ModelsDir) ? null : new SpeechModelStoreOptions { RootPathOverride = ModelsDir };

    /// <summary>
    ///     Resolves the <c>--mirror-*</c> options into <see cref="SpeechModelDownloaderOptions"/>
    ///     for <see cref="SpeechModelCatalog"/>'s constructor.
    /// </summary>
    /// <returns>
    ///     A new <see cref="SpeechModelDownloaderOptions"/> when <see cref="MirrorUrl"/> was
    ///     supplied; otherwise <see langword="null"/> (every model downloads from its own
    ///     declared public URI).
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <see cref="MirrorUser"/> is supplied without <see cref="MirrorPassword"/>
    ///     (or vice versa), when both Basic credentials and a bearer token are supplied, when
    ///     <see cref="MirrorUrl"/> is not a valid absolute URI, or for any other configuration
    ///     <see cref="DownloadMirror"/>'s own constructor rejects (for example a non-HTTPS mirror
    ///     combined with a credential, on a non-loopback host).
    /// </exception>
    public SpeechModelDownloaderOptions? CreateDownloaderOptions()
    {
        var mirror = MirrorOptionsFactory.Create(MirrorUrl, MirrorUser, MirrorPassword, MirrorBearerToken);
        return mirror is null ? null : new SpeechModelDownloaderOptions { Mirror = mirror };
    }

    /// <summary>
    ///     Prints this demo's launch-option usage to the console, mirroring the CLI's own
    ///     <c>--help</c> presentation for the same option names.
    /// </summary>
    public static void PrintHelp()
    {
        Console.WriteLine("Usage: DemaConsulting.Speech.Demo [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, -?, --help             Display this help message and exit");
        Console.WriteLine("  --models-dir <path>        Override the model store root directory");
        Console.WriteLine("  --mirror-url <url>         Download every model from this internal mirror instead of its public URI");
        Console.WriteLine("  --mirror-user <user>       HTTP Basic username for --mirror-url (requires --mirror-password)");
        Console.WriteLine("  --mirror-password <pass>   HTTP Basic password for --mirror-url (requires --mirror-user)");
        Console.WriteLine("  --mirror-bearer-token <t>  Bearer token for --mirror-url (mutually exclusive with --mirror-user/--mirror-password)");
    }
}
