using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Composition entry point for obtaining an <see cref="ISpeechSynthesizer"/> for one
///     installed synthesis model and one playback device.
/// </summary>
/// <remarks>
///     Hosts call <see cref="Create(ISynthesisModel,string,IAudioPlaybackDevice,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>,
///     <see cref="Create(ISynthesisModel,SpeechModelStore,IAudioPlaybackDevice,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>,
///     or <see cref="Create(ISynthesisModel,SpeechModelCatalog,IAudioPlaybackDevice,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>
///     rather than constructing a synthesizer directly, so all of the "can this machine actually
///     speak right now?" logic lives in one reviewable place. Per this library's "nothing
///     throws at composition" decision this method never throws for an ordinary machine state - a
///     model that is not installed, a model whose role is not synthesis, a machine with no
///     playback device, and a machine missing the sherpa-onnx native runtime all return
///     <see cref="UnavailableSpeechSynthesizer.Instance"/> and are reported through the
///     diagnostics sink. Only a null argument, which is a programming error rather than a machine
///     state, throws.
///     <para>
///     Nothing in this type's public signature names a sherpa-onnx type, keeping this library's
///     "engine backend stays swappable at the public API surface" promise intact.
///     </para>
/// </remarks>
public static class SpeechSynthesizerFactory
{
    /// <summary>The diagnostics category used for every event this factory reports.</summary>
    private const string DiagnosticsCategory = "SynthesisSubsystem";

    /// <summary>
    ///     Creates a speech synthesizer for an installed synthesis model, playing back through the
    ///     supplied playback device.
    /// </summary>
    /// <param name="model">
    ///     The synthesis model to load. Must not be null and must already be installed.
    /// </param>
    /// <param name="installedModelDirectory">
    ///     The absolute path of the directory holding the model's installed files, as returned by
    ///     <c>SpeechModelStore.GetCurrentDirectory(model.Id)</c>. A path that is null, empty, or
    ///     does not exist is treated as "model not installed", not as an error.
    /// </param>
    /// <param name="playbackDevice">
    ///     The playback device to play synthesized audio through, as returned by
    ///     <c>AudioDeviceFactory.CreatePlaybackDevice(...)</c>. Must not be null; a device
    ///     reporting <c>IsAvailable == false</c> is treated as "no speakers", not as an error.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural composition, lifecycle, and fault events to, or
    ///     <see langword="null"/> to use <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag (for example a selected voice, built
    ///     from the model's declared <see cref="ISpeechModel.Parameters"/>), forwarded to the
    ///     returned synthesizer and re-resolved via <see cref="ISynthesisModel.ResolveSpeakerId"/>
    ///     once per synthesized segment, or <see langword="null"/> to use every model's own
    ///     default voice/speaker.
    /// </param>
    /// <returns>
    ///     A real chunked/streaming synthesizer when the model is installed, its role is
    ///     synthesis, the playback device is available, and the engine loaded successfully;
    ///     otherwise <see cref="UnavailableSpeechSynthesizer.Instance"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/> or <paramref name="playbackDevice"/> is
    ///     <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains a value for a parameter
    ///     <paramref name="model"/> declares that is invalid for it (wrong type, out of range,
    ///     non-integral for an integer-only parameter, or an unrecognized choice/boolean value).
    ///     An unrecognized parameter id is not an error - it is reported at
    ///     <see cref="Diagnostics.SpeechDiagnosticLevel.Info"/> and silently ignored, preserving
    ///     this library's cross-model settings-dictionary-reuse contract.
    /// </exception>
    /// <remarks>
    ///     Loads the model into native memory when it succeeds, so the returned synthesizer owns
    ///     unmanaged resources and must be disposed. Reports every fallback decision through the
    ///     diagnostics sink as a structural fact, never including synthesized text.
    ///     <para>
    ///     Recommended composition pattern: create the playback device first with
    ///     <c>new AudioDeviceFactory().CreatePlaybackDevice(selection, model.PreferredAudioFormat)</c>,
    ///     then pass that device here. This hint is best-effort only: the authoritative playback
    ///     rate remains the constructed engine's <see cref="ISynthesisEngine.SampleRate"/>, so
    ///     <see cref="PlaybackAudioResampler"/> remains the guaranteed fallback whenever the
    ///     loaded engine's actual output rate differs from the preferred hint.
    ///     </para>
    /// </remarks>
    public static ISpeechSynthesizer Create(
        ISynthesisModel model,
        string installedModelDirectory,
        IAudioPlaybackDevice playbackDevice,
        ISpeechDiagnostics? diagnostics = null,
        IReadOnlyDictionary<string, object>? parameterValues = null)
    {
        return Create(model, installedModelDirectory, playbackDevice, diagnostics, new SherpaOnnxSynthesisEngineFactory(), parameterValues);
    }

