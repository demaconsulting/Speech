using Avalonia;

namespace DemaConsulting.Speech.Proofreader;

/// <summary>
///     The command-line options of the Proofreader.
/// </summary>
/// <param name="ModelsDir">The model store root override, or <see langword="null"/> for the per-user default.</param>
/// <param name="VoicesDir">The Chatterbox reference-voice directory, or <see langword="null"/> for none.</param>
/// <param name="File">A text, Markdown, or session (<c>.json</c>) file to open at start-up, or <see langword="null"/>.</param>
/// <param name="Help">Whether only the usage text was requested.</param>
internal sealed record LaunchOptions(string? ModelsDir, string? VoicesDir, string? File, bool Help)
{
    /// <summary>The usage text.</summary>
    public const string Usage =
        """
        DemaConsulting.Speech.Proofreader - listen to long-form TTS output sentence by sentence and flag problems.

        Usage: DemaConsulting.Speech.Proofreader [--models-dir <dir>] [--voices-dir <dir>] [file]

          --models-dir <dir>   Model store root (default: the per-user model store shared with the Demo).
          --voices-dir <dir>   Directory of Chatterbox Turbo reference voices.
          file                 A .txt or .md text, or a .json session, to open at start-up.
          --help               Print this text.
        """;

    /// <summary>
    ///     Parses the command line.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentException">Thrown for an unknown option, a missing option value, or a second file.</exception>
    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        string? modelsDir = null;
        string? voicesDir = null;
        string? file = null;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h" or "-?":
                    return new LaunchOptions(null, null, null, true);
                case "--models-dir":
                    modelsDir = RequireValue(args, ref i);
                    break;
                case "--voices-dir":
                    voicesDir = RequireValue(args, ref i);
                    break;
                case var option when option.StartsWith("--", StringComparison.Ordinal):
                    throw new ArgumentException($"Unknown option '{option}'.");
                case var path when file is null:
                    file = path;
                    break;
                default:
                    throw new ArgumentException("Only one file can be opened at start-up.");
            }
        }

        return new LaunchOptions(modelsDir, voicesDir, file, false);
    }

    /// <summary>
    ///     Reads the value that follows an option.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <param name="index">The option's index; advanced to the value's index.</param>
    /// <returns>The value.</returns>
    private static string RequireValue(IReadOnlyList<string> args, ref int index)
    {
        if (index + 1 >= args.Count)
        {
            throw new ArgumentException($"Option '{args[index]}' needs a value.");
        }

        index++;
        return args[index];
    }
}

/// <summary>
///     Process entry point of the Proofreader.
/// </summary>
/// <remarks>
///     Kept to the Avalonia-prescribed minimum plus argument handling: an invalid command line fails
///     fast with a console message, and <c>--help</c> prints the usage and exits without starting
///     Avalonia. <see cref="BuildAvaloniaApp"/> stays callable by design-time tooling.
/// </remarks>
internal static class Program
{
    /// <summary>
    ///     Starts the desktop application.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    [STAThread]
    public static void Main(string[] args)
    {
        LaunchOptions options;
        try
        {
            options = LaunchOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(LaunchOptions.Usage);
            Environment.ExitCode = 1;
            return;
        }

        if (options.Help)
        {
            Console.Out.WriteLine(LaunchOptions.Usage);
            return;
        }

        App.Options = options;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    ///     Configures the Avalonia application for runtime start-up and design-time preview.
    /// </summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
