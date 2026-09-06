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
    /// </remarks>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

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