    /// <summary>
    ///     Creates a speech synthesizer for an installed synthesis model, resolving the model's
    ///     installed-files directory from the supplied model store rather than requiring the caller
    ///     to know anything about the store's on-disk directory layout.
    /// </summary>
    /// <param name="model">
    ///     The synthesis model to load. Must not be null and must already be installed.
    /// </param>
    /// <param name="store">
    ///     The store to resolve <paramref name="model"/>'s installed-files directory from, via
    ///     <see cref="SpeechModelStore.GetCurrentDirectory(string)"/>. Must not be null.
    /// </param>
    /// <param name="playbackDevice">
    ///     The playback device to play synthesized audio through, as returned by
    ///     <c>AudioDeviceFactory.CreatePlaybackDevice(...)</c>. Must not be null; a device
    ///     reporting <c>IsAvailable == false</c> is treated as "no speakers", not as an error.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural composition, lifecycle, and fault events to, or
    ///     <see langword="null"/> to use <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag, forwarded to the returned synthesizer, or
    ///     <see langword="null"/> to use every model's own default voice/speaker.
    /// </param>
    /// <returns>
    ///     A real chunked/streaming synthesizer when the model is installed, its role is synthesis,
    ///     the playback device is available, and the engine loaded successfully; otherwise
    ///     <see cref="UnavailableSpeechSynthesizer.Instance"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/>, <paramref name="store"/>, or
    ///     <paramref name="playbackDevice"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains an invalid value for a
    ///     parameter <paramref name="model"/> declares. See the
    ///     <see cref="Create(ISynthesisModel,string,IAudioPlaybackDevice,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>
    ///     overload's matching remark for the full behavior.
    /// </exception>
    /// <remarks>
    ///     Equivalent to calling
    ///     <see cref="Create(ISynthesisModel,string,IAudioPlaybackDevice,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>
    ///     with <c>store.GetCurrentDirectory(model.Id)</c> as the installed-model-directory argument,
    ///     so callers never need to know <see cref="SpeechModelStore"/>'s on-disk directory-naming
    ///     scheme just to compose a synthesizer.
    /// </remarks>
    public static ISpeechSynthesizer Create(
        ISynthesisModel model,
        SpeechModelStore store,
        IAudioPlaybackDevice playbackDevice,
        ISpeechDiagnostics? diagnostics = null,
        IReadOnlyDictionary<string, object>? parameterValues = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(store);

        return Create(model, store.GetCurrentDirectory(model.Id), playbackDevice, diagnostics, parameterValues);
    }

