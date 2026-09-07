using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;

/// <summary>
///     Production <see cref="ISynthesizerSessionFactory"/> implementation backed by the library's
///     <see cref="SpeechModelStore"/> and <see cref="SpeechSynthesizerFactory"/>.
/// </summary>
/// <remarks>
///     This adapter resolves the model's installed-files directory from the shared
///     <see cref="SpeechModelStore"/>, narrows <see cref="ISpeechModel"/> to the
///     <see cref="ISynthesisModel"/> the library's factory requires, and forwards to
///     <see cref="SpeechSynthesizerFactory.Create(ISynthesisModel,SpeechModelStore,IAudioPlaybackDevice,Diagnostics.ISpeechDiagnostics?,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>,
///     inheriting that factory's "nothing throws at composition" contract. A model that declares
///     a role other than synthesis (and therefore is not an <see cref="ISynthesisModel"/>) is an
///     honest unavailable outcome, exactly like a model that is not installed, rather than a
///     defect: a host that lets a user choose an installed model with the wrong role must still
///     get a working, if unavailable, synthesizer back.
/// </remarks>
public sealed class SynthesizerSessionFactory : ISynthesizerSessionFactory
{
    /// <summary>The store used to resolve each model's installed-files directory.</summary>
    private readonly SpeechModelStore _store;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SynthesizerSessionFactory"/> class.
    /// </summary>
    /// <param name="store">The library model store to resolve installed directories from. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    public SynthesizerSessionFactory(SpeechModelStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    /// <inheritdoc/>
    public ISpeechSynthesizer Create(
        ISpeechModel model,
        IAudioPlaybackDevice playbackDevice,
        IReadOnlyDictionary<string, object>? parameterValues = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(playbackDevice);

        if (model is not ISynthesisModel synthesisModel)
        {
            return UnavailableSpeechSynthesizer.Instance;
        }

        return SpeechSynthesizerFactory.Create(synthesisModel, _store, playbackDevice, parameterValues: parameterValues);
    }
}
