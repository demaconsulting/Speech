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

using System.Text.Json;
using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;

/// <summary>
///     Unit tests for <see cref="ListVoicesCommand"/>, using <see cref="FakeCliModelCatalog"/>.
/// </summary>
[Collection("Sequential")]
public sealed class ListVoicesCommandTests
{
    /// <summary>
    ///     Test that the default table lists each voice id and source, the usage hint, and the consent note.
    /// </summary>
    [Fact]
    public void ListVoicesCommand_Run_Table_ListsVoicesAndConsentNote()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        catalog.Voices.Add(new ReferenceVoiceInfo("alice", "Alice", "/v/alice.wav", ReferenceVoiceSource.User, null, "alice"));
        catalog.Voices.Add(new ReferenceVoiceInfo("bob-1", "Bob 1", "/v/bob-1.flac", ReferenceVoiceSource.Pack, "voice-zero-1.0", "bob"));
        var output = Execute(["list-voices"], catalog, out var exitCode);

        // Assert
        Assert.Contains("alice", output);
        Assert.Contains("bob-1", output);
        Assert.Contains("Pack", output);
        Assert.Contains("--tts-param voice=", output);
        Assert.Contains(ReferenceVoiceLibrary.ConsentNotice, output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that the table and JSON show how many mood variants a voice has (and none as a dash).
    /// </summary>
    [Fact]
    public void ListVoicesCommand_Run_MoodClips_ShowsMoodsColumnAndJson()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        catalog.Voices.Add(new ReferenceVoiceInfo("alice", "Alice", "/v/alice.wav", ReferenceVoiceSource.User, null, "alice"));
        catalog.Voices.Add(new ReferenceVoiceInfo(
            "bob-1",
            "Bob 1",
            "/v/bob-1.flac",
            ReferenceVoiceSource.Pack,
            "voice-zero-1.0",
            "bob-1",
            MoodClips: new Dictionary<string, string> { ["happy"] = "/m/happy.flac", ["sad"] = "/m/sad.flac" }));

        // Act
        var table = Execute(["list-voices"], catalog, out _);
        var json = Execute(["list-voices", "--format", "json"], catalog, out _);

        // Assert
        var lines = table.Split('\n');
        Assert.Contains("Moods", lines[0], StringComparison.Ordinal);
        Assert.Contains(lines, line => line.StartsWith("alice", StringComparison.Ordinal) && line.Contains(" -  ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("bob-1", StringComparison.Ordinal) && line.Contains(" 2  ", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(json);
        var moods = document.RootElement[1].GetProperty("Moods").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Equal(["happy", "sad"], moods);
        Assert.Empty(document.RootElement[0].GetProperty("Moods").EnumerateArray());
    }

    /// <summary>
    ///     Test that with no voices the command explains how to get some and still prints the consent note.
    /// </summary>
    [Fact]
    public void ListVoicesCommand_Run_NoVoices_ExplainsHowToAddVoices()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog { UserVoicesDirectoryValue = "my-user-dir" };

        // Act
        var output = Execute(["list-voices"], catalog, out var exitCode);

        // Assert
        Assert.Contains("No reference voices are available.", output);
        Assert.Contains("voices install voice-zero-1.0", output);
        Assert.Contains("my-user-dir", output);
        Assert.Contains(ReferenceVoiceLibrary.ConsentNotice, output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that <c>--format json</c> writes a parseable array carrying id, source, and pack id.
    /// </summary>
    [Fact]
    public void ListVoicesCommand_Run_Json_WritesParseableArray()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        catalog.Voices.Add(new ReferenceVoiceInfo("bob-1", "Bob 1", "/v/bob-1.flac", ReferenceVoiceSource.Pack, "voice-zero-1.0", "bob", LicenseName: "CC0 1.0 Universal"));

        // Act
        var output = Execute(["list-voices", "--format", "json"], catalog, out _);

        // Assert
        using var document = JsonDocument.Parse(output);
        var row = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("bob-1", row.GetProperty("Id").GetString());
        Assert.Equal("Pack", row.GetProperty("Source").GetString());
        Assert.Equal("voice-zero-1.0", row.GetProperty("PackId").GetString());
    }

    /// <summary>
    ///     Test that an unsupported flag or an invalid format value is a clean <see cref="ArgumentException"/>.
    /// </summary>
    [Theory]
    [InlineData("--bogus")]
    [InlineData("--format")]
    public void ListVoicesCommand_Run_BadArguments_ThrowsArgumentException(string argument)
    {
        // Arrange
        using var context = Context.Create(["list-voices", argument]);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ListVoicesCommand.Run(context, new FakeCliModelCatalog()));
    }

    /// <summary>
    ///     Test that an unknown <c>--format</c> value is rejected.
    /// </summary>
    [Fact]
    public void ListVoicesCommand_Run_UnknownFormat_ThrowsArgumentException()
    {
        // Arrange
        using var context = Context.Create(["list-voices", "--format", "xml"]);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ListVoicesCommand.Run(context, new FakeCliModelCatalog()));
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void ListVoicesCommand_Run_NullArguments_Throw()
    {
        // Arrange
        using var context = Context.Create(["list-voices"]);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ListVoicesCommand.Run(null!, new FakeCliModelCatalog()));
        Assert.Throws<ArgumentNullException>(() => ListVoicesCommand.Run(context, null!));
        Assert.Throws<ArgumentNullException>(() => ListVoicesCommand.Run(null!));
    }

    /// <summary>Runs the command capturing console output.</summary>
    private static string Execute(string[] args, FakeCliModelCatalog catalog, out int exitCode)
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(args);
            ListVoicesCommand.Run(context, catalog);
            exitCode = context.ExitCode;
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
