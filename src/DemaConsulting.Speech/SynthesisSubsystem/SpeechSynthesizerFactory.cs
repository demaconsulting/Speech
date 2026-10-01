using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Composition entry point for obtaining an <see cref="ISpeechSynthesizerEngine"/> for one
///     installed synthesis model.
/// </summary>
/// <remarks>
///     Hosts call <see cref="LoadAsync(ISynthesisModel,string,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,System.Threading.CancellationToken)"/>,
///     <see cref="LoadAsync(ISynthesisModel,SpeechModelStore,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,System.Threading.CancellationToken)"/>,
///     or <see cref="LoadAsync(ISynthesisModel,SpeechModelCatalog,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,System.Threading.CancellationToken)"/>
///     rather than constructing an engine directly, so all of the "can this machine actually speak
///     right now?" logic lives in one reviewable place. Per this library's "nothing throws at
///     composition" decision this method never throws for an ordinary machine state - a model
///     that is not installed, a model whose role is not synthesis, and a machine missing the
///     sherpa-onnx native runtime all return <see cref="UnavailableSpeechSynthesizerEngine.Instance"/>
///     and are reported through the diagnostics sink. A null argument or an invalid enum value is a
///     programming error rather than a machine state, and still throws; so does cancelling the
///     supplied cancellation token via <see cref="OperationCanceledException"/>.
///     <para>
///     Unlike the former synchronous factory, this type no longer takes a playback device: an
///     engine loaded here can create many sessions over its life, each bound to its own device, via
///     <see cref="ISpeechSynthesizerEngine.CreateSessionAsync"/>.
///     </para>
///     <para>
///     The blocking native model load itself runs on a <see cref="DedicatedWorker"/> rather than
///     the calling thread, so awaiting any <c>LoadAsync</c> overload never blocks a caller's
///     synchronization context.
///     </para>
/// </remarks>
public static class SpeechSynthesizerFactory
{
    /// <summary>The diagnostics category used for every event this factory reports.</summary>
    private const string DiagnosticsCategory = "SynthesisSubsystem";

