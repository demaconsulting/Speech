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
using DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;

/// <summary>
///     Deterministic, in-memory fake <see cref="ICliModelCatalog"/> used by every
///     model-management command's unit tests, so no test depends on a real, sealed
///     <see cref="SpeechModelCatalog"/>/<see cref="SpeechModelStore"/> or on network access.
/// </summary>
internal sealed class FakeCliModelCatalog : ICliModelCatalog
{
    /// <summary>The descriptors this fake enumerates, keyed by model id for fast lookup/mutation.</summary>
    private readonly Dictionary<string, SpeechModelDescriptor> _descriptors = new(StringComparer.Ordinal);

    /// <summary>Gets the ordered list of model ids passed to <see cref="UninstallAsync"/>.</summary>
    public List<string> UninstallCalls { get; } = [];

    /// <summary>Gets the ordered list of model ids passed to <see cref="CleanUpLeftoversAsync"/>.</summary>
    public List<string> CleanUpLeftoversCalls { get; } = [];

    /// <summary>Gets the ordered list of model ids passed to <see cref="DownloadAsync"/>.</summary>
    public List<string> DownloadCalls { get; } = [];

    /// <summary>
    ///     Gets or sets a factory producing the result <see cref="DownloadAsync"/> returns for a
    ///     given model id, or <see langword="null"/> to always return
    ///     <see cref="SpeechModelDownloadOutcome.Installed"/>.
    /// </summary>
    public Func<string, SpeechModelDownloadResult>? DownloadResultFactory { get; set; }

    /// <summary>
    ///     Gets or sets an action invoked with the progress sink supplied to
    ///     <see cref="DownloadAsync"/>, letting a test simulate progress reports.
    /// </summary>
    public Action<string, IProgress<SpeechModelDownloadProgress>?>? OnDownload { get; set; }

    /// <summary>Gets or sets an exception to throw from <see cref="UninstallAsync"/>, or <see langword="null"/> for none.</summary>
    public Exception? UninstallException { get; set; }

    /// <summary>Gets or sets an exception <see cref="CleanUpLeftoversAsync"/> faults with, or null.</summary>
    public Exception? CleanUpException { get; set; }

    /// <summary>Gets or sets an exception to throw from <see cref="DownloadAsync"/> before any result is produced.</summary>
    public Exception? DownloadException { get; set; }

