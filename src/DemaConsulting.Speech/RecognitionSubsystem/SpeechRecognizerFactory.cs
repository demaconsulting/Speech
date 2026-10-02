using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Composition entry point for obtaining an <see cref="ISpeechRecognizerEngine"/> for one
///     installed recognition model.
/// </summary>
/// <remarks>
///     Hosts call one of this factory's three <c>LoadAsync</c> overloads - taking an
///     <c>installedModelDirectory</c> path directly
///     (<see cref="LoadAsync(IRecognitionModel,string,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,CancellationToken)"/>),
///     a <see cref="SpeechModelStore"/>
///     (<see cref="LoadAsync(IRecognitionModel,SpeechModelStore,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,CancellationToken)"/>),
///     or a <see cref="SpeechModelCatalog"/>
///     (<see cref="LoadAsync(IRecognitionModel,SpeechModelCatalog,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,CancellationToken)"/>)
///     - rather than constructing an engine directly, so all of the "can this machine actually
///     recognize speech right now?" logic lives in one reviewable place. Per this library's
///     "nothing throws at composition" decision, these methods never fault their returned task
///     for an ordinary machine state - a model that is not installed, a model whose role is not
///     recognition, and a machine missing the sherpa-onnx native runtime all return
///     <see cref="UnavailableSpeechRecognizerEngine.Instance"/> and are reported through the
///     diagnostics sink. Only a null argument, an invalid parameter value, and caller
///     cancellation - all programming errors or explicit caller requests rather than machine
///     states - fault the returned task.
///     <para>
///     Loading a model is the expensive step (it touches the native runtime and can block), so it
///     runs on a dedicated worker thread and is awaited by the returned <see cref="Task"/> rather
///     than blocking the calling thread synchronously. Nothing in this type's public signature
///     names a sherpa-onnx type, keeping this library's "engine backend stays swappable at the
///     public API surface" promise intact. A capture device is bound later, per session, via
///     <see cref="ISpeechRecognizerEngine.CreateSessionAsync"/> - not here - so one loaded engine
///     can be reused across many devices or many sequential sessions over its life.
///     </para>
///     <para>
///     <b>Reuse for low latency ("hot" recognition).</b> Calling this factory is the expensive
///     step; <see cref="ISpeechRecognizerEngine.CreateSessionAsync"/> and the resulting session's
///     <see cref="IRecognitionSession.StartAsync"/>/<see cref="IRecognitionSession.StopAsync"/>
///     cycle are comparatively cheap. For low-latency repeated recognition (for example, many
///     turns of a voice conversation), load one engine and reuse it across many sessions rather
///     than reloading the model per turn; only load a new engine to change model or parameters.
///     </para>
/// </remarks>
public static class SpeechRecognizerFactory
{
    /// <summary>The diagnostics category used for every event this factory reports.</summary>
    private const string DiagnosticsCategory = "RecognitionSubsystem";

