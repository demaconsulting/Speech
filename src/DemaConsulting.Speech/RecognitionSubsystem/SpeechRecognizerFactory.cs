using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Composition entry point for obtaining an <see cref="ISpeechRecognizer"/> for one installed
///     recognition model and one capture device.
/// </summary>
/// <remarks>
///     Hosts call <see cref="Create(IRecognitionModel,string,IAudioCaptureDevice,ISpeechDiagnostics)"/>,
///     <see cref="Create(IRecognitionModel,SpeechModelStore,IAudioCaptureDevice,ISpeechDiagnostics)"/>, or
///     <see cref="Create(IRecognitionModel,SpeechModelCatalog,IAudioCaptureDevice,ISpeechDiagnostics)"/>
///     rather than constructing a recognizer directly, so all of the "can this machine actually
///     recognize speech right now?" logic lives in one reviewable place. Per this library's
///     "nothing throws at composition" decision this method never throws for an ordinary machine
///     state - a model that is not installed, a model whose role is not recognition, a machine
///     with no capture device, and a machine missing the sherpa-onnx native runtime all return
///     <see cref="UnavailableSpeechRecognizer.Instance"/> and are reported through the
///     diagnostics sink. Only a null argument, which is a programming error rather than a machine
///     state, throws.
///     <para>
///     Nothing in this type's public signature names a sherpa-onnx type, keeping this library's
///     "engine backend stays swappable at the public API surface" promise intact.
///     </para>
/// </remarks>
public static class SpeechRecognizerFactory
{
    /// <summary>The diagnostics category used for every event this factory reports.</summary>
    private const string DiagnosticsCategory = "RecognitionSubsystem";

    /// <summary>
    ///     Creates a speech recognizer for an installed recognition model, streaming from the
    ///     supplied capture device.
    /// </summary>
    /// <param name="model">
    ///     The recognition model to load. Must not be null and must already be installed.
    /// </param>
    /// <param name="installedModelDirectory">
    ///     The absolute path of the directory holding the model's installed files, as returned by
    ///     <c>SpeechModelStore.GetCurrentDirectory(model.Id)</c>. A path that is null, empty, or
    ///     does not exist is treated as "model not installed", not as an error.
    /// </param>
    /// <param name="captureDevice">
    ///     The capture device to stream audio from, as returned by
    ///     <c>AudioDeviceFactory.CreateCaptureDevice(...)</c>. Must not be null; a device
    ///     reporting <c>IsAvailable == false</c> is treated as "no microphone", not as an error.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural composition, lifecycle, and fault events to, or
    ///     <see langword="null"/> to use <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <returns>
    ///     A real streaming recognizer when the model is installed, its role is recognition, the
    ///     capture device is available, and the engine loaded successfully; otherwise
    ///     <see cref="UnavailableSpeechRecognizer.Instance"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/> or <paramref name="captureDevice"/> is
    ///     <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Loads the model into native memory when it succeeds, so the returned recognizer owns
    ///     unmanaged resources and must be disposed. Reports every fallback decision through the
    ///     diagnostics sink as a structural fact, never including recognized text.
    ///     <para>
    ///     Recommended composition pattern: create the capture device first with
    ///     <c>new AudioDeviceFactory().CreateCaptureDevice(selection, model.AudioFormat)</c>, then
    ///     pass that device here. When the backend honors the preferred format, the device opens
    ///     already matching the recognition model's mono input rate and
    ///     <see cref="AudioFrameResampler"/> falls through to its existing no-op fast path.
    ///     </para>
    /// </remarks>
    public static ISpeechRecognizer Create(
        IRecognitionModel model,
        string installedModelDirectory,
        IAudioCaptureDevice captureDevice,
        ISpeechDiagnostics? diagnostics = null)
    {
        return Create(model, installedModelDirectory, captureDevice, diagnostics, new SherpaOnnxRecognitionEngineFactory());
    }

