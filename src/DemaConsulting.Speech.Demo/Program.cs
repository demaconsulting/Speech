using Avalonia;

namespace DemaConsulting.Speech.Demo;

/// <summary>
///     Process entry point for the demo application.
/// </summary>
/// <remarks>
///     Kept to the Avalonia-prescribed minimum: no application logic lives here, because
///     <see cref="BuildAvaloniaApp"/> must remain callable by Avalonia's design-time tooling,
///     which invokes it without ever running <see cref="Main"/>.
/// </remarks>
internal static class Program
{
    /// <summary>
    ///     Starts the Avalonia desktop application.
    /// </summary>
    /// <param name="args">Command-line arguments forwarded to the Avalonia lifetime.</param>
    /// <remarks>
    ///     Marked <c>STAThread</c> because Windows requires single-threaded apartment semantics
    ///     for common dialogs and clipboard interaction. Nothing that touches Avalonia may run
    ///     before <see cref="BuildAvaloniaApp"/>, so initialization order here is deliberate.
    ///     <para>
    ///     This demo's own small <c>--models-dir</c>/<c>--mirror-*</c> launch options (see
    ///     <see cref="AppLaunchOptions"/>) are parsed before Avalonia starts, since an invalid
    ///     option (for example a malformed mirror URL) should fail fast with a clear console
    ///     message rather than surfacing later as an exception from
    ///     <see cref="App.OnFrameworkInitializationCompleted"/> after a window has already begun
    ///     opening. <c>--help</c> is handled the same way, printing usage and exiting without
    ///     ever starting the Avalonia lifetime.
    ///     </para>
    /// </remarks>
    [STAThread]
    public static void Main(string[] args)
    {
        AppLaunchOptions options;
        try
        {
            options = AppLaunchOptions.Parse(args);

            // Validate --mirror-* eagerly here (rather than letting App.OnFrameworkInitializationCompleted
            // discover a configuration error only after Avalonia has begun starting up), so a bad
            // mirror URL/credential combination fails fast with the same clear console message
            // this catch block already produces for a parse error.
            _ = options.CreateDownloaderOptions();
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            AppLaunchOptions.PrintHelp();
            Environment.ExitCode = 1;
            return;
        }

        if (options.Help)
        {
            AppLaunchOptions.PrintHelp();
            return;
        }

        App.LaunchOptions = options;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    ///     Configures the Avalonia application for both runtime start-up and design-time preview.
    /// </summary>
    /// <returns>The configured Avalonia application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
