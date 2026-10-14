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

namespace DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;

/// <summary>
///     Minimal <see cref="ISynthesisModel"/> fake that is never installed, so loading it through the
///     seam yields the library's unavailable engine without any native runtime.
/// </summary>
internal sealed class FakeSynthesisModel : ISynthesisModel
{
    private readonly FakeSpeechModel _inner;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeSynthesisModel"/> class.
    /// </summary>
    /// <param name="id">The model identifier.</param>
    /// <param name="preferredAudioFormat">The preferred playback format to report.</param>
    public FakeSynthesisModel(string id, AudioFormat preferredAudioFormat)
    {
        _inner = new FakeSpeechModel(id, role: SpeechModelRole.Synthesis);
        PreferredAudioFormat = preferredAudioFormat;
    }

    /// <inheritdoc/>
    public string Id => _inner.Id;

    /// <inheritdoc/>
    public string DisplayName => _inner.DisplayName;

    /// <inheritdoc/>
    public SpeechModelRole Role => _inner.Role;

    /// <inheritdoc/>
    public IReadOnlyList<ISpeechModelParameter> Parameters => _inner.Parameters;

    /// <inheritdoc/>
    public SpeechModelAudioTagSupport AudioTagSupport => _inner.AudioTagSupport;

    /// <inheritdoc/>
    public SpeechModelDownloadDescriptor DownloadDescriptor => _inner.DownloadDescriptor;

    /// <inheritdoc/>
    public string LicenseName => _inner.LicenseName;

    /// <inheritdoc/>
    public Uri? LicenseUrl => _inner.LicenseUrl;

    /// <inheritdoc/>
    public AudioFormat PreferredAudioFormat { get; }

    /// <inheritdoc/>
    public ISynthesisBackend CreateBackend(string installedModelDirectory) =>
        throw new NotSupportedException("The fake synthesis model has no backend.");
}

/// <summary>
///     Minimal <see cref="IRecognitionModel"/> fake that is never installed, so loading it through the
///     seam yields the library's unavailable engine without any native runtime.
/// </summary>
internal sealed class FakeRecognitionModel : IRecognitionModel
{
    private readonly FakeSpeechModel _inner;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeRecognitionModel"/> class.
    /// </summary>
    /// <param name="id">The model identifier.</param>
    /// <param name="audioFormat">The required input format to report.</param>
    public FakeRecognitionModel(string id, AudioFormat audioFormat)
    {
        _inner = new FakeSpeechModel(id, role: SpeechModelRole.Recognition);
        AudioFormat = audioFormat;
    }

    /// <inheritdoc/>
    public string Id => _inner.Id;

    /// <inheritdoc/>
    public string DisplayName => _inner.DisplayName;

    /// <inheritdoc/>
    public SpeechModelRole Role => _inner.Role;

    /// <inheritdoc/>
    public IReadOnlyList<ISpeechModelParameter> Parameters => _inner.Parameters;

    /// <inheritdoc/>
    public SpeechModelAudioTagSupport AudioTagSupport => _inner.AudioTagSupport;

    /// <inheritdoc/>
    public SpeechModelDownloadDescriptor DownloadDescriptor => _inner.DownloadDescriptor;

    /// <inheritdoc/>
    public string LicenseName => _inner.LicenseName;

    /// <inheritdoc/>
    public Uri? LicenseUrl => _inner.LicenseUrl;

    /// <inheritdoc/>
    public AudioFormat AudioFormat { get; }

    /// <inheritdoc/>
    public IRecognitionBackend CreateBackend(string installedModelDirectory) =>
        throw new NotSupportedException("The fake recognition model has no backend.");
}
