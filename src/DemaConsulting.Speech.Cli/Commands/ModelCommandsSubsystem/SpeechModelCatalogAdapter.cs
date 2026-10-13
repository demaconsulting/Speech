// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo;
using DemaConsulting.Speech.Onnx.Kokoro;
using DemaConsulting.Speech.Onnx.NemotronStt;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Sherpa;
using DemaConsulting.Speech.SynthesisSubsystem;
using ChatterboxTurboModels = DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;

/// <summary>
///     Production <see cref="ICliModelCatalog"/> implementation backed by a real
///     <see cref="SpeechModelCatalog"/>.
/// </summary>
/// <remarks>
///     This adapter forwards every call to the library catalog (and, for the store-only
///     operations <see cref="UninstallAsync"/>/<see cref="CleanUpLeftoversAsync"/>, to
///     <see cref="SpeechModelCatalog.Store"/>) unchanged. Owns the composed catalog and disposes
///     it when this adapter is disposed.
/// </remarks>
internal sealed class SpeechModelCatalogAdapter : ICliModelCatalog, IDisposable
{
    /// <summary>The real library catalog every call is forwarded to.</summary>
    private readonly SpeechModelCatalog _catalog;

    /// <summary>The reference-voice library shared by the Chatterbox model and the voice commands.</summary>
    private readonly ReferenceVoiceLibrary _voiceLibrary;