    /// <summary>
    ///     Loads a speech synthesizer engine for an installed synthesis model.
    /// </summary>
    /// <param name="model">
    ///     The synthesis model to load. Must not be null and must already be installed.
    /// </param>
    /// <param name="installedModelDirectory">
    ///     The absolute path of the directory holding the model's installed files, as returned by
    ///     <c>SpeechModelStore.GetCurrentDirectory(model.Id)</c>. A path that is null, empty, or
    ///     does not exist is treated as "model not installed", not as an error.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural composition, lifecycle, and fault events to, or
    ///     <see langword="null"/> to use <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag (for example a selected voice, built
    ///     from the model's declared <see cref="ISpeechModel.Parameters"/>), forwarded to the
    ///     returned engine and re-resolved via <see cref="ISynthesisModel.ResolveSpeakerId"/> once
    ///     per synthesized segment, or <see langword="null"/> to use every model's own default
    ///     voice/speaker.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the load. Must not already be cancelled.</param>
    /// <returns>
    ///     A real engine when the model is installed, its role is synthesis, and the backend loaded
    ///     successfully; otherwise <see cref="UnavailableSpeechSynthesizerEngine.Instance"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains a value for a parameter
    ///     <paramref name="model"/> declares that is invalid for it (wrong type, out of range,
    ///     non-integral for an integer-only parameter, or an unrecognized choice/boolean value).
    ///     An unrecognized parameter id is not an error - it is reported at
    ///     <see cref="SpeechDiagnosticLevel.Info"/> and silently ignored, preserving this
    ///     library's cross-model settings-dictionary-reuse contract.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public static Task<ISpeechSynthesizerEngine> LoadAsync(
        ISynthesisModel model,
        string installedModelDirectory,
        ISpeechDiagnostics? diagnostics = null,
        IReadOnlyDictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default)
    {
        return LoadAsync(model, installedModelDirectory, diagnostics, new SherpaOnnxSynthesisEngineFactory(), parameterValues, cancellationToken);
    }

    /// <summary>
    ///     Loads a speech synthesizer engine for an installed synthesis model, resolving the
    ///     model's installed-files directory from the supplied model store rather than requiring
    ///     the caller to know anything about the store's on-disk directory layout.
    /// </summary>
    /// <param name="model">
    ///     The synthesis model to load. Must not be null and must already be installed.
    /// </param>
    /// <param name="store">
    ///     The store to resolve <paramref name="model"/>'s installed-files directory from, via
    ///     <see cref="SpeechModelStore.GetCurrentDirectory(string)"/>. Must not be null.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural composition, lifecycle, and fault events to, or
    ///     <see langword="null"/> to use <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag, forwarded to the returned engine, or
    ///     <see langword="null"/> to use every model's own default voice/speaker.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the load. Must not already be cancelled.</param>
    /// <returns>
    ///     A real engine when the model is installed, its role is synthesis, and the backend loaded
    ///     successfully; otherwise <see cref="UnavailableSpeechSynthesizerEngine.Instance"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/> or <paramref name="store"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains an invalid value for a
    ///     parameter <paramref name="model"/> declares. See the
    ///     <see cref="LoadAsync(ISynthesisModel,string,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,System.Threading.CancellationToken)"/>
    ///     overload's matching remark for the full behavior.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    /// <remarks>
    ///     Equivalent to calling
    ///     <see cref="LoadAsync(ISynthesisModel,string,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,System.Threading.CancellationToken)"/>
    ///     with <c>store.GetCurrentDirectory(model.Id)</c> as the installed-model-directory argument.
    /// </remarks>
    public static Task<ISpeechSynthesizerEngine> LoadAsync(
        ISynthesisModel model,
        SpeechModelStore store,
        ISpeechDiagnostics? diagnostics = null,
        IReadOnlyDictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(store);

        return LoadAsync(model, store.GetCurrentDirectory(model.Id), diagnostics, parameterValues, cancellationToken);
    }

    /// <summary>
    ///     Loads a speech synthesizer engine for an installed synthesis model, resolving the
    ///     model's installed-files directory from the supplied catalog's own store rather than
    ///     requiring the caller to construct a separate <see cref="SpeechModelStore"/>.
    /// </summary>
    /// <param name="model">
    ///     The synthesis model to load. Must not be null and must already be installed.
    /// </param>
    /// <param name="catalog">
    ///     The catalog whose <see cref="SpeechModelCatalog.Store"/> resolves
    ///     <paramref name="model"/>'s installed-files directory. Must not be null.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural composition, lifecycle, and fault events to, or
    ///     <see langword="null"/> to use <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag, forwarded to the returned engine, or
    ///     <see langword="null"/> to use every model's own default voice/speaker.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the load. Must not already be cancelled.</param>
    /// <returns>
    ///     A real engine when the model is installed, its role is synthesis, and the backend loaded
    ///     successfully; otherwise <see cref="UnavailableSpeechSynthesizerEngine.Instance"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/> or <paramref name="catalog"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains an invalid value for a
    ///     parameter <paramref name="model"/> declares. See the
    ///     <see cref="LoadAsync(ISynthesisModel,string,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,System.Threading.CancellationToken)"/>
    ///     overload's matching remark for the full behavior.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    /// <remarks>
    ///     Equivalent to calling
    ///     <see cref="LoadAsync(ISynthesisModel,SpeechModelStore,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,System.Threading.CancellationToken)"/>
    ///     with <c>catalog.Store</c> as the store argument.
    /// </remarks>
    public static Task<ISpeechSynthesizerEngine> LoadAsync(
        ISynthesisModel model,
        SpeechModelCatalog catalog,
        ISpeechDiagnostics? diagnostics = null,
        IReadOnlyDictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(catalog);

        return LoadAsync(model, catalog.Store, diagnostics, parameterValues, cancellationToken);
    }

    /// <summary>
    ///     Loads a speech synthesizer engine using an injected engine factory and a model store,
    ///     for tests that need a deterministic engine while still exercising store-based directory
    ///     resolution.
    /// </summary>
    /// <param name="model">The synthesis model to load. Must not be null.</param>
    /// <param name="store">The store to resolve the model's installed-files directory from. Must not be null.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for the null sink.</param>
    /// <param name="backendFactory">The backend factory to load the model through. Must not be null.</param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag forwarded to the returned engine, or
    ///     <see langword="null"/> to use every model's own default voice/speaker.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the load. Must not already be cancelled.</param>
    /// <returns>
    ///     A real engine, or <see cref="UnavailableSpeechSynthesizerEngine.Instance"/> for any
    ///     honest unavailable state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/>, <paramref name="store"/>, or
    ///     <paramref name="backendFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains an invalid value for a
    ///     parameter <paramref name="model"/> declares.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    internal static Task<ISpeechSynthesizerEngine> LoadAsync(
        ISynthesisModel model,
        SpeechModelStore store,
        ISpeechDiagnostics? diagnostics,
        ISynthesisBackendFactory backendFactory,
        IReadOnlyDictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(store);

        return LoadAsync(model, store.GetCurrentDirectory(model.Id), diagnostics, backendFactory, parameterValues, cancellationToken);
    }

    /// <summary>
    ///     Loads a speech synthesizer engine using an injected engine factory and a model catalog,
    ///     for tests that need a deterministic engine while still exercising catalog-based
    ///     directory resolution.
    /// </summary>
    /// <param name="model">The synthesis model to load. Must not be null.</param>
    /// <param name="catalog">The catalog whose store resolves the model's installed-files directory. Must not be null.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for the null sink.</param>
    /// <param name="backendFactory">The backend factory to load the model through. Must not be null.</param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag forwarded to the returned engine, or
    ///     <see langword="null"/> to use every model's own default voice/speaker.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the load. Must not already be cancelled.</param>
    /// <returns>
    ///     A real engine, or <see cref="UnavailableSpeechSynthesizerEngine.Instance"/> for any
    ///     honest unavailable state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/>, <paramref name="catalog"/>, or
    ///     <paramref name="backendFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains an invalid value for a
    ///     parameter <paramref name="model"/> declares.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    internal static Task<ISpeechSynthesizerEngine> LoadAsync(
        ISynthesisModel model,
        SpeechModelCatalog catalog,
        ISpeechDiagnostics? diagnostics,
        ISynthesisBackendFactory backendFactory,
        IReadOnlyDictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(catalog);

        return LoadAsync(model, catalog.Store, diagnostics, backendFactory, parameterValues, cancellationToken);
    }

    /// <summary>
    ///     Loads a speech synthesizer engine using an injected engine factory, for tests that need
    ///     a deterministic engine with no model files and no native runtime.
    /// </summary>
    /// <param name="model">The synthesis model to load. Must not be null.</param>
    /// <param name="installedModelDirectory">The model's installed-files directory.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for the null sink.</param>
    /// <param name="backendFactory">The backend factory to load the model through. Must not be null.</param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag forwarded to the returned engine, or
    ///     <see langword="null"/> to use every model's own default voice/speaker.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the load. Must not already be cancelled.</param>
    /// <returns>
    ///     A real engine, or <see cref="UnavailableSpeechSynthesizerEngine.Instance"/> for any
    ///     honest unavailable state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/> or <paramref name="backendFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains an invalid value for a
    ///     parameter <paramref name="model"/> declares - wrong CLR type, out of range, a
    ///     non-integral value for an integer-only parameter, or an unrecognized choice/boolean
    ///     value. An unrecognized parameter id is reported at <see cref="SpeechDiagnosticLevel.Info"/>
    ///     and silently ignored instead.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    internal static async Task<ISpeechSynthesizerEngine> LoadAsync(
        ISynthesisModel model,
        string installedModelDirectory,
        ISpeechDiagnostics? diagnostics,
        ISynthesisBackendFactory backendFactory,
        IReadOnlyDictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(backendFactory);
        cancellationToken.ThrowIfCancellationRequested();

        var sink = diagnostics ?? NullSpeechDiagnostics.Instance;

        // Validate the caller's parameter value bag against this model's own declared
        // parameters before anything else: an unrecognized id is reported and silently ignored
        // (preserving cross-model settings-dictionary reuse), while an invalid value for a
        // parameter this model does declare throws synchronously from this call, rather than
        // degrading silently deep inside a later per-segment resolution hook.
        SpeechModelParameterDiagnostics.ValidateAndReport(
            model.Id, model.Parameters, parameterValues, sink, DiagnosticsCategory);

        // A model that has not been downloaded yet is the single most common reason synthesis
        // is unavailable, and is an ordinary first-run state rather than an error.
        if (string.IsNullOrEmpty(installedModelDirectory) || !Directory.Exists(installedModelDirectory))
        {
            sink.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                $"Speech synthesis is unavailable because model '{model.Id}' is not installed.");
            return UnavailableSpeechSynthesizerEngine.Instance;
        }

        // Guard against a model whose declared role contradicts the synthesis interface it
        // implements: loading it would build an engine that could never produce audio.
        if (model.Role != SpeechModelRole.Synthesis)
        {
            sink.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                $"Speech synthesis is unavailable because model '{model.Id}' does not declare the synthesis role.");
            return UnavailableSpeechSynthesizerEngine.Instance;
        }

        // Loading is the one step that touches the native runtime, so it is also the one step
        // that can fail for a missing org.k2fsa.sherpa.onnx.runtime.{RID} binary or unusable
        // model files. Both degrade exactly like a missing model rather than crashing start-up.
        // The native load is a blocking call, so it runs on a dedicated worker rather than
        // blocking whichever thread is awaiting this method.
        ISynthesisBackend backend;
        try
        {
            backend = await DedicatedWorker.Run(
                _ => backendFactory.Create(model, installedModelDirectory),
                cancellationToken,
                sink,
                DiagnosticsCategory).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Intentionally broad: engine creation crosses the native runtime/model-file
            // boundary, and every load failure must degrade to the documented unavailable
            // engine rather than crash composition.
            sink.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Speech synthesis is unavailable because the engine for model '{model.Id}' could not be loaded: {ex.Message}");
            return UnavailableSpeechSynthesizerEngine.Instance;
        }

        sink.Report(
            SpeechDiagnosticLevel.Info,
            DiagnosticsCategory,
            $"Loaded a speech synthesizer engine for model '{model.Id}'.");
        return new SherpaOnnxSpeechSynthesizerEngine(backend, model, parameterValues, sink);
    }
}
