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

using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;

/// <summary>
///     Unit tests for <see cref="VoicesCommand"/>, using <see cref="FakeCliModelCatalog"/> so no
///     test touches the network or a real store.
/// </summary>
[Collection("Sequential")]
public sealed class VoicesCommandTests
{
    /// <summary>The built-in pack's id.</summary>
    private const string PackId = "voice-zero-1.0";

    /// <summary>
    ///     Test that <c>voices packs</c> lists the pack, its state, and the consent note.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_Packs_ListsPackStateAndConsentNote()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        catalog.PackStates[PackId] = SpeechModelState.Downloaded;

        // Act
        var (output, _) = await ExecuteAsync(["voices", "packs"], catalog, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(PackId, output);
        Assert.Contains("Downloaded", output);
        Assert.Contains("CC0", output);
        Assert.Contains(ReferenceVoiceLibrary.ConsentNotice, output);
    }

    /// <summary>
    ///     Test that <c>voices install</c> installs through the seam, reports progress, and prints the consent note.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_Install_ReportsProgressAndConsentNote()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog
        {
            OnInstallPack = progress => progress?.Report(new SpeechModelDownloadProgress(0, 1, 50, 100)),
        };
        var originalSyncContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new ImmediateSynchronizationContext());
        try
        {
            // Act
            var (output, exitCode) = await ExecuteAsync(["voices", "install", PackId], catalog, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal([PackId], catalog.InstallPackCalls);
            Assert.Contains("50%", output);
            Assert.Contains("installed", output);
            Assert.Contains(ReferenceVoiceLibrary.ConsentNotice, output);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalSyncContext);
        }
    }

    /// <summary>
    ///     Test that <c>voices install</c> includes the emotion clips by default and says so.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_Install_DefaultIncludesEmotions()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();

        // Act
        var (output, exitCode) = await ExecuteAsync(["voices", "install", PackId], catalog, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([true], catalog.InstallEmotionsCalls);
        Assert.Contains("with emotion variants", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that <c>--no-emotions</c> installs the base voices only and is forwarded to the seam.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_InstallNoEmotions_InstallsBaseVoicesOnly()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();

        // Act
        var (output, exitCode) = await ExecuteAsync(["voices", "install", PackId, "--no-emotions"], catalog, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([false], catalog.InstallEmotionsCalls);
        Assert.Contains("base voices only", output);
        Assert.DoesNotContain("with emotion variants", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that installing over a voices-only pack announces the upgrade.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_InstallOverVoicesOnly_AnnouncesUpgrade()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        catalog.PackStates[PackId] = SpeechModelState.Downloaded;

        // Act
        var (output, _) = await ExecuteAsync(["voices", "install", PackId], catalog, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("removed and downloaded again", output);
        Assert.Equal([true], catalog.InstallEmotionsCalls);
    }

    /// <summary>
    ///     Test that <c>voices packs</c> shows the real download size and the installed sizes with and without emotions.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_Packs_ShowsDownloadAndInstalledSizes()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        catalog.PackStates[PackId] = SpeechModelState.Downloaded;
        catalog.PacksWithEmotions.Add(PackId);

        // Act
        var (output, _) = await ExecuteAsync(["voices", "packs"], catalog, TestContext.Current.CancellationToken);

        // Assert: 597,269,359 B download; 45,632,432 B voices; 553,873,246 B emotions
        Assert.Contains("+Emotions", output);
        Assert.Contains("570 MB", output);
        Assert.Contains(" 44 MB", output);
        Assert.Contains("528 MB", output);
        Assert.Contains("Downloaded (with emotions)", output);
    }

    /// <summary>
    ///     Test that a failed install is reported as an error (non-zero exit) without the consent note of success.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_InstallFails_ReportsErrorExitCode()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog
        {
            InstallPackResult = new SpeechModelDownloadResult(
                SpeechModelDownloadOutcome.ChecksumMismatch,
                new InvalidOperationException("bad hash")),
        };

        // Act
        var (_, exitCode) = await ExecuteAsync(["voices", "install", PackId], catalog, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>
    ///     Test that a canceled install is reported cleanly as an error.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_InstallCanceled_ReportsErrorExitCode()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();

        // Act
        var (_, exitCode) = await ExecuteAsync(["voices", "install", PackId], catalog, new CancellationToken(canceled: true));

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>
    ///     Test that <c>voices uninstall</c> forwards to the seam and reports success.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_Uninstall_ForwardsAndReports()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();

        // Act
        var (output, exitCode) = await ExecuteAsync(["voices", "uninstall", PackId], catalog, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([PackId], catalog.UninstallPackCalls);
        Assert.Contains("uninstalled", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that a store failure during uninstall becomes a clean <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_UninstallStoreThrows_ThrowsInvalidOperationException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog { UninstallPackException = new SpeechModelStoreException("in use") };
        using var context = Context.Create(["voices", "uninstall", PackId]);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => VoicesCommand.RunAsync(context, catalog, CancellationToken.None));
    }

    /// <summary>
    ///     Test that a canceled uninstall is reported as an error rather than propagating the cancellation.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_UninstallCanceled_ReportsErrorExitCode()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var catalog = new FakeCliModelCatalog { UninstallPackException = new OperationCanceledException(cancellation.Token) };
        using var context = Context.Create(["voices", "uninstall", PackId]);

        // Act
        await VoicesCommand.RunAsync(context, catalog, cancellation.Token);

        // Assert
        Assert.Equal(1, context.ExitCode);
    }

    /// <summary>
    ///     Test that a cancellation while the installed-pack emotion check runs ahead of an upgrade
    ///     install is reported as an error rather than escaping as an unhandled exception.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_InstallCanceledDuringPreCheck_ReportsErrorExitCode()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog { HasEmotionsThrowsCanceled = true };
        catalog.PackStates[PackId] = SpeechModelState.Downloaded;

        // Act
        var (_, exitCode) = await ExecuteAsync(["voices", "install", PackId], catalog, new CancellationToken(canceled: true));

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Empty(catalog.InstallPackCalls);
    }

    /// <summary>
    ///     Test that a cancellation while <c>voices packs</c> reads an installed pack's emotion state is
    ///     reported as an error rather than escaping as an unhandled exception.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_PacksCanceled_ReportsErrorExitCode()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog { HasEmotionsThrowsCanceled = true };
        catalog.PackStates[PackId] = SpeechModelState.Downloaded;

        // Act
        var (_, exitCode) = await ExecuteAsync(["voices", "packs"], catalog, new CancellationToken(canceled: true));

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>
    ///     Test that an <see cref="OperationCanceledException"/> not caused by Ctrl+C (the token is not
    ///     canceled) is not masked as a user cancellation.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_InstallCanceledWithoutToken_Propagates()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog { HasEmotionsThrowsCanceled = true };
        catalog.PackStates[PackId] = SpeechModelState.Downloaded;
        using var context = Context.Create(["voices", "install", PackId]);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => VoicesCommand.RunAsync(context, catalog, CancellationToken.None));
    }

    /// <summary>
    ///     Test that argument-parse errors report no misleading parameter name (not <c>context</c>).
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_MissingSubAction_ReportsNoParameterName()
    {
        using var context = Context.Create(["voices"]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => VoicesCommand.RunAsync(context, new FakeCliModelCatalog(), CancellationToken.None));

        Assert.Null(exception.ParamName);
    }

    /// <summary>
    ///     Test that an unsupported sub-action reports no misleading parameter name.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_UnknownSubAction_ReportsNoParameterName()
    {
        using var context = Context.Create(["voices", "bogus"]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => VoicesCommand.RunAsync(context, new FakeCliModelCatalog(), CancellationToken.None));

        Assert.Null(exception.ParamName);
    }

    /// <summary>
    ///     Test that an unknown pack id is a clean <see cref="ArgumentException"/> for install.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_InstallUnknownPack_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["voices", "install", "nope"]);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => VoicesCommand.RunAsync(context, catalog, CancellationToken.None));
        Assert.Empty(catalog.InstallPackCalls);
    }

    /// <summary>
    ///     Test that missing, unknown, or extra arguments are rejected with <see cref="ArgumentException"/>.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("bogus")]
    [InlineData("install")]
    [InlineData("uninstall")]
    [InlineData("install voice-zero-1.0 extra")]
    [InlineData("packs extra")]
    public async Task VoicesCommand_RunAsync_BadArguments_ThrowsArgumentException(string commandArgs)
    {
        // Arrange
        var args = new List<string> { "voices" };
        args.AddRange(commandArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        using var context = Context.Create([.. args]);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => VoicesCommand.RunAsync(context, new FakeCliModelCatalog(), CancellationToken.None));
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public async Task VoicesCommand_RunAsync_NullArguments_Throw()
    {
        using var context = Context.Create(["voices", "packs"]);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => VoicesCommand.RunAsync(null!, new FakeCliModelCatalog(), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => VoicesCommand.RunAsync(context, null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => VoicesCommand.RunAsync(null!));
    }

    /// <summary>Runs the command capturing console output and the exit code.</summary>
    private static async Task<(string Output, int ExitCode)> ExecuteAsync(
        string[] args,
        FakeCliModelCatalog catalog,
        CancellationToken cancellationToken)
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(args);
            await VoicesCommand.RunAsync(context, catalog, cancellationToken);
            return (writer.ToString(), context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>A synchronization context that runs posted callbacks inline, so <see cref="Progress{T}"/> reports are deterministic.</summary>
    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        /// <inheritdoc/>
        public override void Post(SendOrPostCallback d, object? state) => d(state);

        /// <inheritdoc/>
        public override void Send(SendOrPostCallback d, object? state) => d(state);
    }
}
