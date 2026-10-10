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

namespace DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;

/// <summary>
///     Unit tests for <see cref="SpeechModelCatalogAdapter"/> and <see cref="CliModelCatalogFactory"/>,
///     proving the production seam forwards to a real (but empty-known-model,
///     temp-directory-rooted) <see cref="SpeechModelCatalog"/> without requiring network access.
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

        // Assert: the adapter registers the same five models the application's own composition
        // root does via AddSherpaModels().AddKokoroModels()
        Assert.Equal(5, descriptors.Count);
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
        Assert.Equal(5, adapter.Enumerate().Count);
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

    // NOTE: No test exercises a real, downloaded synthesis-role model here. CI has no cached TTS
    // model available (see the Pass 5 planning report's Assumption 4), so real
    // ISynthesisModel.PreferredAudioFormat/CreateSynthesizer forwarding is validated only through
    // SpeakCommandTests's FakeCliModelCatalog seam, not against a real SpeechModelCatalog. The
    // same applies to a real, downloaded recognition-role model and
    // GetAudioFormat/CreateRecognizer (see the Pass 6 planning report's Assumption 4) - validated
    // only through RecognizeCommandTests's FakeCliModelCatalog seam.
}
