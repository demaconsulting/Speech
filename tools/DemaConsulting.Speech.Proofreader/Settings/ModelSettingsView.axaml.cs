using Avalonia.Controls;

namespace DemaConsulting.Speech.Proofreader.Settings;

/// <summary>
///     Renders a <see cref="ModelSettingsViewModel"/>: one control per declared model parameter.
/// </summary>
public sealed partial class ModelSettingsView : UserControl
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelSettingsView"/> class.
    /// </summary>
    public ModelSettingsView()
    {
        InitializeComponent();
    }
}