    /// <summary>The extra <c>--voices-dir</c> folder, or <see langword="null"/>.</summary>
    private readonly string? _voicesDir;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelCatalogAdapter"/> class,
    ///     composing a real <see cref="SpeechModelCatalog"/>.
    /// </summary>
    /// <param name="options">
    ///     Optional host-configured storage options (for example honoring the CLI's
    ///     <c>--models-dir</c> override), or <see langword="null"/> to use the library's default
    ///     per-user storage root.
    /// </param>
    /// <param name="downloaderOptions">
    ///     Optional host-configured download options (for example honoring the CLI's
    ///     <c>--mirror-*</c> overrides), or <see langword="null"/> to download every model from
    ///     its own declared public URI.
    /// </param>
    /// <param name="voicesDir">
    ///     Optional extra reference-voice directory (the CLI's <c>--voices-dir</c>), or
    ///     <see langword="null"/> for only the user folder and installed packs.
    /// </param>
    public SpeechModelCatalogAdapter(
        SpeechModelStoreOptions? options = null,
        SpeechModelDownloaderOptions? downloaderOptions = null,
        string? voicesDir = null)
    {
        _voicesDir = voicesDir;
        var catalog = new SpeechModelCatalog(options, downloaderOptions, diagnostics: null)
            .AddSherpaModels()
            .AddKokoroModels()
            .AddNemotronSttModels();
        ReferenceVoiceLibrary? voiceLibrary = null;

        try
        {
            // One library instance serves both the Chatterbox model (voice list read now) and the
            // voice commands (install/uninstall/enumerate), so they share the same store and mirror.
            voiceLibrary = new ReferenceVoiceLibrary(catalog.Store, downloaderOptions);
            catalog.AddChatterboxTurboModels(new ChatterboxTurboModels.ChatterboxTurboModelOptions
            {
                ReferenceVoiceDirectory = voicesDir,
                ReferenceVoiceLibrary = voiceLibrary
            });

            _catalog = catalog;
            _voiceLibrary = voiceLibrary;
        }
        catch
        {
            // Composition failed part-way: release what was built, since no caller will receive
            // this instance to dispose
            voiceLibrary?.Dispose();
            catalog.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<ReferenceVoicePack> VoicePacks => ReferenceVoiceLibrary.Packs;

    /// <inheritdoc/>
    public string UserVoicesDirectory => _voiceLibrary.UserDirectory;

    /// <inheritdoc/>
    public IReadOnlyList<ReferenceVoiceInfo> EnumerateVoices() =>
        _voiceLibrary.EnumerateVoices(string.IsNullOrEmpty(_voicesDir) ? null : [_voicesDir]);

    /// <inheritdoc/>
    public SpeechModelState GetVoicePackState(string packId) => _voiceLibrary.GetPackState(packId);

    /// <inheritdoc/>
    public Task<bool> VoicePackHasEmotionsAsync(string packId, CancellationToken cancellationToken) =>
        _voiceLibrary.PackHasEmotionsAsync(packId, cancellationToken);

    /// <inheritdoc/>
    public Task<SpeechModelDownloadResult> InstallVoicePackAsync(
        string packId,
        bool includeEmotions,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken) =>
        _voiceLibrary.InstallPackAsync(packId, includeEmotions, progress, cancellationToken);

    /// <inheritdoc/>
    public Task UninstallVoicePackAsync(string packId, CancellationToken cancellationToken) =>
        _voiceLibrary.UninstallPackAsync(packId, cancellationToken);

    /// <inheritdoc/>
    public IReadOnlyList<SpeechModelDescriptor> Enumerate() => _catalog.Enumerate();

    /// <inheritdoc/>
    public Task<SpeechModelDownloadResult> DownloadAsync(
        string modelId,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken) =>
        _catalog.DownloadAsync(modelId, progress, cancellationToken);

    /// <inheritdoc/>
    public Task UninstallAsync(string modelId, CancellationToken cancellationToken) =>
        _catalog.Store.UninstallAsync(modelId, cancellationToken);

    /// <inheritdoc/>
    public Task CleanUpLeftoversAsync(string modelId, CancellationToken cancellationToken) =>
        _catalog.Store.CleanUpLeftoversAsync(modelId, cancellationToken);

    /// <inheritdoc/>
    public AudioFormat GetPreferredAudioFormat(SpeechModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return RequireSynthesisModel(descriptor).PreferredAudioFormat;
    }

    /// <inheritdoc/>
    public Task<ISpeechSynthesizerEngine> CreateSynthesizerEngineAsync(
        SpeechModelDescriptor descriptor,
        IReadOnlyDictionary<string, object>? parameterValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var synthesisModel = RequireSynthesisModel(descriptor);
        return SpeechSynthesizerFactory.LoadAsync(synthesisModel, _catalog, null, parameterValues, cancellationToken);
    }

    /// <summary>
    ///     Resolves a descriptor's model to <see cref="ISynthesisModel"/>, throwing a clean
    ///     <see cref="ArgumentException"/> when it is not one.
    /// </summary>
    /// <remarks>
    ///     This branch is defensive-only in production: <c>SpeakCommand</c> validates
    ///     <c>descriptor.Role == SpeechModelRole.Synthesis</c> itself before ever calling either
    ///     new member, so it should be unreachable through the CLI's own dispatch, but is
    ///     retained because <see cref="ICliModelCatalog"/> is an interface any future caller could
    ///     misuse.
    /// </remarks>
    private static ISynthesisModel RequireSynthesisModel(SpeechModelDescriptor descriptor)
    {
        if (descriptor.Model is not ISynthesisModel synthesisModel)
        {
            throw new ArgumentException(
                $"Model '{descriptor.Id}' is not a synthesis model.",
                nameof(descriptor));
        }

        return synthesisModel;
    }

    /// <inheritdoc/>
    public Task<ISpeechRecognizerEngine> CreateRecognizerEngineAsync(
        SpeechModelDescriptor descriptor,
        IReadOnlyDictionary<string, object>? parameterValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var recognitionModel = RequireRecognitionModel(descriptor);
        return SpeechRecognizerFactory.LoadAsync(recognitionModel, _catalog, null, parameterValues, cancellationToken);
    }

    /// <summary>
    ///     Resolves a descriptor's model to <see cref="IRecognitionModel"/>, throwing a clean
    ///     <see cref="ArgumentException"/> when it is not one.
    /// </summary>
    /// <remarks>
    ///     This branch is defensive-only in production: <c>RecognizeCommand</c> validates
    ///     <c>descriptor.Role == SpeechModelRole.Recognition</c> itself before ever calling either
    ///     new member, so it should be unreachable through the CLI's own dispatch, but is
    ///     retained because <see cref="ICliModelCatalog"/> is an interface any future caller could
    ///     misuse.
    /// </remarks>
    private static IRecognitionModel RequireRecognitionModel(SpeechModelDescriptor descriptor)
    {
        if (descriptor.Model is not IRecognitionModel recognitionModel)
        {
            throw new ArgumentException(
                $"Model '{descriptor.Id}' is not a recognition model.",
                nameof(descriptor));
        }

        return recognitionModel;
    }

    /// <summary>
    ///     Disposes the underlying <see cref="SpeechModelCatalog"/>.
    /// </summary>
    public void Dispose()
    {
        _voiceLibrary.Dispose();
        _catalog.Dispose();
    }
}