    /// <summary>Gets or sets an exception to throw from <see cref="Enumerate"/>.</summary>
    public Exception? EnumerateException { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether <see cref="VoicePackHasEmotionsAsync"/> throws
    ///     <see cref="OperationCanceledException"/> bound to the supplied token (simulating Ctrl+C mid-call).
    /// </summary>
    public bool HasEmotionsThrowsCanceled { get; set; }

    /// <summary>
    ///     Adds or replaces a model descriptor this fake enumerates.
    /// </summary>
    public FakeCliModelCatalog WithModel(ISpeechModel model, SpeechModelState state)
    {
        _descriptors[model.Id] = new SpeechModelDescriptor(model, state);
        return this;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SpeechModelDescriptor> Enumerate() =>
        EnumerateException is null ? _descriptors.Values.ToList() : throw EnumerateException;

    /// <inheritdoc/>
    public Task<SpeechModelDownloadResult> DownloadAsync(
        string modelId,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        DownloadCalls.Add(modelId);
        cancellationToken.ThrowIfCancellationRequested();

        if (DownloadException is not null)
        {
            throw DownloadException;
        }

        OnDownload?.Invoke(modelId, progress);

        // Re-checked after the hook above (not just before it) so a test can simulate a genuine
        // in-progress cancellation - one that happens only after this model's download has
        // demonstrably started - by canceling the token from within OnDownload itself, rather
        // than only ever exercising the pre-start cancellation check above.
        cancellationToken.ThrowIfCancellationRequested();

        if (!_descriptors.ContainsKey(modelId))
        {
            throw new ArgumentException(
                $"'{modelId}' does not match any model in this catalog's known-model list.",
                nameof(modelId));
        }

        var result = DownloadResultFactory?.Invoke(modelId) ?? new SpeechModelDownloadResult(SpeechModelDownloadOutcome.Installed);
        if (result.Outcome == SpeechModelDownloadOutcome.Installed && _descriptors.TryGetValue(modelId, out var descriptor))
        {
            _descriptors[modelId] = new SpeechModelDescriptor(descriptor.Model, SpeechModelState.Downloaded);
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task UninstallAsync(string modelId, CancellationToken cancellationToken)
    {
        UninstallCalls.Add(modelId);
        if (UninstallException is not null)
        {
            return Task.FromException(UninstallException);
        }

        if (_descriptors.TryGetValue(modelId, out var descriptor))
        {
            _descriptors[modelId] = new SpeechModelDescriptor(descriptor.Model, SpeechModelState.NotDownloaded);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task CleanUpLeftoversAsync(string modelId, CancellationToken cancellationToken)
    {
        CleanUpLeftoversCalls.Add(modelId);
        return CleanUpException is not null ? Task.FromException(CleanUpException) : Task.CompletedTask;
    }

    /// <summary>
    ///     Gets or sets the delegate <see cref="GetPreferredAudioFormat"/> forwards to, or
    ///     <see langword="null"/> to throw a clear "not configured" exception if a test forgets
    ///     to set it and the code path is reached.
    /// </summary>
    public Func<SpeechModelDescriptor, AudioFormat>? GetPreferredAudioFormatOverride { get; set; }

    /// <summary>
    ///     Gets or sets the delegate <see cref="CreateSynthesizerEngineAsync"/> forwards to, or
    ///     <see langword="null"/> to throw a clear "not configured" exception if a test forgets
    ///     to set it and the code path is reached.
    /// </summary>
    public Func<SpeechModelDescriptor, IReadOnlyDictionary<string, object>?, CancellationToken, Task<ISpeechSynthesizerEngine>>?
        CreateSynthesizerEngineOverride
    { get; set; }

    /// <summary>Gets the ordered list of parameter value bags passed to <see cref="CreateSynthesizerEngineAsync"/>.</summary>
    public List<IReadOnlyDictionary<string, object>?> CreateSynthesizerParameterValueCalls { get; } = [];

    /// <inheritdoc/>
    public AudioFormat GetPreferredAudioFormat(SpeechModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return GetPreferredAudioFormatOverride?.Invoke(descriptor)
            ?? throw new InvalidOperationException(
                $"{nameof(FakeCliModelCatalog)}.{nameof(GetPreferredAudioFormatOverride)} was not configured for this test.");
    }

    /// <inheritdoc/>
    public Task<ISpeechSynthesizerEngine> CreateSynthesizerEngineAsync(
        SpeechModelDescriptor descriptor,
        IReadOnlyDictionary<string, object>? parameterValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        CreateSynthesizerParameterValueCalls.Add(parameterValues);

        return CreateSynthesizerEngineOverride?.Invoke(descriptor, parameterValues, cancellationToken)
            ?? throw new InvalidOperationException(
                $"{nameof(FakeCliModelCatalog)}.{nameof(CreateSynthesizerEngineOverride)} was not configured for this test.");
    }

    /// <summary>
    ///     Gets or sets the delegate <see cref="CreateRecognizerEngineAsync"/> forwards to, or
    ///     <see langword="null"/> to throw a clear "not configured" exception if a test forgets
    ///     to set it and the code path is reached.
    /// </summary>
    public Func<SpeechModelDescriptor, IReadOnlyDictionary<string, object>?, CancellationToken, Task<ISpeechRecognizerEngine>>?
        CreateRecognizerEngineOverride
    { get; set; }

    /// <summary>Gets the ordered list of parameter value bags passed to <see cref="CreateRecognizerEngineAsync"/>.</summary>
    public List<IReadOnlyDictionary<string, object>?> CreateRecognizerParameterValueCalls { get; } = [];

    /// <inheritdoc/>
    public Task<ISpeechRecognizerEngine> CreateRecognizerEngineAsync(
        SpeechModelDescriptor descriptor,
        IReadOnlyDictionary<string, object>? parameterValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        CreateRecognizerParameterValueCalls.Add(parameterValues);

        return CreateRecognizerEngineOverride?.Invoke(descriptor, parameterValues, cancellationToken)
            ?? throw new InvalidOperationException(
                $"{nameof(FakeCliModelCatalog)}.{nameof(CreateRecognizerEngineOverride)} was not configured for this test.");
    }

    /// <summary>Gets the voices <see cref="EnumerateVoices"/> returns; tests add to it.</summary>
    public List<ReferenceVoiceInfo> Voices { get; } = [];

    /// <summary>Gets or sets the user voices directory this fake reports.</summary>
    public string UserVoicesDirectoryValue { get; set; } = "user-voices";

    /// <summary>Gets the per-pack state this fake reports (missing = NotDownloaded).</summary>
    public Dictionary<string, SpeechModelState> PackStates { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the ordered pack ids passed to <see cref="InstallVoicePackAsync"/>.</summary>
    public List<string> InstallPackCalls { get; } = [];

    /// <summary>Gets the ordered <c>includeEmotions</c> values passed to <see cref="InstallVoicePackAsync"/>.</summary>
    public List<bool> InstallEmotionsCalls { get; } = [];

    /// <summary>Gets the pack ids this fake reports as installed with emotion clips.</summary>
    public HashSet<string> PacksWithEmotions { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the ordered pack ids passed to <see cref="UninstallVoicePackAsync"/>.</summary>
    public List<string> UninstallPackCalls { get; } = [];

    /// <summary>Gets or sets the outcome <see cref="InstallVoicePackAsync"/> returns.</summary>
    public SpeechModelDownloadResult InstallPackResult { get; set; } = new(SpeechModelDownloadOutcome.Installed);

    /// <summary>Gets or sets an action invoked with the progress sink during an install.</summary>
    public Action<IProgress<SpeechModelDownloadProgress>?>? OnInstallPack { get; set; }

    /// <summary>Gets or sets an exception thrown by <see cref="UninstallVoicePackAsync"/>.</summary>
    public Exception? UninstallPackException { get; set; }

    /// <inheritdoc/>
    public IReadOnlyList<ReferenceVoicePack> VoicePacks => ReferenceVoicePack.All;

    /// <inheritdoc/>
    public string UserVoicesDirectory => UserVoicesDirectoryValue;

    /// <inheritdoc/>
    public IReadOnlyList<ReferenceVoiceInfo> EnumerateVoices() => Voices.ToList();

    /// <inheritdoc/>
    public SpeechModelState GetVoicePackState(string packId)
    {
        if (!VoicePacks.Any(pack => pack.Id == packId))
        {
            throw new ArgumentException($"'{packId}' is not a known reference-voice pack.", nameof(packId));
        }

        return PackStates.GetValueOrDefault(packId, SpeechModelState.NotDownloaded);
    }

    /// <inheritdoc/>
    public Task<bool> VoicePackHasEmotionsAsync(string packId, CancellationToken cancellationToken)
    {
        if (HasEmotionsThrowsCanceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        return Task.FromResult(PacksWithEmotions.Contains(packId));
    }

    /// <inheritdoc/>
    public Task<SpeechModelDownloadResult> InstallVoicePackAsync(
        string packId,
        bool includeEmotions,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        InstallPackCalls.Add(packId);
        InstallEmotionsCalls.Add(includeEmotions);
        cancellationToken.ThrowIfCancellationRequested();
        OnInstallPack?.Invoke(progress);
        cancellationToken.ThrowIfCancellationRequested();
        if (InstallPackResult.Outcome == SpeechModelDownloadOutcome.Installed)
        {
            PackStates[packId] = SpeechModelState.Downloaded;
            if (includeEmotions)
            {
                PacksWithEmotions.Add(packId);
            }
        }

        return Task.FromResult(InstallPackResult);
    }

    /// <inheritdoc/>
    public Task UninstallVoicePackAsync(string packId, CancellationToken cancellationToken)
    {
        UninstallPackCalls.Add(packId);
        if (UninstallPackException is not null)
        {
            return Task.FromException(UninstallPackException);
        }

        PackStates[packId] = SpeechModelState.NotDownloaded;
        return Task.CompletedTask;
    }
}
