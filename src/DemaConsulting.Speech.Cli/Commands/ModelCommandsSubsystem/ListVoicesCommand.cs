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

using System.Globalization;
using System.Text.Json;
using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;

/// <summary>
///     Implements the <c>list-voices</c> subcommand: lists every available reference voice (the
///     <c>--voices-dir</c> folder, the user folder, and installed packs) as an aligned table
///     (default) or JSON. A voice id is chosen with <c>speak --tts-param voice=&lt;id&gt;</c>.
/// </summary>
internal static class ListVoicesCommand
{
    /// <summary>
    ///     Runs the <c>list-voices</c> subcommand against a real, composed
    ///     <see cref="SpeechModelCatalogAdapter"/>.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when an unsupported flag or flag value is given.</exception>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var catalog = CliModelCatalogFactory.Create(context);
        Run(context, catalog);
    }

    /// <summary>
    ///     Runs the <c>list-voices</c> subcommand against an injected catalog seam, for unit testing.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="catalog">The catalog seam to enumerate voices from. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="catalog"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when an unsupported flag or flag value is given.</exception>
    internal static void Run(Context context, ICliModelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);

        var json = ParseArguments(context.CommandArgs);
        var voices = catalog.EnumerateVoices();

        if (json)
        {
            var rows = voices
                .Select(voice => new VoiceRow(
                    voice.Id,
                    voice.DisplayName,
                    voice.Source.ToString(),
                    voice.PackId,
                    voice.FilePath,
                    voice.LicenseName,
                    [.. (voice.MoodClips?.Keys ?? [])]))
                .ToList();
            context.WriteLine(JsonSerializer.Serialize(rows, JsonOptions));
            return;
        }

        WriteTable(context, catalog, voices);
    }

    /// <summary>Parses <c>--format table|json</c>; returns <see langword="true"/> for JSON.</summary>
    private static bool ParseArguments(IReadOnlyList<string> args)
    {
        var json = false;
        var index = 0;
        while (index < args.Count)
        {
            var arg = args[index++];
            if (arg != "--format")
            {
                throw new ArgumentException($"Unsupported argument '{arg}' for 'list-voices'.", nameof(args));
            }

            if (index >= args.Count)
            {
                throw new ArgumentException("--format requires a value.", nameof(args));
            }

            var value = args[index++];
            json = value switch
            {
                "table" => false,
                "json" => true,
                _ => throw new ArgumentException($"--format must be 'table' or 'json', not '{value}'.", nameof(args))
            };
        }

        return json;
    }

    /// <summary>Writes the voices as an aligned table followed by usage hints and the consent note.</summary>
    private static void WriteTable(Context context, ICliModelCatalog catalog, IReadOnlyList<ReferenceVoiceInfo> voices)
    {
        if (voices.Count == 0)
        {
            context.WriteLine("No reference voices are available.");
            context.WriteLine($"Install the voice pack with 'speech-cli voices install {ReferenceVoicePack.VoiceZero10.Id}', or put .wav/.flac");
            context.WriteLine($"files in '{catalog.UserVoicesDirectory}' (or pass --voices-dir <path>).");
            context.WriteLine(ReferenceVoiceLibrary.ConsentNotice);
            return;
        }

        var idWidth = Math.Max("Id".Length, voices.Max(v => v.Id.Length));
        var sourceWidth = Math.Max("Source".Length, voices.Max(v => v.Source.ToString().Length));
        const int MoodsWidth = 5;

        context.WriteLine($"{"Id".PadRight(idWidth)}  {"Source".PadRight(sourceWidth)}  {"Moods".PadRight(MoodsWidth)}  Display Name");
        context.WriteLine($"{new string('-', idWidth)}  {new string('-', sourceWidth)}  {new string('-', MoodsWidth)}  {new string('-', "Display Name".Length)}");
        foreach (var voice in voices)
        {
            var moods = voice.MoodClips is { Count: > 0 } clips ? clips.Count.ToString(CultureInfo.InvariantCulture) : "-";
            context.WriteLine($"{voice.Id.PadRight(idWidth)}  {voice.Source.ToString().PadRight(sourceWidth)}  {moods.PadRight(MoodsWidth)}  {voice.DisplayName}");
        }

        context.WriteLine("");
        context.WriteLine("Moods is the number of emotion variants of the voice (installed with 'voices install', not with --no-emotions).");
        context.WriteLine("Use a voice with: speak --tts-model <id> --tts-param voice=<Id> ...");
        context.WriteLine(ReferenceVoiceLibrary.ConsentNotice);
    }

    /// <summary>Shared, indented JSON serialization options for this command's output.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>One JSON-serializable voice row.</summary>
    private sealed record VoiceRow(string Id, string DisplayName, string Source, string? PackId, string FilePath, string? LicenseName, IReadOnlyList<string> Moods);
}
