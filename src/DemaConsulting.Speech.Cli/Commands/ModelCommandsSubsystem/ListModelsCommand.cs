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
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;

/// <summary>
///     Implements the <c>list-models</c> subcommand: enumerates every known model, optionally
///     filtered by role/state, formatted as an aligned table (default) or JSON.
/// </summary>
internal static class ListModelsCommand
{
    /// <summary>
    ///     Runs the <c>list-models</c> subcommand against a real, composed
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
    ///     Runs the <c>list-models</c> subcommand against an injected catalog seam, for unit
    ///     testing without a real <see cref="SpeechModelCatalog"/>.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="catalog">The catalog seam to enumerate. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="catalog"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when an unsupported flag or flag value is given.</exception>
    internal static void Run(Context context, ICliModelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);

        var options = ParseArguments(context.CommandArgs);

        var descriptors = catalog
            .Enumerate()
            .Where(descriptor => options.Role is null || descriptor.Role == options.Role)
            .Where(descriptor => !options.MissingOnly || descriptor.State != SpeechModelState.Downloaded)
            .Where(descriptor => !options.DownloadedOnly || descriptor.State == SpeechModelState.Downloaded)
            .ToList();

        if (options.Format == OutputFormat.Json)
        {
            WriteJson(context, descriptors);
        }
        else
        {
            WriteTable(context, descriptors);
        }
    }

    /// <summary>
    ///     Parses <c>list-models</c>' own flags: <c>--role tts|stt</c>,
    ///     <c>--state downloaded|missing</c>, and <c>--format table|json</c>.
    /// </summary>
    private static ListModelsOptions ParseArguments(IReadOnlyList<string> args)
    {
        SpeechModelRole? role = null;
        var missingOnly = false;
        var downloadedOnly = false;
        var format = OutputFormat.Table;

        var index = 0;
        while (index < args.Count)
        {
            var arg = args[index++];
            switch (arg)
            {
                case "--role":
                    var roleValue = RequireValue(args, ref index, "--role");
                    role = roleValue switch
                    {
                        "tts" => SpeechModelRole.Synthesis,
                        "stt" => SpeechModelRole.Recognition,
                        _ => throw new ArgumentException($"--role must be 'tts' or 'stt', not '{roleValue}'.", nameof(args))
                    };
                    break;

                case "--state":
                    var stateValue = RequireValue(args, ref index, "--state");
                    switch (stateValue)
                    {
                        case "downloaded":
                            downloadedOnly = true;
                            break;
                        case "missing":
                            missingOnly = true;
                            break;
                        default:
                            throw new ArgumentException($"--state must be 'downloaded' or 'missing', not '{stateValue}'.", nameof(args));
                    }

                    break;

                case "--format":
                    var formatValue = RequireValue(args, ref index, "--format");
                    format = formatValue switch
                    {
                        "table" => OutputFormat.Table,
                        "json" => OutputFormat.Json,
                        _ => throw new ArgumentException($"--format must be 'table' or 'json', not '{formatValue}'.", nameof(args))
                    };
                    break;

                default:
                    throw new ArgumentException($"Unsupported argument '{arg}' for 'list-models'.", nameof(args));
            }
        }

        return new ListModelsOptions(role, missingOnly, downloadedOnly, format);
    }

    /// <summary>
    ///     Gets the value following a flag, throwing a clean <see cref="ArgumentException"/> when
    ///     none was supplied.
    /// </summary>
    private static string RequireValue(IReadOnlyList<string> args, ref int index, string flag)
    {
        if (index >= args.Count)
        {
            throw new ArgumentException($"{flag} requires a value.", nameof(args));
        }

        return args[index++];
    }

    /// <summary>
    ///     Writes the given descriptors as an aligned, human-readable table.
    /// </summary>
    private static void WriteTable(Context context, List<SpeechModelDescriptor> descriptors)
    {
        if (descriptors.Count == 0)
        {
            context.WriteLine("No models match the given filters.");
            return;
        }

        var idWidth = Math.Max("Id".Length, descriptors.Max(d => d.Id.Length));
        var nameWidth = Math.Max("Display Name".Length, descriptors.Max(d => d.DisplayName.Length));
        var roleWidth = Math.Max("Role".Length, descriptors.Max(d => d.Role.ToString().Length));
        var stateWidth = Math.Max("State".Length, descriptors.Max(d => d.State.ToString().Length));

        string FormatRow(string id, string displayName, string role, string state, string license) =>
            $"{id.PadRight(idWidth)}  {displayName.PadRight(nameWidth)}  {role.PadRight(roleWidth)}  {state.PadRight(stateWidth)}  {license}";

        context.WriteLine(FormatRow("Id", "Display Name", "Role", "State", "License"));
        context.WriteLine(FormatRow(
            new string('-', idWidth),
            new string('-', nameWidth),
            new string('-', roleWidth),
            new string('-', stateWidth),
            new string('-', "License".Length)));

        foreach (var descriptor in descriptors)
        {
            context.WriteLine(FormatRow(
                descriptor.Id,
                descriptor.DisplayName,
                descriptor.Role.ToString(),
                descriptor.State.ToString(),
                descriptor.LicenseName));
        }
    }

    /// <summary>
    ///     Writes the given descriptors as an indented JSON array.
    /// </summary>
    private static void WriteJson(Context context, List<SpeechModelDescriptor> descriptors)
    {
        var rows = descriptors
            .Select(descriptor => new ModelRow(
                descriptor.Id,
                descriptor.DisplayName,
                descriptor.Role.ToString(),
                descriptor.State.ToString(),
                descriptor.LicenseName))
            .ToList();

        context.WriteLine(JsonSerializer.Serialize(rows, JsonOptions));
    }

    /// <summary>Shared, indented JSON serialization options for this command's output.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>The output format requested by <c>--format</c>.</summary>
    private enum OutputFormat
    {
        /// <summary>An aligned, human-readable table (the default).</summary>
        Table,

        /// <summary>An indented JSON array.</summary>
        Json,
    }

    /// <summary>The parsed <c>list-models</c> flags.</summary>
    private sealed record ListModelsOptions(SpeechModelRole? Role, bool MissingOnly, bool DownloadedOnly, OutputFormat Format);

    /// <summary>One JSON-serializable model row: Id, DisplayName, Role, State, LicenseName.</summary>
    private sealed record ModelRow(string Id, string DisplayName, string Role, string State, string LicenseName);
}
