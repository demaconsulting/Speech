using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;

/// <summary>
///     Demo-owned seam over the library's synthesis-engine composition surface.
/// </summary>
/// <remarks>
///     The library exposes engine composition through the static
///     <see cref="SpeechSynthesizerFactory.LoadAsync(ISynthesisModel,SpeechModelStore,Diagnostics.ISpeechDiagnostics?,System.Collections.Generic.IReadOnlyDictionary{string,object}?,CancellationToken)"/>
///     method, which requires an <see cref="ISynthesisModel"/> - an interface whose members are
///     partly <see langword="internal"/> to the library, so only the library's own assemblies can
///     implement it. This seam therefore accepts the common <see cref="ISpeechModel"/> contract
///     instead and performs the narrowing itself, which is what lets a demo test substitute a
///     plain, publicly implementable fake model for every scenario (including "wrong role") with
///     no <c>InternalsVisibleTo</c> grant from the library. This method also cannot be
///     substituted directly in a ViewModel unit test because it is static; the production
///     implementation supplies the shared <see cref="SpeechModelStore"/> itself and delegates
///     straight to that factory, which resolves the model's installed-files directory
///     internally, while tests substitute a fake that returns a controlled
///     <see cref="ISpeechSynthesizerEngine"/> without a downloaded model, a native runtime, or a
///     real playback device. It adds no public API to <c>DemaConsulting.Speech</c>.
///     <para>
///     Only the <em>engine</em> is composed here - a playback device is bound later, per run, via
///     <see cref="ISpeechSynthesizerEngine.CreateSessionAsync"/> directly against the returned
///     engine, so a host can load one engine per model/parameter combination and reuse it across
///     many sessions instead of reloading the model on every Play.
///     </para>
/// </remarks>
public interface ISynthesizerSessionFactory
{
    /// <summary>
    ///     Loads a speech synthesizer engine for an installed model.
    /// </summary>
    /// <param name="model">The model to load. Must not be <see langword="null"/>.</param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag (for example a selected voice, built by
    ///     a host's settings UI from the model's declared <see cref="ISpeechModel.Parameters"/>),
    ///     forwarded unchanged to the library's engine composition, or <see langword="null"/> to
    ///     use the model's own default voice/speaker.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation of this call.</param>
    /// <returns>
    ///     A task that completes with a real engine when the model is installed and implements
    ///     the library's synthesis role; otherwise an honest <c>IsAvailable == false</c> engine.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is <see langword="null"/>.</exception>
    /// <remarks>Never throws for an ordinary unavailable state; every such state is reported through <c>IsAvailable</c>.</remarks>
    Task<ISpeechSynthesizerEngine> LoadAsync(
        ISpeechModel model,
        IReadOnlyDictionary<string, object>? parameterValues,
        CancellationToken cancellationToken = default);
}