    /// <summary>
    ///     Creates a speech synthesizer for an installed synthesis model, resolving the model's
    ///     installed-files directory from the supplied catalog's own store rather than requiring
    ///     the caller to construct a separate <see cref="SpeechModelStore"/>.
    /// </summary>
    /// <param name="model">
    ///     The synthesis model to load. Must not be null and must already be installed.
    /// </param>
    /// <param name="catalog">
    ///     The catalog whose <see cref="SpeechModelCatalog.Store"/> resolves
    ///     <paramref name="model"/>'s installed-files directory. Must not be null.
    /// </param>
    /// <param name="playbackDevice">
    ///     The playback device to play synthesized audio through, as returned by
    ///     <c>AudioDeviceFactory.CreatePlaybackDevice(...)</c>. Must not be null; a device
    ///     reporting <c>IsAvailable == false</c> is treated as "no speakers", not as an error.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural composition, lifecycle, and fault events to, or
    ///     <see langword="null"/> to use <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag, forwarded to the returned synthesizer, or
    ///     <see langword="null"/> to use every model's own default voice/speaker.
    /// </param>
    /// <returns>
    ///     A real chunked/streaming synthesizer when the model is installed, its role is synthesis,
    ///     the playback device is available, and the engine loaded successfully; otherwise
    ///     <see cref="UnavailableSpeechSynthesizer.Instance"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/>, <paramref name="catalog"/>, or
    ///     <paramref name="playbackDevice"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains an invalid value for a
    ///     parameter <paramref name="model"/> declares. See the
    ///     <see cref="Create(ISynthesisModel,string,IAudioPlaybackDevice,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>
    ///     overload's matching remark for the full behavior.
    /// </exception>
    /// <remarks>
    ///     Equivalent to calling
    ///     <see cref="Create(ISynthesisModel,SpeechModelStore,IAudioPlaybackDevice,ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?)"/>
    ///     with <c>catalog.Store</c> as the store argument, so a host that already owns a
    ///     <see cref="SpeechModelCatalog"/> for enumeration and download can compose a synthesizer
    ///     through that same catalog instance, without constructing a second, potentially
    ///     divergent <see cref="SpeechModelStore"/>.
    /// </remarks>
    public static ISpeechSynthesizer Create(
        ISynthesisModel model,
        SpeechModelCatalog catalog,
        IAudioPlaybackDevice playbackDevice,
        ISpeechDiagnostics? diagnostics = null,
        IReadOnlyDictionary<string, object>? parameterValues = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(catalog);

        return Create(model, catalog.Store, playbackDevice, diagnostics, parameterValues);
    }

    /// <summary>
    ///     Creates a speech synthesizer using an injected engine factory and a model store, for
    ///     tests that need a deterministic engine while still exercising store-based directory
    ///     resolution.
    /// </summary>
    /// <param name="model">The synthesis model to load. Must not be null.</param>
    /// <param name="store">The store to resolve the model's installed-files directory from. Must not be null.</param>
    /// <param name="playbackDevice">The playback device to play through. Must not be null.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for the null sink.</param>
    /// <param name="engineFactory">The engine factory to load the model through. Must not be null.</param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag forwarded to the returned synthesizer, or
    ///     <see langword="null"/> to use every model's own default voice/speaker.
    /// </param>
    /// <returns>
    ///     A real chunked/streaming synthesizer, or <see cref="UnavailableSpeechSynthesizer.Instance"/>
    ///     for any honest unavailable state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/>, <paramref name="store"/>,
    ///     <paramref name="playbackDevice"/>, or <paramref name="engineFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains an invalid value for a
    ///     parameter <paramref name="model"/> declares.
    /// </exception>
    internal static ISpeechSynthesizer Create(
        ISynthesisModel model,
        SpeechModelStore store,
        IAudioPlaybackDevice playbackDevice,
        ISpeechDiagnostics? diagnostics,
        ISynthesisEngineFactory engineFactory,
        IReadOnlyDictionary<string, object>? parameterValues = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(store);

        return Create(model, store.GetCurrentDirectory(model.Id), playbackDevice, diagnostics, engineFactory, parameterValues);
    }

    /// <summary>
    ///     Creates a speech synthesizer using an injected engine factory and a model catalog, for
    ///     tests that need a deterministic engine while still exercising catalog-based directory
    ///     resolution.
    /// </summary>
    /// <param name="model">The synthesis model to load. Must not be null.</param>
    /// <param name="catalog">The catalog whose store resolves the model's installed-files directory. Must not be null.</param>
    /// <param name="playbackDevice">The playback device to play through. Must not be null.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for the null sink.</param>
    /// <param name="engineFactory">The engine factory to load the model through. Must not be null.</param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag forwarded to the returned synthesizer, or
    ///     <see langword="null"/> to use every model's own default voice/speaker.
    /// </param>
    /// <returns>
    ///     A real chunked/streaming synthesizer, or <see cref="UnavailableSpeechSynthesizer.Instance"/>
    ///     for any honest unavailable state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/>, <paramref name="catalog"/>,
    ///     <paramref name="playbackDevice"/>, or <paramref name="engineFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains an invalid value for a
    ///     parameter <paramref name="model"/> declares.
    /// </exception>
    internal static ISpeechSynthesizer Create(
        ISynthesisModel model,
        SpeechModelCatalog catalog,
        IAudioPlaybackDevice playbackDevice,
        ISpeechDiagnostics? diagnostics,
        ISynthesisEngineFactory engineFactory,
        IReadOnlyDictionary<string, object>? parameterValues = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(catalog);

        return Create(model, catalog.Store, playbackDevice, diagnostics, engineFactory, parameterValues);
    }

