using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;

/// <summary>
///     View hosting the audio capture/playback device pickers.
/// </summary>
/// <remarks>
///     Contains no logic beyond loading its XAML: every behavior it presents lives in
///     <c>DeviceSelectionViewModel</c>, which is what allows the panel to be verified without
///     starting a UI.
/// </remarks>
public sealed partial class DeviceSelectionView : UserControl
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DeviceSelectionView"/> class.
    /// </summary>
    public DeviceSelectionView() => InitializeComponent();

    /// <summary>
    ///     Loads the XAML-declared control tree for this view.
    /// </summary>
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
