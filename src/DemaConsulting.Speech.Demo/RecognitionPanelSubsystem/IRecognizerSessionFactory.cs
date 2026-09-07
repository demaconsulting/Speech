using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;

/// <summary>
///     Demo-owned seam over the library's recognition-session composition surface.
/// </summary>
/// <remarks>
///     The library exposes recognizer composition through the static
///     <see cref="SpeechRecognizerFactory.Create(IRecognitionModel,SpeechModelStore,IAudioCaptureDevice,Diagnostics.ISpeechDiagnostics?)"/>
///     method, which requires an <see cref="IRecognitionModel"/> - an interface whose members are
///     partly <see langword="internal"/> to the library, so only the library's own assemblies can
///     implement it. This seam therefore accepts the common <see cref="ISpeechModel"/> contract
///     instead and performs the narrowing itself, which is what lets a demo test substitute a
///     plain, publicly implementable fake model for every scenario (including "wrong role") with
///     no <c>InternalsVisibleTo</c> grant from the library. This method also cannot be
///     substituted directly in a ViewModel unit test because it is static; the production
///     implementation resolves the model's installed-files directory and delegates straight to
///     that factory, while tests substitute a fake that returns a controlled
///     <see cref="ISpeechRecognizer"/> without a downloaded model, a native runtime, or a real
///     capture device. It adds no public API to <c>DemaConsulting.Speech</c>.
/// </remarks>
public interface IRecognizerSessionFactory
{
    /// <summary>
    ///     Creates a speech recognizer for an installed model, streaming from the supplied
    ///     device.
    /// </summary>
    /// <param name="model">The model to load. Must not be <see langword="null"/>.</param>
    /// <param name="captureDevice">
    ///     The capture device to stream audio from. Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    ///     A real recognizer when the model is installed, implements the library's recognition
    ///     role, and the device is available; otherwise an honest <c>IsAvailable == false</c>
    ///     recognizer.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/> or <paramref name="captureDevice"/> is
    ///     <see langword="null"/>.
    /// </exception>
    /// <remarks>Never throws; every honest unavailable state is reported through <c>IsAvailable</c>.</remarks>
    ISpeechRecognizer Create(ISpeechModel model, IAudioCaptureDevice captureDevice);
}
