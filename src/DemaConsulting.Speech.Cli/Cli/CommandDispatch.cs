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

using DemaConsulting.Speech.Cli.Commands.DeviceCommandsSubsystem;
using DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.Cli.Commands.RecognitionCommandSubsystem;
using DemaConsulting.Speech.Cli.Commands.SynthesisCommandSubsystem;

namespace DemaConsulting.Speech.Cli.Cli;

/// <summary>
///     Describes one subcommand recognized by the CLI's dispatch table: its canonical name,
///     a one-line summary of its expected flags for <c>--help</c> output, and the handler that
///     runs when the subcommand is invoked.
/// </summary>
/// <param name="Name">The subcommand's canonical, kebab-case name (e.g. <c>list-models</c>).</param>
/// <param name="UsageSummary">
///     A one-line summary of the subcommand's expected flags, shown by <c>--help</c>.
/// </param>
/// <param name="Handler">
///     The action that runs when this subcommand is dispatched. As of Pass 6, every subcommand
///     handler is a real implementation; no dispatch entry remains a
///     <see cref="NotImplementedException"/> stub.
/// </param>
internal sealed record CommandDescriptor(string Name, string UsageSummary, Action<Context> Handler);

/// <summary>
///     Defines the fixed set of subcommands the CLI recognizes and dispatches to.
/// </summary>
/// <remarks>
///     Every recognized subcommand name has a real implementation as of Pass 6, which replaced
///     the last remaining stub (<c>recognize</c>).
/// </remarks>
internal static class CommandDispatch
{
    /// <summary>
    ///     Gets the ordered list of recognized subcommands, in the order they should appear in
    ///     <c>--help</c> output.
    /// </summary>
    public static IReadOnlyList<CommandDescriptor> Commands { get; } =
    [
        new CommandDescriptor(
            "list-models",
            "list-models [--role tts|stt] [--state downloaded|missing] [--format table|json]",
            ListModelsCommand.Run),
        new CommandDescriptor(
            "model-info",
            "model-info <modelId>",
            ModelInfoCommand.Run),
        new CommandDescriptor(
            "download",
            "download <modelId> [<modelId>...] [--force]",
            DownloadCommand.Run),
        new CommandDescriptor(
            "uninstall",
            "uninstall <modelId>",
            UninstallCommand.Run),
        new CommandDescriptor(
            "clean",
            "clean <modelId>",
            CleanCommand.Run),
        new CommandDescriptor(
            "list-devices",
            "list-devices [--direction input|output]",
            ListDevicesCommand.Run),
        new CommandDescriptor(
            "devices",
            "devices test [--device <name>] [--direction input|output]",
            DevicesTestCommand.Run),
        new CommandDescriptor(
            "doctor",
            "doctor",
            DoctorCommand.Run),
        new CommandDescriptor(
            "speak",
            "speak --model <id> (--text <string> | --file <path> | stdin) [--output <wav-path>] [--device <name>] [--param key=value...] [--no-tags]",
            SpeakCommand.Run),
        new CommandDescriptor(
            "recognize",
            "recognize --model <id> (--input <wav-path> | --mic) [--device <name>] [--silence-timeout <seconds>] [--start-timeout <seconds>] [--param key=value...] [--interim | --final-only] [--output <text-path>]",
            RecognizeCommand.Run)
    ];

    /// <summary>
    ///     Looks up the command descriptor for the given subcommand name.
    /// </summary>
    /// <param name="name">The subcommand name to look up.</param>
    /// <returns>The matching <see cref="CommandDescriptor"/>, or <see langword="null"/> if none matches.</returns>
    public static CommandDescriptor? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Commands.FirstOrDefault(command => string.Equals(command.Name, name, StringComparison.Ordinal));
    }
}
