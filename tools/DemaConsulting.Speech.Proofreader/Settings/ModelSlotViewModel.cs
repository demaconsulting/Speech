using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Proofreader.Playback;
using DemaConsulting.Speech.Proofreader.Session;
using DemaConsulting.Speech.Proofreader.Synthesis;

namespace DemaConsulting.Speech.Proofreader.Settings;

/// <summary>
///     One entry of a slot's model dropdown: a registered synthesis model and whether it is installed.
/// </summary>
/// <param name="Descriptor">The catalog descriptor with the current install state.</param>
internal sealed record ModelChoice(SpeechModelDescriptor Descriptor)
{
    /// <summary>Gets the real synthesis model.</summary>
    public ISynthesisModel Model => (ISynthesisModel)Descriptor.Model;

    /// <summary>Gets a value indicating whether the model is installed in the store.</summary>
    public bool IsInstalled => Descriptor.State == SpeechModelState.Downloaded;

    /// <summary>Gets the dropdown label; models that are not installed say so.</summary>
    public string Label => IsInstalled ? Descriptor.DisplayName : $"{Descriptor.DisplayName} (not installed)";
}

/// <summary>
///     Presentation state of one model slot (A or B): which model, with which settings.
/// </summary>
/// <remarks>
///     The slot pairs the model dropdown with the generic settings panel (the same presentation
///     pattern the Demo uses). A slot whose model is not installed is selectable but reports an
///     honest status and cannot play. Not thread-safe; use from the UI thread.
/// </remarks>
internal sealed partial class ModelSlotViewModel : ObservableObject
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelSlotViewModel"/> class.
    /// </summary>
    /// <param name="name">The slot name, <c>A</c> or <c>B</c>.</param>
    public ModelSlotViewModel(string name)
    {
        Name = name;
    }

    /// <summary>Gets the slot name.</summary>
    public string Name { get; }

    /// <summary>Gets the registered synthesis models.</summary>
    public ObservableCollection<ModelChoice> Models { get; } = [];

    /// <summary>Gets the parameter settings of the selected model.</summary>
    public ModelSettingsViewModel Settings { get; } = new();

    /// <summary>Gets or sets the selected model.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    public partial ModelChoice? SelectedModel { get; set; }

    /// <summary>Gets a value indicating whether the slot can play (a model is selected and installed).</summary>
    public bool IsReady => SelectedModel is { IsInstalled: true };

    /// <summary>Gets a one-line explanation of the slot's state.</summary>
    public string StatusText => SelectedModel switch
    {
        null => "No model selected.",
        { IsInstalled: false } choice =>
            $"Not installed (state: {choice.Descriptor.State}). Install it with the Demo or the ModelDownloader tool.",
        _ => "Ready.",
    };

    /// <summary>
    ///     Gets the model and current settings, or <see langword="null"/> when no model is selected.
    /// </summary>
    public ModelSelection? CurrentSelection => SelectedModel is { } choice
        ? new ModelSelection(choice.Descriptor.Id, choice.Descriptor.DisplayName, Settings.BuildValueBag())
        : null;

    /// <summary>
    ///     Describes the slot for the playback controller, read afresh for every sentence.
    /// </summary>
    /// <returns>The target; its problem explains why it cannot play when it cannot.</returns>
    public PlaybackTarget ToTarget()
    {
        if (SelectedModel is not { } choice)
        {
            return new PlaybackTarget(Name, null, null, $"Slot {Name}: no model is selected.");
        }

        return new PlaybackTarget(Name, choice.Model, CurrentSelection, choice.IsInstalled ? null : $"Slot {Name}: {StatusText}");
    }

    /// <summary>
    ///     Replaces the model list (for example after a refresh), keeping the selection and its settings.
    /// </summary>
    /// <param name="descriptors">The registered synthesis models with their current state.</param>
    /// <param name="preferredIndex">
    ///     When nothing was selected before: the index among the installed models to select (clamped),
    ///     so the two slots start with different models when two are installed.
    /// </param>
    public void SetModels(IReadOnlyList<SpeechModelDescriptor> descriptors, int preferredIndex)
    {
        var previousId = SelectedModel?.Descriptor.Id;
        var previousValues = Settings.BuildValueBag();

        Models.Clear();
        foreach (var descriptor in descriptors)
        {
            Models.Add(new ModelChoice(descriptor));
        }

        // Keep the previous model when it is still listed; otherwise prefer an installed one.
        var installed = Models.Where(choice => choice.IsInstalled).ToList();
        SelectedModel = Models.FirstOrDefault(choice => choice.Descriptor.Id == previousId)
            ?? (installed.Count > 0 ? installed[Math.Clamp(preferredIndex, 0, installed.Count - 1)] : Models.FirstOrDefault());
        Settings.RestoreValues(previousValues);
    }

    /// <summary>
    ///     Selects the model and settings saved in a session.
    /// </summary>
    /// <param name="saved">The saved model, or <see langword="null"/> to leave the slot as it is.</param>
    /// <returns><see langword="false"/> when the saved model is not registered in this catalog.</returns>
    public bool Restore(SessionModel? saved)
    {
        if (saved is null)
        {
            return true;
        }

        var choice = Models.FirstOrDefault(candidate => candidate.Descriptor.Id == saved.ModelId);
        if (choice is null)
        {
            return false;
        }

        SelectedModel = choice;
        Settings.RestoreValues(saved.Parameters);
        return true;
    }

    /// <summary>
    ///     Points the settings panel at the newly selected model.
    /// </summary>
    /// <param name="value">The newly selected model.</param>
    partial void OnSelectedModelChanged(ModelChoice? value) => Settings.Model = value?.Descriptor.Model;
}