    /// <summary>
    ///     Creates a speech recognizer for an installed recognition model, resolving the model's
    ///     installed-files directory from the supplied model store rather than requiring the caller
    ///     to know anything about the store's on-disk directory layout.
    /// </summary>
    /// <param name="model">
    ///     The recognition model to load. Must not be null and must already be installed.
    /// </param>
    /// <param name="store">
    ///     The store to resolve <paramref name="model"/>'s installed-files directory from, via
    ///     <see cref="SpeechModelStore.GetCurrentDirectory(string)"/>. Must not be null.
    /// </param>
    /// <param name="captureDevice">
    ///     The capture device to stream audio from, as returned by
    ///     <c>AudioDeviceFactory.CreateCaptureDevice(...)</c>. Must not be null; a device
    ///     reporting <c>IsAvailable == false</c> is treated as "no microphone", not as an error.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural composition, lifecycle, and fault events to, or
    ///     <see langword="null"/> to use <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <returns>
    ///     A real streaming recognizer when the model is installed, its role is recognition, the
    ///     capture device is available, and the engine loaded successfully; otherwise
    ///     <see cref="UnavailableSpeechRecognizer.Instance"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/>, <paramref name="store"/>, or
    ///     <paramref name="captureDevice"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Equivalent to calling
    ///     <see cref="Create(IRecognitionModel,string,IAudioCaptureDevice,ISpeechDiagnostics)"/> with
    ///     <c>store.GetCurrentDirectory(model.Id)</c> as the installed-model-directory argument, so
    ///     callers never need to know <see cref="SpeechModelStore"/>'s on-disk directory-naming
    ///     scheme just to compose a recognizer.
    /// </remarks>
    public static ISpeechRecognizer Create(
        IRecognitionModel model,
        SpeechModelStore store,
        IAudioCaptureDevice captureDevice,
        ISpeechDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(store);

        return Create(model, store.GetCurrentDirectory(model.Id), captureDevice, diagnostics);
    }

    /// <summary>
    ///     Creates a speech recognizer for an installed recognition model, resolving the model's
    ///     installed-files directory from the supplied catalog's own store rather than requiring
    ///     the caller to construct a separate <see cref="SpeechModelStore"/>.
    /// </summary>
    /// <param name="model">
    ///     The recognition model to load. Must not be null and must already be installed.
    /// </param>
    /// <param name="catalog">
    ///     The catalog whose <see cref="SpeechModelCatalog.Store"/> resolves
    ///     <paramref name="model"/>'s installed-files directory. Must not be null.
    /// </param>
    /// <param name="captureDevice">
    ///     The capture device to stream audio from, as returned by
    ///     <c>AudioDeviceFactory.CreateCaptureDevice(...)</c>. Must not be null; a device
    ///     reporting <c>IsAvailable == false</c> is treated as "no microphone", not as an error.
    /// </param>
    /// <param name="diagnostics">
    ///     The sink to report structural composition, lifecycle, and fault events to, or
    ///     <see langword="null"/> to use <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <returns>
    ///     A real streaming recognizer when the model is installed, its role is recognition, the
    ///     capture device is available, and the engine loaded successfully; otherwise
    ///     <see cref="UnavailableSpeechRecognizer.Instance"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/>, <paramref name="catalog"/>, or
    ///     <paramref name="captureDevice"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Equivalent to calling
    ///     <see cref="Create(IRecognitionModel,SpeechModelStore,IAudioCaptureDevice,ISpeechDiagnostics)"/>
    ///     with <c>catalog.Store</c> as the store argument, so a host that already owns a
    ///     <see cref="SpeechModelCatalog"/> for enumeration and download can compose a recognizer
    ///     through that same catalog instance, without constructing a second, potentially
    ///     divergent <see cref="SpeechModelStore"/>.
    /// </remarks>
    public static ISpeechRecognizer Create(
        IRecognitionModel model,
        SpeechModelCatalog catalog,
        IAudioCaptureDevice captureDevice,
        ISpeechDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(catalog);

        return Create(model, catalog.Store, captureDevice, diagnostics);
    }

    /// <summary>
    ///     Creates a speech recognizer using an injected engine factory and a model store, for tests
    ///     that need a deterministic engine while still exercising store-based directory resolution.
    /// </summary>
    /// <param name="model">The recognition model to load. Must not be null.</param>
    /// <param name="store">The store to resolve the model's installed-files directory from. Must not be null.</param>
    /// <param name="captureDevice">The capture device to stream audio from. Must not be null.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for the null sink.</param>
    /// <param name="engineFactory">The engine factory to load the model through. Must not be null.</param>
    /// <returns>
    ///     A real streaming recognizer, or <see cref="UnavailableSpeechRecognizer.Instance"/> for
    ///     any honest unavailable state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/>, <paramref name="store"/>,
    ///     <paramref name="captureDevice"/>, or <paramref name="engineFactory"/> is <see langword="null"/>.
    /// </exception>
    internal static ISpeechRecognizer Create(
        IRecognitionModel model,
        SpeechModelStore store,
        IAudioCaptureDevice captureDevice,
        ISpeechDiagnostics? diagnostics,
        IRecognitionEngineFactory engineFactory)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(store);

