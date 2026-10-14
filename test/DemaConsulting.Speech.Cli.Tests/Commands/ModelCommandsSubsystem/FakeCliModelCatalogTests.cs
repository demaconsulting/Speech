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

using DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;

/// <summary>
///     Unit tests for <see cref="FakeCliModelCatalog"/>, proving the shared test double honors the
///     <see cref="ICliModelCatalog"/> contract (cancellation and unknown-identifier refusal) that the
///     command tests rely on.
/// </summary>
public sealed class FakeCliModelCatalogTests
{
    /// <summary>A known model id.</summary>
    private const string KnownModelId = "known-model";

    /// <summary>A known built-in pack id.</summary>
    private const string KnownPackId = "voice-zero-1.0";

    /// <summary>
    ///     Test that uninstalling and cleaning up an unknown model id is refused with an argument error.
    /// </summary>
    [Fact]
    public async Task FakeCliModelCatalog_UninstallAndCleanUp_UnknownModelId_ThrowsArgumentException()
    {
        // Arrange: a catalog with one known model
        var catalog = new FakeCliModelCatalog().WithModel(new FakeSpeechModel(KnownModelId), SpeechModelState.Downloaded);
        var token = TestContext.Current.CancellationToken;

        // Act / Assert: both operations refuse an id that is not in the catalog
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.UninstallAsync("missing", token));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.CleanUpLeftoversAsync("missing", token));
    }

    /// <summary>
    ///     Test that uninstalling and cleaning up a known model that was never installed stays a no-op.
    /// </summary>
    [Fact]
    public async Task FakeCliModelCatalog_UninstallAndCleanUp_KnownNeverInstalledModel_IsNoOp()
    {
        // Arrange: a known model that is not downloaded
        var catalog = new FakeCliModelCatalog().WithModel(new FakeSpeechModel(KnownModelId), SpeechModelState.NotDownloaded);
        var token = TestContext.Current.CancellationToken;

        // Act
        await catalog.UninstallAsync(KnownModelId, token);
        await catalog.CleanUpLeftoversAsync(KnownModelId, token);

        // Assert: nothing is thrown and the model is still reported as not downloaded
        Assert.Equal(SpeechModelState.NotDownloaded, catalog.Enumerate().Single().State);
    }

    /// <summary>
    ///     Test that every voice-pack operation refuses an unknown pack id with an argument error.
    /// </summary>
    [Fact]
    public async Task FakeCliModelCatalog_VoicePackOperations_UnknownPackId_ThrowArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        var token = TestContext.Current.CancellationToken;

        // Act / Assert: each operation refuses the unknown pack
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.VoicePackHasEmotionsAsync("missing", token));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.InstallVoicePackAsync("missing", true, null, token));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.UninstallVoicePackAsync("missing", token));
    }

    /// <summary>
    ///     Test that an already-canceled token is observed by the pack query and uninstall, and that a
    ///     canceled uninstall leaves the pack state unchanged.
    /// </summary>
    [Fact]
    public async Task FakeCliModelCatalog_VoicePackOperations_CanceledToken_ThrowOperationCanceledException()
    {
        // Arrange: an installed pack and a token that is already canceled
        var catalog = new FakeCliModelCatalog();
        catalog.PackStates[KnownPackId] = SpeechModelState.Downloaded;
        var canceled = new CancellationToken(canceled: true);

        // Act / Assert: both operations observe the cancellation
        await Assert.ThrowsAsync<OperationCanceledException>(() => catalog.VoicePackHasEmotionsAsync(KnownPackId, canceled));
        await Assert.ThrowsAsync<OperationCanceledException>(() => catalog.UninstallVoicePackAsync(KnownPackId, canceled));

        // Assert: the canceled uninstall did not mutate state
        Assert.Equal(SpeechModelState.Downloaded, catalog.PackStates[KnownPackId]);
    }

    /// <summary>
    ///     Test that uninstalling a known pack that was never installed is still a no-op.
    /// </summary>
    [Fact]
    public async Task FakeCliModelCatalog_UninstallVoicePack_KnownNeverInstalledPack_IsNoOp()
    {
        // Arrange: a known pack with no recorded state
        var catalog = new FakeCliModelCatalog();

        // Act
        await catalog.UninstallVoicePackAsync(KnownPackId, TestContext.Current.CancellationToken);

        // Assert: the pack is reported as not downloaded
        Assert.Equal(SpeechModelState.NotDownloaded, catalog.GetVoicePackState(KnownPackId));
    }
}
