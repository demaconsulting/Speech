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
///     CLI-owned seam over the library's speech-model catalog and store surface, letting each
///     model-management command's own parsing/formatting logic be unit tested against a
///     controlled, in-memory fake instead of the library's real, sealed
///     <see cref="SpeechModelCatalog"/>/<see cref="SpeechModelStore"/> types.
/// </summary>
/// <remarks>
///     Mirrors the same "demo-owned seam over a sealed library type" pattern
///     <c>DemaConsulting.Speech.Demo</c>'s own <c>IModelCatalogService</c> establishes for the
///     desktop demo application. The production implementation
///     (<see cref="SpeechModelCatalogAdapter"/>) forwards every call unchanged to a real
///     <see cref="SpeechModelCatalog"/>; it adds no public API to
///     <c>DemaConsulting.Speech</c>.
/// </remarks>
internal interface ICliModelCatalog
{
    /// <summary>
    ///     Enumerates every known model alongside its current install state.
    /// </summary>
    /// <returns>A snapshot list of model descriptors. Never null; may be empty.</returns>
    IReadOnlyList<SpeechModelDescriptor> Enumerate();

    /// <summary>
    ///     Downloads, verifies, and atomically installs a known model.
    /// </summary>
    /// <param name="modelId">The identifier of a model present in the list returned by <see cref="Enumerate"/>.</param>
    /// <param name="progress">An optional progress sink invoked as the model's file(s) transfer.</param>
    /// <param name="cancellationToken">A token that, when canceled, aborts the in-progress download.</param>
    /// <returns>The outcome describing whether the model was installed, or the honest failure reason when it was not.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> matches no known model.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    Task<SpeechModelDownloadResult> DownloadAsync(
        string modelId,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Removes a model's installed content, manifest, and any leftover scratch directories.
    /// </summary>
    /// <param name="modelId">The model identifier. Must not be null, empty, or an invalid directory name.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> is null, empty, or not a valid directory name.</exception>
    void Uninstall(string modelId);

    /// <summary>
    ///     Best-effort deletes any leftover partial-install/staging directories for a model.
    /// </summary>
    /// <param name="modelId">The model identifier. Must not be null, empty, or an invalid directory name.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> is null, empty, or not a valid directory name.</exception>
    void CleanUpLeftovers(string modelId);

    /// <summary>
    ///     Resolves the preferred playback audio format for a synthesis-role model descriptor,
    ///     used to size a <see cref="WavFileAudioPlaybackDevice"/> before a synthesizer is
    ///     constructed.
    /// </summary>
    /// <param name="descriptor">
    ///     The descriptor to resolve the preferred format for. Must not be null and must describe
    ///     a synthesis-role model.
    /// </param>
    /// <returns>The model's preferred playback audio format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="descriptor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="descriptor"/> does not describe a synthesis-role model.</exception>
    AudioFormat GetPreferredAudioFormat(SpeechModelDescriptor descriptor);

    /// <summary>
    ///     Constructs a real <see cref="ISpeechSynthesizer"/> for a resolved synthesis-role model
    ///     descriptor, playing back through the supplied device.
    /// </summary>
    /// <param name="descriptor">
    ///     The descriptor identifying the model to load. Must not be null and must describe a
    ///     synthesis-role model.
    /// </param>
    /// <param name="playbackDevice">The playback device to speak through. Must not be null.</param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag resolved from <c>--param</c>, or
    ///     <see langword="null"/> to use every model's own default parameter values.
    /// </param>
    /// <returns>
    ///     A real synthesizer when the model is installed, its role is synthesis, and the
    ///     playback device is available; otherwise an honestly unavailable synthesizer per
    ///     <see cref="SpeechSynthesizerFactory.Create(ISynthesisModel,SpeechModelCatalog,IAudioPlaybackDevice,DemaConsulting.Speech.Diagnostics.ISpeechDiagnostics?,IReadOnlyDictionary{string,object}?)"/>'s
    ///     own contract.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="descriptor"/> or <paramref name="playbackDevice"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="descriptor"/> does not describe a synthesis-role model, or
    ///     when <paramref name="parameterValues"/> contains an invalid value for a parameter the
    ///     model declares.
    /// </exception>
    ISpeechSynthesizer CreateSynthesizer(
        SpeechModelDescriptor descriptor,
        IAudioPlaybackDevice playbackDevice,
        IReadOnlyDictionary<string, object>? parameterValues);

    /// <summary>
    ///     Resolves the mono audio format a recognition-role model descriptor's engine requires
    ///     its input audio to be supplied at, symmetric to <see cref="GetPreferredAudioFormat"/>
    ///     on the synthesis side.
    /// </summary>
    /// <param name="descriptor">
    ///     The descriptor to resolve the required format for. Must not be null and must describe
    ///     a recognition-role model.
    /// </param>
    /// <returns>The model's required capture audio format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="descriptor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="descriptor"/> does not describe a recognition-role model.</exception>
    AudioFormat GetAudioFormat(SpeechModelDescriptor descriptor);

    /// <summary>
    ///     Constructs a real <see cref="ISpeechRecognizer"/> for a resolved recognition-role model
    ///     descriptor, streaming audio from the supplied capture device.
    /// </summary>
    /// <param name="descriptor">
    ///     The descriptor identifying the model to load. Must not be null and must describe a
    ///     recognition-role model.
    /// </param>
    /// <param name="captureDevice">The capture device to stream audio from. Must not be null.</param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag resolved from <c>--param</c>, or
    ///     <see langword="null"/> to use every model's own default parameter values.
    /// </param>
    /// <returns>
    ///     A real recognizer when the model is installed, its role is recognition, and the
    ///     capture device is available; otherwise an honestly unavailable recognizer per
    ///     <see cref="SpeechRecognizerFactory.Create(IRecognitionModel,SpeechModelCatalog,IAudioCaptureDevice,DemaConsulting.Speech.Diagnostics.ISpeechDiagnostics?,IReadOnlyDictionary{string,object}?)"/>'s
    ///     own contract.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="descriptor"/> or <paramref name="captureDevice"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="descriptor"/> does not describe a recognition-role model, or
    ///     when <paramref name="parameterValues"/> contains an invalid value for a parameter the
    ///     model declares.
    /// </exception>
    ISpeechRecognizer CreateRecognizer(
        SpeechModelDescriptor descriptor,
        IAudioCaptureDevice captureDevice,
        IReadOnlyDictionary<string, object>? parameterValues);
}
