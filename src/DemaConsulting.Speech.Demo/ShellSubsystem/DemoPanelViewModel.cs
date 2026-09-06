using CommunityToolkit.Mvvm.ComponentModel;

namespace DemaConsulting.Speech.Demo.ShellSubsystem;

/// <summary>
///     One selectable entry in the demo's navigation shell, pairing a tab caption with the panel
///     ViewModel shown when that entry is selected.
/// </summary>
/// <remarks>
///     The shell deliberately knows nothing about any specific panel: it renders whatever list of
///     entries it is given. That is what lets later phases add the model-settings, synthesis, and
///     recognition panels by appending entries, with no change to the shell itself.
/// </remarks>
public sealed class DemoPanelViewModel
{
    /// <summary>
    ///     Gets the caption shown for this entry in the navigation shell.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Gets the panel ViewModel rendered when this entry is selected.
    /// </summary>
    public ObservableObject Content { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DemoPanelViewModel"/> class.
    /// </summary>
    /// <param name="title">
    ///     The caption shown in the navigation shell. Must not be null or whitespace.
    /// </param>
    /// <param name="content">The panel ViewModel to render. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="title"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="content"/> is <see langword="null"/>.
    /// </exception>
    public DemoPanelViewModel(string title, ObservableObject content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(content);

        Title = title;
        Content = content;
    }
}
