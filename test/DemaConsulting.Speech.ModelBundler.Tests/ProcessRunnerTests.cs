using System.Diagnostics;

namespace DemaConsulting.Speech.ModelBundler.Tests;

/// <summary>
///     Unit tests for <see cref="ProcessRunner"/>: a canceled run surfaces the caller's cancellation even when the
///     child has already exited. The child is <c>dotnet --version</c>, which is always on <c>PATH</c> where these
///     tests run and exits on its own within moments.
/// </summary>
public sealed class ProcessRunnerTests
{
    /// <summary>
    ///     Validates that killing after cancellation tolerates a child that has already exited, the losing side of the
    ///     race between cancellation and normal completion.
    /// </summary>
    [Fact]
    public async Task ProcessRunner_KillAfterCancellation_ProcessAlreadyExited_DoesNotThrow()
    {
        // Arrange: a child that has run to completion
        using var process = Process.Start(new ProcessStartInfo("dotnet", "--version")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        })!;
        await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        // Act
        var exception = Record.Exception(() => ProcessRunner.KillAfterCancellation(process));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Validates that a run canceled before the child exits throws the caller's cancellation, not a kill failure.
    /// </summary>
    [Fact]
    public async Task ProcessRunner_RunAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange: a token that is already canceled
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ProcessRunner().RunAsync("dotnet", ["--version"], cancellation.Token));
    }
}
