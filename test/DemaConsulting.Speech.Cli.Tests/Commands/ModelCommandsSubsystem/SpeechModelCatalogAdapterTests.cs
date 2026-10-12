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
using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;
using DemaConsulting.Speech.Sherpa.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;

/// <summary>
///     Unit tests for <see cref="SpeechModelCatalogAdapter"/> and <see cref="CliModelCatalogFactory"/>,
///     proving the production seam forwards to a real
///     (temp-directory-rooted) <see cref="SpeechModelCatalog"/> without requiring network access.
/// </summary>
[Collection("Sequential")]
public sealed class SpeechModelCatalogAdapterTests : IDisposable
{
    private readonly string _testRoot;

    /// <summary>
    ///     Initializes a fresh, unique scratch directory for each test.
    /// </summary>
    public SpeechModelCatalogAdapterTests()
    {
        _testRoot = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    /// <summary>
    ///     Deletes the scratch directory tree created for this test instance.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup only; a leftover temp directory does not fail the test.
        }
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.Enumerate"/> forwards to the library's
    ///     real, compiled-in known-model registry.
    /// </summary>
    [Fact]
    public void SpeechModelCatalogAdapter_Enumerate_ReturnsCompiledInKnownModels()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });

        // Act
        var descriptors = adapter.Enumerate();

        // Assert: the adapter registers the same models the application's own composition
        // root does via AddSherpaModels().AddKokoroModels().AddNemotronSttModels().AddChatterboxTurboModels()
        Assert.Contains(descriptors, d => d.Id == SherpaOnnxZipformerEnRecognitionModel.ModelId);
        Assert.Contains(descriptors, d => d.Id == OnnxKokoroEnglishSynthesisModel.ModelId);
        Assert.Contains(descriptors, d => d.Id == OnnxNemotronMultilingualRecognitionModel.ModelId);
        Assert.Contains(descriptors, d => d.Id.StartsWith(OnnxChatterboxTurboEnglishSynthesisModel.BaseModelId, StringComparison.Ordinal));
        Assert.All(descriptors, descriptor => Assert.Equal(SpeechModelState.NotDownloaded, descriptor.State));
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.Uninstall"/> and
    ///     <see cref="SpeechModelCatalogAdapter.CleanUpLeftovers"/> forward to the store without
    ///     throwing for a model with nothing installed.
    /// </summary>
    [Fact]
    public void SpeechModelCatalogAdapter_UninstallAndCleanUpLeftovers_NeverInstalledModel_DoNotThrow()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });
        var modelId = adapter.Enumerate()[0].Id;

        // Act & Assert: neither call throws for a model with nothing installed
        adapter.Uninstall(modelId);
        adapter.CleanUpLeftovers(modelId);
        Assert.DoesNotContain(adapter.Enumerate(), descriptor => descriptor.State == SpeechModelState.Downloaded);
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.DownloadAsync"/> forwards to the real
    ///     catalog and throws for an id absent from the known-model list.
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalogAdapter_DownloadAsync_UnknownModelId_ThrowsArgumentException()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => adapter.DownloadAsync("does-not-exist", null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that <see cref="CliModelCatalogFactory.Create"/> honors <c>--models-dir</c>,
    ///     resolving the adapter's underlying store to the given root.
    /// </summary>
    [Fact]
    public void CliModelCatalogFactory_Create_WithModelsDir_UsesGivenRoot()
    {
        // Arrange
        using var context = Context.Create(["list-models", "--models-dir", _testRoot]);

        // Act
        using var adapter = CliModelCatalogFactory.Create(context);

        // Assert: enumerating does not throw and returns the compiled-in known models
        Assert.NotEmpty(adapter.Enumerate());
    }

    /// <summary>
    ///     Test that the adapter's voice members use the store's reference-voice library: the pack is
    ///     listed as not downloaded, user and <c>--voices-dir</c> voices are enumerated, and the user
    ///     directory lies under the models root.
    /// </summary>
    [Fact]
    public void SpeechModelCatalogAdapter_Voices_ListsUserAndVoicesDirVoices()
    {
        // Arrange
        var voicesDir = Path.Join(_testRoot, "extra-voices");
        Directory.CreateDirectory(voicesDir);
        WriteTinyWav(Path.Join(voicesDir, "zoe.wav"));
        using var adapter = new SpeechModelCatalogAdapter(
            new SpeechModelStoreOptions { RootPathOverride = _testRoot },
            voicesDir: voicesDir);
        Directory.CreateDirectory(adapter.UserVoicesDirectory);
        WriteTinyWav(Path.Join(adapter.UserVoicesDirectory, "yan.wav"));

        // Act
        var voices = adapter.EnumerateVoices();

        // Assert
        Assert.StartsWith(_testRoot, adapter.UserVoicesDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(voices, v => v.Id == "zoe" && v.Source == ReferenceVoiceSource.Directory);
        Assert.Contains(voices, v => v.Id == "yan" && v.Source == ReferenceVoiceSource.User);
        var pack = Assert.Single(adapter.VoicePacks);
        Assert.Equal(SpeechModelState.NotDownloaded, adapter.GetVoicePackState(pack.Id));
    }

    /// <summary>
    ///     Test that uninstalling a never-installed pack is a no-op and an unknown pack id is rejected.
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalogAdapter_VoicePackUninstallAndInstall_UnknownOrMissingPack_BehaveCleanly()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });
        var packId = adapter.VoicePacks[0].Id;

        // Act & Assert
        adapter.UninstallVoicePack(packId);
        Assert.Equal(SpeechModelState.NotDownloaded, adapter.GetVoicePackState(packId));
        Assert.Throws<ArgumentException>(() => adapter.GetVoicePackState("nope"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => adapter.InstallVoicePackAsync("nope", true, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that <see cref="CliModelCatalogFactory.Create"/> threads <c>--voices-dir</c> into the adapter.
    /// </summary>
    [Fact]
    public void CliModelCatalogFactory_Create_WithVoicesDir_EnumeratesThatFolder()
    {
        // Arrange
        var voicesDir = Path.Join(_testRoot, "voices-from-flag");
        Directory.CreateDirectory(voicesDir);
        WriteTinyWav(Path.Join(voicesDir, "kim.wav"));
        using var context = Context.Create(["list-voices", "--models-dir", _testRoot, "--voices-dir", voicesDir]);

        // Act
        using var adapter = CliModelCatalogFactory.Create(context);

        // Assert
        Assert.Contains(adapter.EnumerateVoices(), v => v.Id == "kim");
    }

    /// <summary>
    ///     Test that a damaged file in <c>--voices-dir</c> does not break composing the adapter, and that
    ///     enumeration still lists it by name.
    /// </summary>
    [Fact]
    public void SpeechModelCatalogAdapter_DamagedVoicesDirFile_DoesNotBreakAdapterOrListing()
    {
        // Arrange
        var voicesDir = Path.Join(_testRoot, "damaged-voices");
        Directory.CreateDirectory(voicesDir);
        File.WriteAllBytes(Path.Join(voicesDir, "broken.wav"), [1, 2, 3, 4]);
        WriteTinyWav(Path.Join(voicesDir, "fine.wav"));

        // Act
        using var adapter = new SpeechModelCatalogAdapter(
            new SpeechModelStoreOptions { RootPathOverride = _testRoot },
            voicesDir: voicesDir);

        // Assert
        var voices = adapter.EnumerateVoices();
        Assert.Contains(voices, v => v.Id == "broken");
        Assert.Contains(voices, v => v.Id == "fine");
        Assert.NotEmpty(adapter.Enumerate());
    }

    /// <summary>Writes a tiny, valid 16-bit mono 24 kHz silent 1.5 s WAV file (a decodable reference voice).</summary>
    private static void WriteTinyWav(string path)
    {
        const int sampleRate = 24000;
        const int samples = 36000;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + (samples * 2));
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples * 2);
        writer.Write(new byte[samples * 2]);
    }

    /// <summary>
    ///     Test that <see cref="CliModelCatalogFactory.Create"/> rejects a null context.
    /// </summary>
    [Fact]
    public void CliModelCatalogFactory_Create_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CliModelCatalogFactory.Create(null!));
    }

    /// <summary>
    ///     Test that <see cref="CliModelCatalogFactory.Create"/> actually threads a configured
    ///     <c>--mirror-url</c> option into the composed catalog's downloader, rather than silently
    ///     dropping it, by pointing the mirror at a loopback port with nothing listening and
    ///     observing that the resulting download failure comes from attempting to reach that
    ///     loopback mirror - never the model's own real, public download host - proving the
    ///     mirror was genuinely applied rather than merely accepted and ignored.
    /// </summary>
    [Fact]
    public async Task CliModelCatalogFactory_Create_WithMirrorUrl_AppliesMirrorToDownloads()
    {
        // Arrange: reserve a loopback port with nothing listening on it, so any connection
        // attempt fails fast and deterministically rather than timing out.
        using var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        var freePort = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var mirrorUrl = $"http://127.0.0.1:{freePort}/models";

        using var context = Context.Create([
            "list-models", "--models-dir", _testRoot, "--mirror-url", mirrorUrl
        ]);
        using var adapter = CliModelCatalogFactory.Create(context);
        var modelId = adapter.Enumerate()[0].Id;

        // Act
        var result = await adapter.DownloadAsync(modelId, null, TestContext.Current.CancellationToken);

        // Assert: the download failed (nothing is listening on the reserved loopback port), and
        // the captured failure reports attempting to reach the configured mirror host/port -
        // proof the mirror was genuinely applied to this model's download rather than the
        // model's own real public URI being used unchanged.
        Assert.NotEqual(SpeechModelDownloadOutcome.Installed, result.Outcome);
        Assert.NotNull(result.Error);
        var errorText = result.Error.ToString();
        Assert.Contains($"127.0.0.1:{freePort}", errorText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.GetPreferredAudioFormat"/> throws a clean
    ///     <see cref="ArgumentException"/> for a real, compiled-in recognition-role model (not a
    ///     synthesis model).
    /// </summary>
    [Fact]
    public void SpeechModelCatalogAdapter_GetPreferredAudioFormat_RecognitionRoleModel_ThrowsArgumentException()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });
        var descriptor = adapter.Enumerate().First(d => d.Role == SpeechModelRole.Recognition);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => adapter.GetPreferredAudioFormat(descriptor));
        Assert.Equal("descriptor", exception.ParamName);
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.GetPreferredAudioFormat"/> rejects a null
    ///     descriptor.
    /// </summary>
    [Fact]
    public void SpeechModelCatalogAdapter_GetPreferredAudioFormat_NullDescriptor_ThrowsArgumentNullException()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => adapter.GetPreferredAudioFormat(null!));
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.CreateSynthesizerEngineAsync"/> throws a
    ///     clean <see cref="ArgumentException"/> for a real, compiled-in recognition-role model
    ///     (not a synthesis model).
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalogAdapter_CreateSynthesizerEngineAsync_RecognitionRoleModel_ThrowsArgumentException()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });
        var descriptor = adapter.Enumerate().First(d => d.Role == SpeechModelRole.Recognition);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => adapter.CreateSynthesizerEngineAsync(descriptor, null, TestContext.Current.CancellationToken));
        Assert.Equal("descriptor", exception.ParamName);
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.CreateSynthesizerEngineAsync"/> rejects a
    ///     null descriptor.
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalogAdapter_CreateSynthesizerEngineAsync_NullDescriptor_ThrowsArgumentNullException()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => adapter.CreateSynthesizerEngineAsync(null!, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.GetAudioFormat"/> throws a clean
    ///     <see cref="ArgumentException"/> for a real, compiled-in synthesis-role model (not a
    ///     recognition model).
    /// </summary>
    [Fact]
    public void SpeechModelCatalogAdapter_GetAudioFormat_SynthesisRoleModel_ThrowsArgumentException()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });
        var descriptor = adapter.Enumerate().First(d => d.Role == SpeechModelRole.Synthesis);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => adapter.GetAudioFormat(descriptor));
        Assert.Equal("descriptor", exception.ParamName);
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.GetAudioFormat"/> rejects a null
    ///     descriptor.
    /// </summary>
    [Fact]
    public void SpeechModelCatalogAdapter_GetAudioFormat_NullDescriptor_ThrowsArgumentNullException()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => adapter.GetAudioFormat(null!));
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.CreateRecognizerEngineAsync"/> throws a
    ///     clean <see cref="ArgumentException"/> for a real, compiled-in synthesis-role model (not
    ///     a recognition model).
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalogAdapter_CreateRecognizerEngineAsync_SynthesisRoleModel_ThrowsArgumentException()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });
        var descriptor = adapter.Enumerate().First(d => d.Role == SpeechModelRole.Synthesis);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => adapter.CreateRecognizerEngineAsync(descriptor, null, TestContext.Current.CancellationToken));
        Assert.Equal("descriptor", exception.ParamName);
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.CreateRecognizerEngineAsync"/> rejects a
    ///     null descriptor.
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalogAdapter_CreateRecognizerEngineAsync_NullDescriptor_ThrowsArgumentNullException()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => adapter.CreateRecognizerEngineAsync(null!, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.GetPreferredAudioFormat"/> returns the
    ///     synthesis model's declared preferred format.
    /// </summary>
    [Fact]
    public void SpeechModelCatalogAdapter_GetPreferredAudioFormat_WithSynthesisModel_ReturnsFormat()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });
        var descriptor = new SpeechModelDescriptor(
            new FakeSynthesisModel("fake-tts", new AudioFormat(24000, 1)), SpeechModelState.NotDownloaded);

        // Act
        var format = adapter.GetPreferredAudioFormat(descriptor);

        // Assert
        Assert.Equal(24000, format.SampleRate);
        Assert.Equal(1, format.ChannelCount);
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.GetAudioFormat"/> returns the recognition
    ///     model's declared input format.
    /// </summary>
    [Fact]
    public void SpeechModelCatalogAdapter_GetAudioFormat_WithRecognitionModel_ReturnsFormat()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });
        var descriptor = new SpeechModelDescriptor(
            new FakeRecognitionModel("fake-stt", new AudioFormat(16000, 1)), SpeechModelState.NotDownloaded);

        // Act
        var format = adapter.GetAudioFormat(descriptor);

        // Assert
        Assert.Equal(16000, format.SampleRate);
        Assert.Equal(1, format.ChannelCount);
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.CreateSynthesizerEngineAsync"/> forwards a
    ///     synthesis model to the library loader, which degrades a not-installed model to an
    ///     unavailable engine rather than throwing.
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalogAdapter_CreateSynthesizerEngineAsync_WithSynthesisModel_ReturnsEngine()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });
        var descriptor = new SpeechModelDescriptor(
            new FakeSynthesisModel("fake-tts", new AudioFormat(24000, 1)), SpeechModelState.NotDownloaded);

        // Act
        await using var engine = await adapter.CreateSynthesizerEngineAsync(
            descriptor, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(engine);
        Assert.False(engine.IsAvailable);
    }

    /// <summary>
    ///     Test that <see cref="SpeechModelCatalogAdapter.CreateRecognizerEngineAsync"/> forwards a
    ///     recognition model to the library loader, which degrades a not-installed model to an
    ///     unavailable engine rather than throwing.
    /// </summary>
    [Fact]
    public async Task SpeechModelCatalogAdapter_CreateRecognizerEngineAsync_WithRecognitionModel_ReturnsEngine()
    {
        // Arrange
        using var adapter = new SpeechModelCatalogAdapter(new SpeechModelStoreOptions { RootPathOverride = _testRoot });
        var descriptor = new SpeechModelDescriptor(
            new FakeRecognitionModel("fake-stt", new AudioFormat(16000, 1)), SpeechModelState.NotDownloaded);

        // Act
        await using var engine = await adapter.CreateRecognizerEngineAsync(
            descriptor, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(engine);
        Assert.False(engine.IsAvailable);
    }
}
