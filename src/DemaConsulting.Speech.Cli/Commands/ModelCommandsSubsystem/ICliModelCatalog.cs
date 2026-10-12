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
    /// <param name="cancellationToken">A token observed before the removal starts.</param>
    /// <returns>A task that completes when the model is removed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> is null, empty, or not a valid directory name.</exception>
    Task UninstallAsync(string modelId, CancellationToken cancellationToken);

    /// <summary>
    ///     Best-effort deletes any leftover partial-install/staging directories for a model.
    /// </summary>
    /// <param name="modelId">The model identifier. Must not be null, empty, or an invalid directory name.</param>
    /// <param name="cancellationToken">A token observed before the cleanup starts.</param>
    /// <returns>A task that completes when the cleanup pass has finished.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> is null, empty, or not a valid directory name.</exception>
    Task CleanUpLeftoversAsync(string modelId, CancellationToken cancellationToken);

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
    ///     Loads a real <see cref="ISpeechSynthesizerEngine"/> for a resolved synthesis-role
    ///     model descriptor, with no playback device bound yet.
    /// </summary>
    /// <remarks>
    ///     Loading is the expensive step; callers should load one engine per model/parameter
    ///     combination and reuse it to create sessions (via
    ///     <see cref="ISpeechSynthesizerEngine.CreateSessionAsync"/>) rather than reloading per
    ///     turn - mirroring <c>ISpeechSynthesizerEngine</c>'s own guidance.
    /// </remarks>
    /// <param name="descriptor">
    ///     The descriptor identifying the model to load. Must not be null and must describe a
    ///     synthesis-role model.
    /// </param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag resolved from <c>--param</c>, or
    ///     <see langword="null"/> to use every model's own default parameter values.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation of the load.</param>
    /// <returns>
    ///     A task that completes with a real engine when the model is installed and its role is
    ///     synthesis; otherwise an honestly unavailable engine per <see cref="SpeechSynthesizerFactory"/>'s
    ///     own contract.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="descriptor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="descriptor"/> does not describe a synthesis-role model, or
    ///     when <paramref name="parameterValues"/> contains an invalid value for a parameter the
    ///     model declares.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled before loading completes.</exception>
    Task<ISpeechSynthesizerEngine> CreateSynthesizerEngineAsync(
        SpeechModelDescriptor descriptor,
        IReadOnlyDictionary<string, object>? parameterValues,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads a real <see cref="ISpeechRecognizerEngine"/> for a resolved recognition-role
    ///     model descriptor, with no capture device bound yet.
    /// </summary>
    /// <remarks>
    ///     Loading is the expensive step; callers should load one engine per model/parameter
    ///     combination and reuse it to create sessions (via
    ///     <see cref="ISpeechRecognizerEngine.CreateSessionAsync"/>) rather than reloading per
    ///     turn - mirroring <c>ISpeechRecognizerEngine</c>'s own guidance.
    /// </remarks>
    /// <param name="descriptor">
    ///     The descriptor identifying the model to load. Must not be null and must describe a
    ///     recognition-role model.
    /// </param>
    /// <param name="parameterValues">
    ///     An optional session-level parameter value bag resolved from <c>--param</c>, or
    ///     <see langword="null"/> to use every model's own default parameter values.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation of the load.</param>
    /// <returns>
    ///     A task that completes with a real engine when the model is installed and its role is
    ///     recognition; otherwise an honestly unavailable engine per <see cref="SpeechRecognizerFactory"/>'s
    ///     own contract.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="descriptor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="descriptor"/> does not describe a recognition-role model, or
    ///     when <paramref name="parameterValues"/> contains an invalid value for a parameter the
    ///     model declares.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled before loading completes.</exception>
    Task<ISpeechRecognizerEngine> CreateRecognizerEngineAsync(
        SpeechModelDescriptor descriptor,
        IReadOnlyDictionary<string, object>? parameterValues,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the reference-voice packs this catalog can install.
    /// </summary>
    IReadOnlyList<ReferenceVoicePack> VoicePacks { get; }

    /// <summary>
    ///     Gets the folder where the user's own reference-voice files are kept (<c>.wav</c>/<c>.flac</c>).
    /// </summary>
    string UserVoicesDirectory { get; }

    /// <summary>
    ///     Enumerates every available reference voice (the <c>--voices-dir</c> folder, the user folder, and
    ///     installed packs), re-reading the file system on every call.
    /// </summary>
    /// <returns>The voices in precedence order. Never null; may be empty.</returns>
    IReadOnlyList<ReferenceVoiceInfo> EnumerateVoices();

    /// <summary>
    ///     Gets the install state of a reference-voice pack.
    /// </summary>
    /// <param name="packId">The pack identifier.</param>
    /// <returns>The pack's current state.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> matches no known pack.</exception>
    SpeechModelState GetVoicePackState(string packId);

    /// <summary>
    ///     Determines whether an installed reference-voice pack was extracted with its emotion (mood) clips.
    /// </summary>
    /// <param name="packId">The pack identifier.</param>
    /// <param name="cancellationToken">A token that aborts the read.</param>
    /// <returns>A task whose result is <see langword="true"/> when the pack is installed with emotion clips; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> matches no known pack.</exception>
    Task<bool> VoicePackHasEmotionsAsync(string packId, CancellationToken cancellationToken);

    /// <summary>
    ///     Downloads, verifies, and installs a reference-voice pack.
    /// </summary>
    /// <param name="packId">The pack identifier.</param>
    /// <param name="includeEmotions"><see langword="true"/> to also extract the pack's emotion (mood) clips (an installed voices-only pack is upgraded); <see langword="false"/> for the base voices only.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">A token that aborts the download.</param>
    /// <returns>The outcome of the install attempt.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> matches no known pack.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    Task<SpeechModelDownloadResult> InstallVoicePackAsync(
        string packId,
        bool includeEmotions,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Removes an installed reference-voice pack (a pack that is not installed is a no-op).
    /// </summary>
    /// <param name="packId">The pack identifier.</param>
    /// <param name="cancellationToken">A token that aborts the wait for an in-flight install of the same pack.</param>
    /// <returns>A task that completes when the pack is removed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> matches no known pack.</exception>
    Task UninstallVoicePackAsync(string packId, CancellationToken cancellationToken);
}
