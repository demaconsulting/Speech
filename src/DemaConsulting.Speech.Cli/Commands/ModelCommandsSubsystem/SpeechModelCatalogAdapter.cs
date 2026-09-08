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
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;

/// <summary>
///     Production <see cref="ICliModelCatalog"/> implementation backed by a real
///     <see cref="SpeechModelCatalog"/>.
/// </summary>
/// <remarks>
///     This adapter forwards every call to the library catalog (and, for the store-only
///     operations <see cref="Uninstall"/>/<see cref="CleanUpLeftovers"/>, to
///     <see cref="SpeechModelCatalog.Store"/>) unchanged. Owns the composed catalog and disposes
///     it when this adapter is disposed.
/// </remarks>
internal sealed class SpeechModelCatalogAdapter : ICliModelCatalog, IDisposable
{
    /// <summary>The real library catalog every call is forwarded to.</summary>
    private readonly SpeechModelCatalog _catalog;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelCatalogAdapter"/> class,
    ///     composing a real <see cref="SpeechModelCatalog"/>.
    /// </summary>
    /// <param name="options">
    ///     Optional host-configured storage options (for example honoring the CLI's
    ///     <c>--models-dir</c> override), or <see langword="null"/> to use the library's default
    ///     per-user storage root.
    /// </param>
    public SpeechModelCatalogAdapter(SpeechModelStoreOptions? options = null)
    {
        _catalog = new SpeechModelCatalog(options);
    }

    /// <inheritdoc/>
    public IReadOnlyList<SpeechModelDescriptor> Enumerate() => _catalog.Enumerate();

    /// <inheritdoc/>
    public Task<SpeechModelDownloadResult> DownloadAsync(
        string modelId,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken) =>
        _catalog.DownloadAsync(modelId, progress, cancellationToken);

    /// <inheritdoc/>
    public void Uninstall(string modelId) => _catalog.Store.Uninstall(modelId);

    /// <inheritdoc/>
    public void CleanUpLeftovers(string modelId) => _catalog.Store.CleanUpLeftovers(modelId);

    /// <inheritdoc/>
    public AudioFormat GetPreferredAudioFormat(SpeechModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return RequireSynthesisModel(descriptor).PreferredAudioFormat;
    }

    /// <inheritdoc/>
    public ISpeechSynthesizer CreateSynthesizer(
        SpeechModelDescriptor descriptor,
        IAudioPlaybackDevice playbackDevice,
        IReadOnlyDictionary<string, object>? parameterValues)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(playbackDevice);

        var synthesisModel = RequireSynthesisModel(descriptor);
        return SpeechSynthesizerFactory.Create(synthesisModel, _catalog, playbackDevice, null, parameterValues);
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
    public AudioFormat GetAudioFormat(SpeechModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return RequireRecognitionModel(descriptor).AudioFormat;
    }

    /// <inheritdoc/>
    public ISpeechRecognizer CreateRecognizer(
        SpeechModelDescriptor descriptor,
        IAudioCaptureDevice captureDevice,
        IReadOnlyDictionary<string, object>? parameterValues)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(captureDevice);

        var recognitionModel = RequireRecognitionModel(descriptor);
        return SpeechRecognizerFactory.Create(recognitionModel, _catalog, captureDevice, null, parameterValues);
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
    public void Dispose() => _catalog.Dispose();
}
