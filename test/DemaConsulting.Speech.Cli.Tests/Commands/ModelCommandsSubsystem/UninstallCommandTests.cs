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
///     Unit tests for <see cref="UninstallCommand"/>, using <see cref="FakeCliModelCatalog"/> so
///     every scenario runs deterministically with no real store access.
/// </summary>
[Collection("Sequential")]
public sealed class UninstallCommandTests
{
    /// <summary>
    ///     Test that uninstalling an installed model forwards to the catalog seam and reports success.
    /// </summary>
    [Fact]
    public async Task UninstallCommand_Run_InstalledModel_UninstallsAndReportsSuccess()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog().WithModel(new FakeSpeechModel("model-1"), SpeechModelState.Downloaded);
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["uninstall", "model-1"]);

            // Act
            await UninstallCommand.RunAsync(context, catalog, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["model-1"], catalog.UninstallCalls);
            Assert.Contains("model-1", writer.ToString());
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that uninstalling a model that was never installed is a safe no-op, per
    ///     <see cref="SpeechModelStore.UninstallAsync"/>'s own documented behavior, not an error.
    /// </summary>
    [Fact]
    public async Task UninstallCommand_Run_NeverInstalledModel_IsSafeNoOp()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["uninstall", "never-installed"]);

        // Act
        await UninstallCommand.RunAsync(context, catalog, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["never-installed"], catalog.UninstallCalls);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that a <see cref="SpeechModelStoreException"/> (for example an open file handle
    ///     preventing removal) is re-thrown as a clean <see cref="InvalidOperationException"/>
    ///     rather than crashing with a stack trace.
    /// </summary>
    [Fact]
    public async Task UninstallCommand_Run_StoreThrows_ThrowsInvalidOperationException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog
        {
            UninstallException = new SpeechModelStoreException("Failed to uninstall model 'model-1'."),
        };
        using var context = Context.Create(["uninstall", "model-1"]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => UninstallCommand.RunAsync(context, catalog, TestContext.Current.CancellationToken));
        Assert.Contains("model-1", ex.Message);
    }

    /// <summary>
    ///     Test that a canceled uninstall is reported as an actionable error naming the model,
    ///     rather than propagating the cancellation.
    /// </summary>
    [Fact]
    public async Task UninstallCommand_Run_Canceled_ReportsErrorExitCode()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var catalog = new FakeCliModelCatalog().WithModel(new FakeSpeechModel("model-1"), SpeechModelState.Downloaded);
        using var context = Context.Create(["uninstall", "model-1"]);
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        // Act
        try
        {
            await UninstallCommand.RunAsync(context, catalog, cancellation.Token);
        }
        finally
        {
            Console.SetError(originalError);
        }

        // Assert: the canceled token is observed by the catalog before anything is removed
        Assert.Equal(1, context.ExitCode);
        Assert.Contains("Uninstalling model 'model-1' was canceled.", errorWriter.ToString());
        Assert.Equal(SpeechModelState.Downloaded, catalog.Enumerate().Single().State);
    }

    /// <summary>
    ///     Test that omitting the required <c>&lt;modelId&gt;</c> argument throws.
    /// </summary>
    [Fact]
    public async Task UninstallCommand_Run_MissingModelIdArgument_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["uninstall"]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => UninstallCommand.RunAsync(context, catalog, TestContext.Current.CancellationToken));
        Assert.Null(ex.ParamName);
    }

    /// <summary>
    ///     Test that a second, unsupported positional argument throws.
    /// </summary>
    [Fact]
    public async Task UninstallCommand_Run_ExtraArgument_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["uninstall", "model-1", "extra"]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => UninstallCommand.RunAsync(context, catalog, TestContext.Current.CancellationToken));
        Assert.Null(ex.ParamName);
    }

    /// <summary>
    ///     Test that a null context is rejected.
    /// </summary>
    [Fact]
    public async Task UninstallCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => UninstallCommand.RunAsync(null!, catalog, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Test that a null catalog is rejected.
    /// </summary>
    [Fact]
    public async Task UninstallCommand_Run_NullCatalog_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = Context.Create(["uninstall", "model-1"]);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => UninstallCommand.RunAsync(context, null!, TestContext.Current.CancellationToken));
    }
}
