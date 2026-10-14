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
///     Implements only the shared <see cref="ISpeechModel"/> contract, since the
///     model-management commands under test only ever consume the common, role-agnostic surface.
///     <see cref="FakeSynthesisModel"/> and <see cref="FakeRecognitionModel"/> cover the public
///     role interfaces for the seam adapter tests.
/// </remarks>
internal sealed class FakeSpeechModel : ISpeechModel
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeSpeechModel"/> class.
    /// </summary>
    /// <param name="id">The model id.</param>
    /// <param name="displayName">The human-readable model name.</param>
    /// <param name="role">The role the model fills (recognition by default).</param>
    /// <param name="parameters">The parameters the model exposes, or <see langword="null"/> for none.</param>
    /// <param name="audioTagSupport">The audio-tag support the model advertises.</param>
    /// <param name="licenseName">The license name the model reports.</param>
    /// <param name="licenseUrl">The license URL the model reports, or <see langword="null"/> when absent.</param>
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
