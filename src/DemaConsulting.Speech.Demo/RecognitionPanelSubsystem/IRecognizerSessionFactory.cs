using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;

/// <summary>
///     Demo-owned seam over the library's recognition-engine composition surface.
/// </summary>
/// <remarks>
///     The library exposes engine composition through the static
///     <see cref="SpeechRecognizerFactory.LoadAsync(IRecognitionModel,SpeechModelStore,Diagnostics.ISpeechDiagnostics?,System.Collections.Generic.IReadOnlyDictionary{string,object}?,CancellationToken)"/>
///     method, which requires an <see cref="IRecognitionModel"/> - an interface whose members are
///     partly <see langword="internal"/> to the library, so only the library's own assemblies can
///     implement it. This seam therefore accepts the common <see cref="ISpeechModel"/> contract
///     instead and performs the narrowing itself, which is what lets a demo test substitute a
///     plain, publicly implementable fake model for every scenario (including "wrong role") with
///     no <c>InternalsVisibleTo</c> grant from the library. This method also cannot be
///     substituted directly in a ViewModel unit test because it is static; the production
///     implementation resolves the model's installed-files directory and delegates straight to
///     that factory, while tests substitute a fake that returns a controlled
///     <see cref="ISpeechRecognizerEngine"/> without a downloaded model, a native runtime, or a
///     real capture device. It adds no public API to <c>DemaConsulting.Speech</c>.
///     <para>
///     Only the <em>engine</em> is composed here - a capture device is bound later, per run, via
///     <see cref="ISpeechRecognizerEngine.CreateSessionAsync"/> directly against the returned
///     engine, so a host can load one engine per model and reuse it across many sessions instead
///     of reloading the model on every Start.
///     </para>
/// </remarks>
public interface IRecognizerSessionFactory
{
    /// <summary>
    ///     Loads a speech recognizer engine for an installed model.
    /// </summary>
    /// <param name="model">The model to load. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to observe for cancellation of this call.</param>
    /// <returns>
    ///     A task that completes with a real engine when the model is installed and implements
    ///     the library's recognition role; otherwise an honest <c>IsAvailable == false</c> engine.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is <see langword="null"/>.</exception>
    /// <remarks>Never throws for an ordinary unavailable state; every such state is reported through <c>IsAvailable</c>.</remarks>
    Task<ISpeechRecognizerEngine> LoadAsync(ISpeechModel model, CancellationToken cancellationToken = default);
}
