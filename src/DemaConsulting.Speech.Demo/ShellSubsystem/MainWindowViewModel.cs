using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.Demo.DeviceSelectionSubsystem;
using DemaConsulting.Speech.Demo.ModelCatalogSubsystem;
using DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;
using DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;

namespace DemaConsulting.Speech.Demo.ShellSubsystem;

/// <summary>
///     Presentation state for the demo's main window: the list of capability panels and which one
///     is currently shown.
/// </summary>
/// <remarks>
///     This ViewModel is the demo's navigation shell. It owns no speech or audio behavior of its
///     own - it composes already-constructed panel ViewModels supplied by the application's
///     composition root - so the window can be created and exercised in tests without an audio
///     backend, an installed model, or a running Avalonia application.
///     <para>
///     The panel list covers this phase's four capabilities: Audio Devices, Model Catalog, Text
///     to Speech, and Speech to Text. The per-model settings panel is not a top-level entry here;
///     it is embedded directly in the Text to Speech and Speech to Text panels.
///     </para>
/// </remarks>
public sealed partial class MainWindowViewModel : ObservableObject
{
    /// <summary>
    ///     Gets the capability panels offered by the demo, in display order.
    /// </summary>
    public ObservableCollection<DemoPanelViewModel> Panels { get; } = [];

    /// <summary>
    ///     Gets or sets the panel currently shown, or <see langword="null"/> when the demo was
    ///     composed with no panels at all.
    /// </summary>
    [ObservableProperty]
    private DemoPanelViewModel? _selectedPanel;

    /// <summary>
    ///     Gets the audio device-selection panel state.
    /// </summary>
    public DeviceSelectionViewModel DeviceSelection { get; }

    /// <summary>
    ///     Gets the model catalog panel state.
    /// </summary>
    public ModelCatalogViewModel ModelCatalog { get; }

    /// <summary>
    ///     Gets the text-to-speech panel state.
    /// </summary>
    public SynthesisPanelViewModel Synthesis { get; }

    /// <summary>
    ///     Gets the speech-to-text panel state.
    /// </summary>
    public RecognitionPanelViewModel Recognition { get; }

    /// <summary>
    ///     Gets the window caption, including the honest statement of what this build demonstrates.
    /// </summary>
    public static string Title => "DemaConsulting.Speech Demo - Devices, Model Catalog, Text to Speech, and Speech to Text";

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="deviceSelection">
    ///     The already-composed device-selection panel state. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="modelCatalog">
    ///     The already-composed model-catalog panel state. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="synthesis">
    ///     The already-composed text-to-speech panel state. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="recognition">
    ///     The already-composed speech-to-text panel state. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when any parameter is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Panels are injected rather than constructed here so the shell has no dependency on the
    ///     library at all, keeping composition in one place (the application entry point) exactly
    ///     as the library itself does with its own factories.
    /// </remarks>
    public MainWindowViewModel(
        DeviceSelectionViewModel deviceSelection,
        ModelCatalogViewModel modelCatalog,
        SynthesisPanelViewModel synthesis,
        RecognitionPanelViewModel recognition)
    {
        ArgumentNullException.ThrowIfNull(deviceSelection);
        ArgumentNullException.ThrowIfNull(modelCatalog);
        ArgumentNullException.ThrowIfNull(synthesis);
        ArgumentNullException.ThrowIfNull(recognition);

        DeviceSelection = deviceSelection;
        ModelCatalog = modelCatalog;
        Synthesis = synthesis;
        Recognition = recognition;

        Panels.Add(new DemoPanelViewModel("Audio Devices", deviceSelection));
        Panels.Add(new DemoPanelViewModel("Model Catalog", modelCatalog));
        Panels.Add(new DemoPanelViewModel("Text to Speech", synthesis));
        Panels.Add(new DemoPanelViewModel("Speech to Text", recognition));

        // Open on the first panel so the window is never shown with a blank content area.
        SelectedPanel = Panels[0];
    }
}