    /// <summary>
    ///     Creates a speech synthesizer using an injected engine factory, for tests that need a
    ///     deterministic engine with no model files and no native runtime.
    /// </summary>
    /// <param name="model">The synthesis model to load. Must not be null.</param>
    /// <param name="installedModelDirectory">The model's installed-files directory.</param>
    /// <param name="playbackDevice">The playback device to play through. Must not be null.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for the null sink.</param>
    /// <param name="engineFactory">The engine factory to load the model through. Must not be null.</param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag forwarded to the returned synthesizer,
    ///     or <see langword="null"/> to use every model's own default voice/speaker.
    /// </param>
    /// <returns>
    ///     A real chunked/streaming synthesizer, or <see cref="UnavailableSpeechSynthesizer.Instance"/>
    ///     for any honest unavailable state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/>, <paramref name="playbackDevice"/>, or
    ///     <paramref name="engineFactory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains an invalid value for a
    ///     parameter <paramref name="model"/> declares - wrong CLR type, out of range, a
    ///     non-integral value for an integer-only parameter, or an unrecognized choice/boolean
    ///     value. An unrecognized parameter id is reported at
    ///     <see cref="Diagnostics.SpeechDiagnosticLevel.Info"/> and silently ignored instead.
    /// </exception>
    internal static ISpeechSynthesizer Create(
        ISynthesisModel model,
        string installedModelDirectory,
        IAudioPlaybackDevice playbackDevice,
        ISpeechDiagnostics? diagnostics,
        ISynthesisEngineFactory engineFactory,
        IReadOnlyDictionary<string, object>? parameterValues = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(playbackDevice);
        ArgumentNullException.ThrowIfNull(engineFactory);

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
            return UnavailableSpeechSynthesizer.Instance;
        }

        // Guard against a model whose declared role contradicts the synthesis interface it
        // implements: loading it would build a synthesizer that could never produce audio.
        if (model.Role != SpeechModelRole.Synthesis)
        {
            sink.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                $"Speech synthesis is unavailable because model '{model.Id}' does not declare the synthesis role.");
            return UnavailableSpeechSynthesizer.Instance;
        }

        // No speakers (or no working audio backend) is an ordinary machine state too; there is
        // nowhere to play audio, so report it honestly rather than loading a model that could
        // never be heard.
        if (!playbackDevice.IsAvailable)
        {
            sink.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                "Speech synthesis is unavailable because no audio playback device is available.");
            return UnavailableSpeechSynthesizer.Instance;
        }

        // Loading is the one step that touches the native runtime, so it is also the one step
        // that can fail for a missing org.k2fsa.sherpa.onnx.runtime.{RID} binary or unusable
        // model files. Both degrade exactly like a missing model rather than crashing start-up.
        ISynthesisEngine engine;
        try
        {
            engine = engineFactory.Create(model, installedModelDirectory);
        }
        catch (Exception ex)
        {
            sink.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Speech synthesis is unavailable because the engine for model '{model.Id}' could not be loaded: {ex.Message}");
            return UnavailableSpeechSynthesizer.Instance;
        }

        sink.Report(
            SpeechDiagnosticLevel.Info,
            DiagnosticsCategory,
            $"Composed a streaming speech synthesizer for model '{model.Id}'.");
        return new SherpaOnnxSpeechSynthesizer(engine, playbackDevice, model, parameterValues, sink);
    }
}