    /// <summary>
    ///     Loads a speech recognizer engine for an installed recognition model.
    /// </summary>
    /// <param name="model">
    ///     The recognition model to load. Must not be null and must already be installed.
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
    ///     An optional session-level parameter value bag (for example a selected recognition
    ///     language or tuning value, built from the model's declared
    ///     <see cref="ISpeechModel.Parameters"/>), forwarded to
    ///     <see cref="IRecognitionModel.CreateEngineConfig(string,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>
    ///     when the backend is constructed, or <see langword="null"/> to use every model's own
    ///     default behavior. Neither shipped recognition model declares a parameter today, so this
    ///     argument is a safe no-op for them.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation of this call.</param>
    /// <returns>
    ///     A task that completes with a real engine when the model is installed, its role is
    ///     recognition, and the backend loaded successfully; otherwise
    ///     <see cref="UnavailableSpeechRecognizerEngine.Instance"/>. The task itself is faulted
    ///     only for a null argument, an invalid parameter value, or cancellation.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown (faulting the returned task) when <paramref name="model"/> is
    ///     <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown (faulting the returned task) when <paramref name="parameterValues"/> contains a
    ///     value for a parameter <paramref name="model"/> declares that is invalid for it (wrong
    ///     type, out of range, non-integral for an integer-only parameter, or an unrecognized
    ///     choice/boolean value). An unrecognized parameter id is not an error - it is reported at
    ///     <see cref="SpeechDiagnosticLevel.Info"/> and silently ignored, preserving this
    ///     library's cross-model settings-dictionary-reuse contract.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     Thrown (faulting the returned task) when <paramref name="cancellationToken"/> is
    ///     cancelled before loading completes.
    /// </exception>
    public static Task<ISpeechRecognizerEngine> LoadAsync(
        IRecognitionModel model,
        string installedModelDirectory,
        ISpeechDiagnostics? diagnostics = null,
        IReadOnlyDictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default)
    {
        return LoadAsync(
            model,
            installedModelDirectory,
            diagnostics,
            new SherpaOnnxRecognitionEngineFactory(),
            parameterValues,
            cancellationToken);
    }

