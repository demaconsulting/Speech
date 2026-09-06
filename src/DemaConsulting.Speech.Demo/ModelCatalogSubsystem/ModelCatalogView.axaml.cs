using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DemaConsulting.Speech.Demo.ModelCatalogSubsystem;

/// <summary>
///     View hosting the model catalog list, download commands, and empty-catalog message.
/// </summary>
/// <remarks>
///     Contains no logic beyond loading its XAML: every behavior it presents lives in
///     <c>ModelCatalogViewModel</c> and <c>ModelListItemViewModel</c>, which is what allows the
///     panel to be verified without starting a UI.
/// </remarks>
public sealed partial class ModelCatalogView : UserControl
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelCatalogView"/> class.
    /// </summary>
    public ModelCatalogView() => InitializeComponent();

    /// <summary>
    ///     Loads the XAML-declared control tree for this view.
    /// </summary>
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
