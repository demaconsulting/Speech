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

using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;

/// <summary>
///     Minimal, deterministic <see cref="ISpeechModel"/> fake for CLI command unit tests.
/// </summary>
/// <remarks>
///     Implements the shared <see cref="ISpeechModel"/> contract directly (never
///     <see cref="IRecognitionModel"/>/<see cref="ISynthesisModel"/>, which restrict
///     implementation to assemblies granted <c>InternalsVisibleTo</c>), since the
///     model-management commands under test only ever consume the common, role-agnostic surface.
/// </remarks>
internal sealed class FakeSpeechModel : ISpeechModel
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeSpeechModel"/> class.
    /// </summary>
    public FakeSpeechModel(
        string id,
        string displayName = "Fake Model",
        SpeechModelRole role = SpeechModelRole.Recognition,
        IReadOnlyList<ISpeechModelParameter>? parameters = null,
        SpeechModelAudioTagSupport audioTagSupport = SpeechModelAudioTagSupport.None,
        string licenseName = "Unknown",
        Uri? licenseUrl = null)
    {
        Id = id;
        DisplayName = displayName;
        Role = role;
        Parameters = parameters ?? [];
        AudioTagSupport = audioTagSupport;
        LicenseName = licenseName;
        LicenseUrl = licenseUrl;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string DisplayName { get; }

    /// <inheritdoc/>
    public SpeechModelRole Role { get; }

    /// <inheritdoc/>
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; }

    /// <inheritdoc/>
    public SpeechModelAudioTagSupport AudioTagSupport { get; }

    /// <inheritdoc/>
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; } =
        new([new SpeechModelDownloadFile(new Uri("https://example.test/model.bin"), new string('0', 64), "model.bin")]);

    /// <inheritdoc/>
    public string LicenseName { get; }

    /// <inheritdoc/>
    public Uri? LicenseUrl { get; }
}
