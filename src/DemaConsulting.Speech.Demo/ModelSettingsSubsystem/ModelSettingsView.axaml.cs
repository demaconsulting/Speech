using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DemaConsulting.Speech.Demo.ModelSettingsSubsystem;

/// <summary>
///     View hosting the generic per-model tunable-parameter settings controls.
/// </summary>
/// <remarks>
///     Contains no logic beyond loading its XAML: every behavior it presents lives in
///     <see cref="ModelSettingsViewModel"/> and its parameter presenters, which is what allows
///     the panel to be verified without starting a UI.
/// </remarks>
public sealed partial class ModelSettingsView : UserControl
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelSettingsView"/> class.
    /// </summary>
    public ModelSettingsView() => InitializeComponent();

    /// <summary>
    ///     Loads the XAML-declared control tree for this view.
    /// </summary>
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