        return Create(model, store.GetCurrentDirectory(model.Id), captureDevice, diagnostics, engineFactory);
    }

    /// <summary>
    ///     Creates a speech recognizer using an injected engine factory and a model catalog, for
    ///     tests that need a deterministic engine while still exercising catalog-based directory
    ///     resolution.
    /// </summary>
    /// <param name="model">The recognition model to load. Must not be null.</param>
    /// <param name="catalog">The catalog whose store resolves the model's installed-files directory. Must not be null.</param>
    /// <param name="captureDevice">The capture device to stream audio from. Must not be null.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for the null sink.</param>
    /// <param name="engineFactory">The engine factory to load the model through. Must not be null.</param>
    /// <returns>
    ///     A real streaming recognizer, or <see cref="UnavailableSpeechRecognizer.Instance"/> for
    ///     any honest unavailable state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/>, <paramref name="catalog"/>,
    ///     <paramref name="captureDevice"/>, or <paramref name="engineFactory"/> is <see langword="null"/>.
    /// </exception>
    internal static ISpeechRecognizer Create(
        IRecognitionModel model,
        SpeechModelCatalog catalog,
        IAudioCaptureDevice captureDevice,
        ISpeechDiagnostics? diagnostics,
        IRecognitionEngineFactory engineFactory)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(catalog);

        return Create(model, catalog.Store, captureDevice, diagnostics, engineFactory);
    }

    /// <summary>
    ///     Creates a speech recognizer using an injected engine factory, for tests that need a
    ///     deterministic engine with no model files and no native runtime.
    /// </summary>
    /// <param name="model">The recognition model to load. Must not be null.</param>
    /// <param name="installedModelDirectory">The model's installed-files directory.</param>
    /// <param name="captureDevice">The capture device to stream audio from. Must not be null.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for the null sink.</param>
    /// <param name="engineFactory">The engine factory to load the model through. Must not be null.</param>
    /// <returns>
    ///     A real streaming recognizer, or <see cref="UnavailableSpeechRecognizer.Instance"/> for
    ///     any honest unavailable state.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="model"/>, <paramref name="captureDevice"/>, or
    ///     <paramref name="engineFactory"/> is <see langword="null"/>.
    /// </exception>
    internal static ISpeechRecognizer Create(
        IRecognitionModel model,
        string installedModelDirectory,
        IAudioCaptureDevice captureDevice,
        ISpeechDiagnostics? diagnostics,
        IRecognitionEngineFactory engineFactory)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(captureDevice);
        ArgumentNullException.ThrowIfNull(engineFactory);

        var sink = diagnostics ?? NullSpeechDiagnostics.Instance;

        // A model that has not been downloaded yet is the single most common reason recognition
        // is unavailable, and is an ordinary first-run state rather than an error.
        if (string.IsNullOrEmpty(installedModelDirectory) || !Directory.Exists(installedModelDirectory))
        {
            sink.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                $"Speech recognition is unavailable because model '{model.Id}' is not installed.");
            return UnavailableSpeechRecognizer.Instance;
        }

        // Guard against a model whose declared role contradicts the recognition interface it
        // implements: loading it would build a recognizer that could never produce text.
        if (model.Role != SpeechModelRole.Recognition)
        {
            sink.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                $"Speech recognition is unavailable because model '{model.Id}' does not declare the recognition role.");
            return UnavailableSpeechRecognizer.Instance;
        }

        // No microphone (or no working audio backend) is an ordinary machine state too; there is
        // nothing to stream, so report it honestly rather than loading a model that cannot be fed.
        if (!captureDevice.IsAvailable)
        {
            sink.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                "Speech recognition is unavailable because no audio capture device is available.");
            return UnavailableSpeechRecognizer.Instance;
        }

        // Loading is the one step that touches the native runtime, so it is also the one step
        // that can fail for a missing org.k2fsa.sherpa.onnx.runtime.{RID} binary or unusable
        // model files. Both degrade exactly like a missing model rather than crashing start-up.
        IRecognitionEngine engine;
        try
        {
            engine = engineFactory.Create(model, installedModelDirectory);
        }
        catch (Exception ex)
        {
            sink.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Speech recognition is unavailable because the engine for model '{model.Id}' could not be loaded: {ex.Message}");
            return UnavailableSpeechRecognizer.Instance;
        }

        sink.Report(
            SpeechDiagnosticLevel.Info,
            DiagnosticsCategory,
            $"Composed a streaming speech recognizer for model '{model.Id}'.");
        return new SherpaOnnxSpeechRecognizer(
            engine,
            captureDevice,
            model.AudioFormat.SampleRate,
            model,
            sink);
    }
}
