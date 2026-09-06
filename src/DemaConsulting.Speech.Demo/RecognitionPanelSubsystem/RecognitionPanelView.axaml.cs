using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;

/// <summary>
///     View hosting the speech-to-text panel.
/// </summary>
/// <remarks>
///     Contains no logic beyond loading its XAML: every behavior it presents lives in
///     <see cref="RecognitionPanelViewModel"/>, which is what allows the panel to be verified
///     without starting a UI.
/// </remarks>
public sealed partial class RecognitionPanelView : UserControl
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RecognitionPanelView"/> class.
    /// </summary>
    public RecognitionPanelView() => InitializeComponent();

    /// <summary>
    ///     Loads the XAML-declared control tree for this view.
    /// </summary>
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