    /// <summary>
    ///     Loads a speech recognizer engine for an installed recognition model, resolving the
    ///     model's installed-files directory from the supplied model store rather than requiring
    ///     the caller to know anything about the store's on-disk directory layout.
    /// </summary>
    /// <param name="model">
    ///     The recognition model to load. Must not be null and must already be installed.
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
    ///     An optional session-level parameter value bag, forwarded to
    ///     <see cref="IRecognitionModel.CreateEngineConfig(string,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>
    ///     when the backend is constructed, or <see langword="null"/> to use every model's own
    ///     default behavior.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation of this call.</param>
    /// <returns>
    ///     A task that completes with a real engine, or <see cref="UnavailableSpeechRecognizerEngine.Instance"/>
    ///     for any honest unavailable state. See the
    ///     <see cref="LoadAsync(IRecognitionModel,string,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,CancellationToken)"/>
    ///     overload for the full behavior.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown synchronously, before the returned task is created, when
    ///     <paramref name="model"/> or <paramref name="store"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown (faulting the returned task) when <paramref name="parameterValues"/> contains an
    ///     invalid value for a parameter <paramref name="model"/> declares.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     Thrown (faulting the returned task) when <paramref name="cancellationToken"/> is
    ///     cancelled before loading completes.
    /// </exception>
    public static Task<ISpeechRecognizerEngine> LoadAsync(
        IRecognitionModel model,
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
    ///     Loads a speech recognizer engine for an installed recognition model, resolving the
    ///     model's installed-files directory from the supplied catalog's own store rather than
    ///     requiring the caller to construct a separate <see cref="SpeechModelStore"/>.
    /// </summary>
    /// <param name="model">
    ///     The recognition model to load. Must not be null and must already be installed.
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
    ///     An optional session-level parameter value bag, forwarded to
    ///     <see cref="IRecognitionModel.CreateEngineConfig(string,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>
    ///     when the backend is constructed, or <see langword="null"/> to use every model's own
    ///     default behavior.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation of this call.</param>
    /// <returns>
    ///     A task that completes with a real engine, or <see cref="UnavailableSpeechRecognizerEngine.Instance"/>
    ///     for any honest unavailable state. See the
    ///     <see cref="LoadAsync(IRecognitionModel,string,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,CancellationToken)"/>
    ///     overload for the full behavior.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown synchronously, before the returned task is created, when
    ///     <paramref name="model"/> or <paramref name="catalog"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown (faulting the returned task) when <paramref name="parameterValues"/> contains an
    ///     invalid value for a parameter <paramref name="model"/> declares.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     Thrown (faulting the returned task) when <paramref name="cancellationToken"/> is
    ///     cancelled before loading completes.
    /// </exception>
    public static Task<ISpeechRecognizerEngine> LoadAsync(
        IRecognitionModel model,
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
    ///     Loads a speech recognizer engine using an injected backend factory and a model store,
    ///     for tests that need a deterministic backend while still exercising store-based
    ///     directory resolution.
    /// </summary>
    /// <param name="model">The recognition model to load. Must not be null.</param>
    /// <param name="store">The store to resolve the model's installed-files directory from. Must not be null.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for the null sink.</param>
    /// <param name="backendFactory">The backend factory to load the model through. Must not be null.</param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag forwarded to
    ///     <paramref name="backendFactory"/>'s <c>Create</c> call, or <see langword="null"/> to use
    ///     every model's own default behavior.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation of this call.</param>
    /// <returns>
    ///     A task that completes with a real engine, or <see cref="UnavailableSpeechRecognizerEngine.Instance"/>
    ///     for any honest unavailable state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown synchronously, before the returned task is created, when
    ///     <paramref name="model"/>, <paramref name="store"/>, or
    ///     <paramref name="backendFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown (faulting the returned task) when <paramref name="parameterValues"/> contains an
    ///     invalid value for a parameter <paramref name="model"/> declares.
    /// </exception>
    internal static Task<ISpeechRecognizerEngine> LoadAsync(
        IRecognitionModel model,
        SpeechModelStore store,
        ISpeechDiagnostics? diagnostics,
        IRecognitionBackendFactory backendFactory,
        IReadOnlyDictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(store);

        return LoadAsync(
            model,
            store.GetCurrentDirectory(model.Id),
            diagnostics,
            backendFactory,
            parameterValues,
            cancellationToken);
    }

    /// <summary>
    ///     Loads a speech recognizer engine using an injected backend factory and a model catalog,
    ///     for tests that need a deterministic backend while still exercising catalog-based
    ///     directory resolution.
    /// </summary>
    /// <param name="model">The recognition model to load. Must not be null.</param>
    /// <param name="catalog">The catalog whose store resolves the model's installed-files directory. Must not be null.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for the null sink.</param>
    /// <param name="backendFactory">The backend factory to load the model through. Must not be null.</param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag forwarded to
    ///     <paramref name="backendFactory"/>'s <c>Create</c> call, or <see langword="null"/> to use
    ///     every model's own default behavior.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation of this call.</param>
    /// <returns>
    ///     A task that completes with a real engine, or <see cref="UnavailableSpeechRecognizerEngine.Instance"/>
    ///     for any honest unavailable state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown synchronously, before the returned task is created, when
    ///     <paramref name="model"/>, <paramref name="catalog"/>, or
    ///     <paramref name="backendFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown (faulting the returned task) when <paramref name="parameterValues"/> contains an
    ///     invalid value for a parameter <paramref name="model"/> declares.
    /// </exception>
    internal static Task<ISpeechRecognizerEngine> LoadAsync(
        IRecognitionModel model,
        SpeechModelCatalog catalog,
        ISpeechDiagnostics? diagnostics,
        IRecognitionBackendFactory backendFactory,
        IReadOnlyDictionary<string, object>? parameterValues = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(catalog);

        return LoadAsync(model, catalog.Store, diagnostics, backendFactory, parameterValues, cancellationToken);
    }

    /// <summary>
    ///     Loads a speech recognizer engine using an injected backend factory, for tests that need
    ///     a deterministic backend with no model files and no native runtime.
    /// </summary>
    /// <param name="model">The recognition model to load. Must not be null.</param>
    /// <param name="installedModelDirectory">The model's installed-files directory.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for the null sink.</param>
    /// <param name="backendFactory">The backend factory to load the model through. Must not be null.</param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag forwarded to
    ///     <paramref name="backendFactory"/>'s <c>Create</c> call, which in turn passes it to
    ///     <see cref="IRecognitionModel.CreateEngineConfig(string,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>,
    ///     or <see langword="null"/> to use every model's own default behavior.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation of this call.</param>
    /// <returns>
    ///     A task that completes with a real engine, or <see cref="UnavailableSpeechRecognizerEngine.Instance"/>
    ///     for any honest unavailable state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown (faulting the returned task) when <paramref name="model"/> or
    ///     <paramref name="backendFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown (faulting the returned task) when <paramref name="parameterValues"/> contains an
    ///     invalid value for a parameter <paramref name="model"/> declares - wrong CLR type, out
    ///     of range, a non-integral value for an integer-only parameter, or an unrecognized
    ///     choice/boolean value. An unrecognized parameter id is reported at
    ///     <see cref="SpeechDiagnosticLevel.Info"/> and silently ignored instead.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     Thrown (faulting the returned task) when <paramref name="cancellationToken"/> is
    ///     cancelled before loading completes.
    /// </exception>
    internal static async Task<ISpeechRecognizerEngine> LoadAsync(
        IRecognitionModel model,
        string installedModelDirectory,
        ISpeechDiagnostics? diagnostics,
        IRecognitionBackendFactory backendFactory,
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
        // parameter this model does declare faults this call synchronously, rather than
        // degrading silently deep inside the loaded backend.
        SpeechModelParameterDiagnostics.ValidateAndReport(
            model.Id, model.Parameters, parameterValues, sink, DiagnosticsCategory);

        // A model that has not been downloaded yet is the single most common reason recognition
        // is unavailable, and is an ordinary first-run state rather than an error.
        if (string.IsNullOrEmpty(installedModelDirectory) || !Directory.Exists(installedModelDirectory))
        {
            sink.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                $"Speech recognition is unavailable because model '{model.Id}' is not installed.");
            return UnavailableSpeechRecognizerEngine.Instance;
        }

        // Guard against a model whose declared role contradicts the recognition interface it
        // implements: loading it would build an engine that could never produce text.
        if (model.Role != SpeechModelRole.Recognition)
        {
            sink.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                $"Speech recognition is unavailable because model '{model.Id}' does not declare the recognition role.");
            return UnavailableSpeechRecognizerEngine.Instance;
        }

        // Loading is the one step that touches the native runtime, so it is also the one step
        // that can fail for a missing org.k2fsa.sherpa.onnx.runtime.{RID} binary or unusable
        // model files. It is also the one step that genuinely blocks, so it runs on a dedicated
        // worker thread and is awaited here rather than run synchronously on the caller's thread.
        IRecognitionBackend? backend = null;
        Exception? loadFailure = null;
        var worker = new DedicatedWorker(diagnostics: sink, diagnosticsCategory: DiagnosticsCategory);
        Task completion = Task.CompletedTask;
        try
        {
            await worker.RunAsync(
                _ =>
                {
                    try
                    {
                        backend = backendFactory.Create(model, installedModelDirectory, parameterValues);
                    }
                    catch (Exception ex)
                    {
                        // Intentionally broad: backend creation crosses the native runtime/model-file
                        // boundary, and every load failure must degrade to the documented unavailable
                        // engine rather than crash composition.
                        loadFailure = ex;
                    }
                },
                cancellationToken,
                out completion).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Abandoned: backendFactory.Create ignored cancellation past the worker's abandon
            // timeout, so it may still create and assign a native backend after this call has
            // already faulted with cancellation. Nothing else observes that eventual backend, so
            // dispose it here once it genuinely arrives rather than leaking native model
            // resources on a canceled load.
            _ = completion.ContinueWith(
                _ => backend?.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            throw;
        }

        if (loadFailure is not null)
        {
            sink.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Speech recognition is unavailable because the engine for model '{model.Id}' could not be loaded: {loadFailure.Message}");
            return UnavailableSpeechRecognizerEngine.Instance;
        }

        sink.Report(
            SpeechDiagnosticLevel.Info,
            DiagnosticsCategory,
            $"Loaded a speech recognizer engine for model '{model.Id}'.");
        return new SherpaOnnxSpeechRecognizerEngine(backend!, model, sink);
    }
}
