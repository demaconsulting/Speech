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

namespace DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;

/// <summary>
///     Unit tests for <see cref="CleanCommand"/>, using <see cref="FakeCliModelCatalog"/> so
///     every scenario runs deterministically with no real store access.
/// </summary>
[Collection("Sequential")]
public sealed class CleanCommandTests
{
    /// <summary>
    ///     Test that <c>clean</c> forwards to the catalog seam's cleanup and reports success.
    /// </summary>
    [Fact]
    public void CleanCommand_Run_KnownModel_CleansUpAndReportsSuccess()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["clean", "model-1"]);

            // Act
            CleanCommand.Run(context, catalog);

            // Assert
            Assert.Equal(["model-1"], catalog.CleanUpLeftoversCalls);
            Assert.Contains("model-1", writer.ToString());
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that omitting the required <c>&lt;modelId&gt;</c> argument throws.
    /// </summary>
    [Fact]
    public void CleanCommand_Run_MissingModelIdArgument_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["clean"]);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CleanCommand.Run(context, catalog));
    }

    /// <summary>
    ///     Test that a second, unsupported positional argument throws.
    /// </summary>
    [Fact]
    public void CleanCommand_Run_ExtraArgument_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        using var context = Context.Create(["clean", "model-1", "extra"]);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => CleanCommand.Run(context, catalog));
    }

    /// <summary>
    ///     Test that a null context is rejected.
    /// </summary>
    [Fact]
    public void CleanCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CleanCommand.Run(null!, new FakeCliModelCatalog()));
    }

    /// <summary>
    ///     Test that a null catalog is rejected.
    /// </summary>
    [Fact]
    public void CleanCommand_Run_NullCatalog_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["clean", "model-1"]);
        Assert.Throws<ArgumentNullException>(() => CleanCommand.Run(context, null!));
    }
}
