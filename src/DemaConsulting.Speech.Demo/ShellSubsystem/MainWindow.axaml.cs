using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DemaConsulting.Speech.Demo.ShellSubsystem;

/// <summary>
///     The demo's single top-level window and navigation shell.
/// </summary>
/// <remarks>
///     Contains no logic beyond loading its XAML. Which panels exist, and which one is shown, are
///     decided entirely by <c>MainWindowViewModel</c>, so a later phase can add panels without
///     touching this window.
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow() => InitializeComponent();

    /// <summary>
    ///     Loads the XAML-declared control tree for this window.
    /// </summary>
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
