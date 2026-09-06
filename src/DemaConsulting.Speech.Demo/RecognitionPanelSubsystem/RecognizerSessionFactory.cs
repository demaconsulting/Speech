using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;

/// <summary>
///     Production <see cref="IRecognizerSessionFactory"/> implementation backed by the library's
///     <see cref="SpeechModelStore"/> and <see cref="SpeechRecognizerFactory"/>.
/// </summary>
/// <remarks>
///     This adapter resolves the model's installed-files directory from the shared
///     <see cref="SpeechModelStore"/>, narrows <see cref="ISpeechModel"/> to the
///     <see cref="IRecognitionModel"/> the library's factory requires, and forwards to
///     <see cref="SpeechRecognizerFactory.Create(IRecognitionModel,string,IAudioCaptureDevice,Diagnostics.ISpeechDiagnostics?)"/>,
///     inheriting that factory's "nothing throws at composition" contract. A model that declares
///     a role other than recognition (and therefore is not an <see cref="IRecognitionModel"/>) is
///     an honest unavailable outcome, exactly like a model that is not installed, rather than a
///     defect: a host that lets a user choose an installed model with the wrong role must still
///     get a working, if unavailable, recognizer back.
/// </remarks>
public sealed class RecognizerSessionFactory : IRecognizerSessionFactory
{
    /// <summary>The store used to resolve each model's installed-files directory.</summary>
    private readonly SpeechModelStore _store;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RecognizerSessionFactory"/> class.
    /// </summary>
    /// <param name="store">The library model store to resolve installed directories from. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    public RecognizerSessionFactory(SpeechModelStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    /// <inheritdoc/>
    public ISpeechRecognizer Create(ISpeechModel model, IAudioCaptureDevice captureDevice)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(captureDevice);

        if (model is not IRecognitionModel recognitionModel)
        {
            return UnavailableSpeechRecognizer.Instance;
        }

        var installedModelDirectory = _store.GetCurrentDirectory(model.Id);
        return SpeechRecognizerFactory.Create(recognitionModel, installedModelDirectory, captureDevice);
    }
}
