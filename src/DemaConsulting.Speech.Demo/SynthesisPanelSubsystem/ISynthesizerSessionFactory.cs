using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;

/// <summary>
///     Demo-owned seam over the library's synthesis-session composition surface.
/// </summary>
/// <remarks>
///     The library exposes synthesizer composition through the static
///     <see cref="SpeechSynthesizerFactory.Create(ISynthesisModel,SpeechModelStore,IAudioPlaybackDevice,Diagnostics.ISpeechDiagnostics?,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>
///     method, which requires an <see cref="ISynthesisModel"/> - an interface whose members are
///     partly <see langword="internal"/> to the library, so only the library's own assemblies can
///     implement it. This seam therefore accepts the common <see cref="ISpeechModel"/> contract
///     instead and performs the narrowing itself, which is what lets a demo test substitute a
///     plain, publicly implementable fake model for every scenario (including "wrong role") with
///     no <c>InternalsVisibleTo</c> grant from the library. This method also cannot be
///     substituted directly in a ViewModel unit test because it is static; the production
///     implementation resolves the model's installed-files directory and delegates straight to
///     that factory, while tests substitute a fake that returns a controlled
///     <see cref="ISpeechSynthesizer"/> without a downloaded model, a native runtime, or a real
///     playback device. It adds no public API to <c>DemaConsulting.Speech</c>.
/// </remarks>
public interface ISynthesizerSessionFactory
{
    /// <summary>
    ///     Creates a speech synthesizer for an installed model, playing back through the supplied
    ///     device.
    /// </summary>
    /// <param name="model">The model to load. Must not be <see langword="null"/>.</param>
    /// <param name="playbackDevice">
    ///     The playback device to play synthesized audio through. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag (for example a selected voice, built by
    ///     a host's settings UI from the model's declared <see cref="ISpeechModel.Parameters"/>),
    ///     forwarded unchanged to the library's synthesizer composition, or <see langword="null"/>
    ///     to use the model's own default voice/speaker.
    /// </param>
    /// <returns>
    ///     A real synthesizer when the model is installed, implements the library's synthesis
    ///     role, and the device is available; otherwise an honest <c>IsAvailable == false</c>
    ///     synthesizer.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/> or <paramref name="playbackDevice"/> is
    ///     <see langword="null"/>.
    /// </exception>
    /// <remarks>Never throws; every honest unavailable state is reported through <c>IsAvailable</c>.</remarks>
    ISpeechSynthesizer Create(
        ISpeechModel model,
        IAudioPlaybackDevice playbackDevice,
        IReadOnlyDictionary<string, object>? parameterValues = null);
}
