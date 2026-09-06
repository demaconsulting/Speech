using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;

/// <summary>
///     View hosting the text-to-speech panel.
/// </summary>
/// <remarks>
///     Contains no logic beyond loading its XAML: every behavior it presents lives in
///     <see cref="SynthesisPanelViewModel"/>, which is what allows the panel to be verified
///     without starting a UI.
/// </remarks>
public sealed partial class SynthesisPanelView : UserControl
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SynthesisPanelView"/> class.
    /// </summary>
    public SynthesisPanelView() => InitializeComponent();

    /// <summary>
    ///     Loads the XAML-declared control tree for this view.
    /// </summary>
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
