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

using System.Net;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Cli;

/// <summary>
///     Context class that handles command-line arguments and program output for the Speech CLI.
/// </summary>
/// <remarks>
///     Extends the hand-rolled, single-purpose parser pattern used by
///     <c>DemaConsulting.TemplateDotNetTool</c>'s own <c>Context</c> with subcommand dispatch:
///     <see cref="Command"/> is resolved from the first argument that is not one of the global
///     options below, and every argument that follows it (other than further global options) is
///     collected, unparsed, into <see cref="CommandArgs"/> for the subcommand's own handler to
///     interpret. Global options (<see cref="ModelsDir"/>,
///     <see cref="Diagnostics"/>, <see cref="Silent"/>, log file, version, help) are recognized
///     regardless of where they appear relative to the subcommand name.
/// </remarks>
internal sealed class Context : IDisposable
{
    /// <summary>
    ///     Name of the environment variable that supplies the mirror base URL when <c>--mirror-url</c> is absent.
    /// </summary>
    public const string MirrorUrlEnvironmentVariable = "DEMASPEECH_MIRROR_URL";

    /// <summary>
    ///     Name of the environment variable that supplies the mirror HTTP Basic username.
    /// </summary>
    public const string MirrorUserEnvironmentVariable = "DEMASPEECH_MIRROR_USER";

    /// <summary>
    ///     Name of the environment variable that supplies the mirror HTTP Basic password.
    /// </summary>
    public const string MirrorPasswordEnvironmentVariable = "DEMASPEECH_MIRROR_PASSWORD";

    /// <summary>
    ///     Name of the environment variable that supplies the mirror bearer token.
    /// </summary>
    public const string MirrorBearerTokenEnvironmentVariable = "DEMASPEECH_MIRROR_BEARER_TOKEN";

    /// <summary>
    ///     Reads an environment variable, treating an empty value as unset.
    /// </summary>
    private static string? ReadEnvironment(Func<string, string?> getEnvironmentVariable, string name)
    {
        var value = getEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    ///     Log file stream writer (if logging is enabled).
    /// </summary>
    private StreamWriter? _logWriter;

    /// <summary>
    ///     Indicates whether errors have been reported.
    /// </summary>
    private bool _hasErrors;

    /// <summary>
    ///     Gets a value indicating whether the version flag was specified.
    /// </summary>
    public bool Version { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the help flag was specified.
    /// </summary>
    public bool Help { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the silent flag was specified.
    /// </summary>
    public bool Silent { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether <c>--no-emotions</c> was specified, which makes <c>voices install</c> extract
    ///     only the base voices of a pack and skip its emotion (mood) clips. It has no effect on other commands.
    /// </summary>
    public bool NoEmotions { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the validate flag was specified.
    /// </summary>
    public bool Validate { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether verbose/diagnostics output was requested
    ///     (<c>--verbose</c> or <c>--diagnostics</c>).
    /// </summary>
    public bool Diagnostics { get; private init; }

    /// <summary>
    ///     Gets the models directory override supplied via <c>--models-dir</c>, or
    ///     <see langword="null"/> to use the library's default per-user model store location.
    /// </summary>
    public string? ModelsDir { get; private init; }

    /// <summary>
    ///     Gets the extra reference-voice directory supplied via <c>--voices-dir</c>, or
    ///     <see langword="null"/> when none was given. When set, it is guaranteed to exist.
    /// </summary>
    public string? VoicesDir { get; private init; }

    /// <summary>
    ///     Gets the internal download mirror base URL supplied via <c>--mirror-url</c>, or
    ///     <see langword="null"/> to download every model from its own declared public URI
    ///     (the default).
    /// </summary>
    public string? MirrorUrl { get; private init; }

    /// <summary>
    ///     Gets the HTTP Basic username supplied via <c>--mirror-user</c>, or
    ///     <see langword="null"/> when the mirror needs no Basic credential.
    /// </summary>
    public string? MirrorUser { get; private init; }

    /// <summary>
    ///     Gets the HTTP Basic password supplied via <c>--mirror-password</c>, or
    ///     <see langword="null"/> when the mirror needs no Basic credential.
    /// </summary>
    public string? MirrorPassword { get; private init; }

    /// <summary>
    ///     Gets the bearer token supplied via <c>--mirror-bearer-token</c>, or
    ///     <see langword="null"/> when the mirror needs no bearer token.
    /// </summary>
    public string? MirrorBearerToken { get; private init; }

    /// <summary>
    ///     Resolves this context's <c>--mirror-*</c> options into a <see cref="DownloadMirror"/>,
    ///     for <see cref="Commands.ModelCommandsSubsystem.CliModelCatalogFactory"/> to pass
    ///     through to the library catalog.
    /// </summary>
    /// <returns>
    ///     A new <see cref="DownloadMirror"/> when <see cref="MirrorUrl"/> was supplied;
    ///     otherwise <see langword="null"/> (every model downloads from its own declared public
    ///     URI).
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when any of <see cref="MirrorUser"/>, <see cref="MirrorPassword"/>, or
    ///     <see cref="MirrorBearerToken"/> is supplied without <see cref="MirrorUrl"/> (a
    ///     credential with nowhere to apply almost always indicates a missing or misspelled
    ///     <c>--mirror-url</c> rather than an intentional no-op), when
    ///     <see cref="MirrorUser"/> is supplied without <see cref="MirrorPassword"/> (or vice
    ///     versa), when both Basic credentials and a bearer token are supplied, when
    ///     <see cref="MirrorUrl"/> is not a valid absolute URI, or for any other configuration
    ///     <see cref="DownloadMirror"/>'s own constructor rejects (for example a non-HTTPS mirror
    ///     combined with a credential, on a non-loopback host).
    /// </exception>
    public DownloadMirror? CreateMirror()
    {
        if (string.IsNullOrEmpty(MirrorUrl))
        {
            if (MirrorUser is not null || MirrorPassword is not null || MirrorBearerToken is not null)
            {
                throw new ArgumentException(
                    "--mirror-user, --mirror-password, and --mirror-bearer-token (and their " +
                    "DEMASPEECH_MIRROR_* environment variables) require a mirror URL " +
                    "(--mirror-url or DEMASPEECH_MIRROR_URL); none of them has any effect " +
                    "without a mirror URL to apply them to.");
            }

            return null;
        }

        if (!Uri.TryCreate(MirrorUrl, UriKind.Absolute, out var baseUri))
        {
            // The rejected value is deliberately not echoed: a mistyped URL can still carry a pasted credential.
            throw new ArgumentException("The mirror URL is not a valid absolute URL.");
        }

        if (MirrorUser is null != MirrorPassword is null)
        {
            throw new ArgumentException(
                "--mirror-user and --mirror-password must both be supplied together.");
        }

        var credentials = MirrorUser is not null ? new NetworkCredential(MirrorUser, MirrorPassword) : null;
        return new DownloadMirror(baseUri, credentials, MirrorBearerToken);
    }

    /// <summary>
    ///     Gets the validation results file path.
    /// </summary>
    public string? ResultsFile { get; private init; }

    /// <summary>
    ///     Gets the heading depth for markdown output (default is 1).
    /// </summary>
    public int HeadingDepth { get; private init; } = 1;

    /// <summary>
    ///     Gets the resolved subcommand name (e.g. <c>list-models</c>), or <see langword="null"/>
    ///     when no subcommand was given on the command line.
    /// </summary>
    public string? Command { get; private init; }

    /// <summary>
    ///     Gets the raw, unparsed arguments that followed <see cref="Command"/> on the command
    ///     line (excluding any recognized global option and its value). Each subcommand's own
    ///     handler is responsible for parsing these.
    /// </summary>
    public IReadOnlyList<string> CommandArgs { get; private init; } = [];

    /// <summary>
    ///     Gets the proposed exit code for the application (0 for success, 1 for errors).
    /// </summary>
    public int ExitCode => _hasErrors ? 1 : 0;

    /// <summary>
    ///     Private constructor - use Create factory method instead.
    /// </summary>
    private Context()
    {
    }

    /// <summary>
    ///     Creates a Context instance from command-line arguments, reading the mirror settings'
    ///     environment-variable fallbacks from the process environment.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A new Context instance.</returns>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the specified log file cannot be opened.</exception>
    public static Context Create(string[] args) => Create(args, Environment.GetEnvironmentVariable);

    /// <summary>
    ///     Creates a Context instance from command-line arguments, reading the mirror settings'
    ///     environment-variable fallbacks through <paramref name="getEnvironmentVariable"/>.
    /// </summary>
    /// <remarks>
    ///     A <c>--mirror-*</c> option given on the command line always takes precedence over its
    ///     environment variable: <see cref="MirrorUrlEnvironmentVariable"/> applies only when
    ///     <c>--mirror-url</c> is absent, and the credential variables
    ///     (<see cref="MirrorUserEnvironmentVariable"/>, <see cref="MirrorPasswordEnvironmentVariable"/>,
    ///     <see cref="MirrorBearerTokenEnvironmentVariable"/>) apply only when none of
    ///     <c>--mirror-user</c>, <c>--mirror-password</c>, or <c>--mirror-bearer-token</c> was given, so
    ///     a command-line credential is never mixed with an environment one. An empty variable is
    ///     treated as unset. A variable's value is never written to any output or error message.
    /// </remarks>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="getEnvironmentVariable">
    ///     Reads an environment variable by name, returning <see langword="null"/> when it is unset.
    /// </param>
    /// <returns>A new Context instance.</returns>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the specified log file cannot be opened.</exception>
    public static Context Create(string[] args, Func<string, string?> getEnvironmentVariable)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var parser = new ArgumentParser();
        parser.ParseArguments(args);

        // Explicit command-line options win; credentials come from one source only, never a mixture.
        var credentialsOnCommandLine =
            parser.MirrorUser is not null || parser.MirrorPassword is not null || parser.MirrorBearerToken is not null;

        var result = new Context
        {
            Version = parser.Version,
            Help = parser.Help,
            Silent = parser.Silent,
            NoEmotions = parser.NoEmotions,
            Validate = parser.Validate,
            Diagnostics = parser.Diagnostics,
            ModelsDir = parser.ModelsDir,
            VoicesDir = parser.VoicesDir,
            MirrorUrl = parser.MirrorUrl ?? ReadEnvironment(getEnvironmentVariable, MirrorUrlEnvironmentVariable),
            MirrorUser = credentialsOnCommandLine
                ? parser.MirrorUser
                : ReadEnvironment(getEnvironmentVariable, MirrorUserEnvironmentVariable),
            MirrorPassword = credentialsOnCommandLine
                ? parser.MirrorPassword
                : ReadEnvironment(getEnvironmentVariable, MirrorPasswordEnvironmentVariable),
            MirrorBearerToken = credentialsOnCommandLine
                ? parser.MirrorBearerToken
                : ReadEnvironment(getEnvironmentVariable, MirrorBearerTokenEnvironmentVariable),
            ResultsFile = parser.ResultsFile,
            HeadingDepth = parser.HeadingDepth,
            Command = parser.Command,
            CommandArgs = parser.CommandArgs.AsReadOnly()
        };

        // Validate mirror options up front so every invocation (including --help/--version) rejects bad combinations
        _ = result.CreateMirror();

        // Validate --voices-dir up front so a mistyped folder fails fast rather than silently listing no voices
        if (result.VoicesDir is not null && !Directory.Exists(result.VoicesDir))
        {
            throw new ArgumentException($"--voices-dir '{result.VoicesDir}' is not an existing directory.");
        }

        // Open log file if specified
        if (parser.LogFile != null)
        {
            result.OpenLogFile(parser.LogFile);
        }

        return result;
    }

    /// <summary>
    ///     Opens the log file for writing
    /// </summary>
    /// <param name="logFile">Log file path</param>
    private void OpenLogFile(string logFile)
    {
        try
        {
            // Open with AutoFlush enabled so log entries are immediately written to disk
            // even if the application terminates unexpectedly before Dispose is called
            _logWriter = new StreamWriter(logFile, append: false) { AutoFlush = true };
        }
        // Intentionally broad: this is a CLI setup boundary, so any file-system failure while
        // opening the requested log sink must be wrapped into one clean, contextual error
        // message instead of crashing the whole process with a raw platform exception.
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open log file '{logFile}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Helper class for parsing command-line arguments
    /// </summary>
    private sealed class ArgumentParser
    {
        /// <summary>
        ///     Gets a value indicating whether the version flag was specified.
        /// </summary>
        public bool Version { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the help flag was specified.
        /// </summary>
        public bool Help { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the silent flag was specified.
        /// </summary>
        public bool Silent { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the no-emotions flag was specified.
        /// </summary>
        public bool NoEmotions { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the validate flag was specified.
        /// </summary>
        public bool Validate { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether verbose/diagnostics output was requested.
        /// </summary>
        public bool Diagnostics { get; private set; }

        /// <summary>
        ///     Gets the models directory override.
        /// </summary>
        public string? ModelsDir { get; private set; }

        /// <summary>
        ///     Gets the extra reference-voice directory.
        /// </summary>
        public string? VoicesDir { get; private set; }

        /// <summary>
        ///     Gets the internal download mirror base URL.
        /// </summary>
        public string? MirrorUrl { get; private set; }

        /// <summary>
        ///     Gets the HTTP Basic username for the mirror.
        /// </summary>
        public string? MirrorUser { get; private set; }

        /// <summary>
        ///     Gets the HTTP Basic password for the mirror.
        /// </summary>
        public string? MirrorPassword { get; private set; }

        /// <summary>
        ///     Gets the bearer token for the mirror.
        /// </summary>
        public string? MirrorBearerToken { get; private set; }

        /// <summary>
        ///     Gets the log file path.
        /// </summary>
        public string? LogFile { get; private set; }

        /// <summary>
        ///     Gets the validation results file path.
        /// </summary>
        public string? ResultsFile { get; private set; }

        /// <summary>
        ///     Gets the heading depth for markdown output.
        /// </summary>
        public int HeadingDepth { get; private set; } = 1;

        /// <summary>
        ///     Gets the resolved subcommand name, or <see langword="null"/> if none was given.
        /// </summary>
        public string? Command { get; private set; }

        /// <summary>
        ///     Gets the raw, unparsed arguments collected after the subcommand name.
        /// </summary>
        public List<string> CommandArgs { get; } = [];

        /// <summary>
        ///     Parses command-line arguments
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        public void ParseArguments(string[] args)
        {
            // Validate input
            ArgumentNullException.ThrowIfNull(args);

            var i = 0;
            while (i < args.Length)
            {
                var arg = args[i++];
                i = ParseArgument(arg, args, i);
            }
        }

        /// <summary>
        ///     Parses a single argument. Recognized global options are handled regardless of
        ///     whether a subcommand has already been resolved. The first argument that is not a
        ///     recognized global option is treated as the subcommand name (if none is resolved
        ///     yet) or, once a subcommand is resolved, is collected verbatim into
        ///     <see cref="CommandArgs"/> for that subcommand's own handler to parse later.
        /// </summary>
        /// <param name="arg">Argument to parse</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <returns>Updated index</returns>
        private int ParseArgument(string arg, string[] args, int index)
        {
            switch (arg)
            {
                case "-v":
                case "--version":
                    Version = true;
                    return index;

                case "-?":
                case "-h":
                case "--help":
                    Help = true;
                    return index;

                case "--silent":
                    Silent = true;
                    return index;

                case "--no-emotions":
                    NoEmotions = true;
                    return index;

                case "--validate":
                    Validate = true;
                    return index;

                case "--verbose":
                case "--diagnostics":
                    Diagnostics = true;
                    return index;

                case "--models-dir":
                    ModelsDir = GetRequiredStringArgument(arg, args, index, "a directory path argument");
                    return index + 1;

                case "--voices-dir":
                    VoicesDir = GetRequiredStringArgument(arg, args, index, "a directory path argument");
                    return index + 1;

                case "--mirror-url":
                    MirrorUrl = GetRequiredStringArgument(arg, args, index, "a mirror base URL argument");
                    return index + 1;

                case "--mirror-user":
                    MirrorUser = GetRequiredStringArgument(arg, args, index, "a mirror username argument");
                    return index + 1;

                case "--mirror-password":
                    MirrorPassword = GetRequiredStringArgument(arg, args, index, "a mirror password argument");
                    return index + 1;

                case "--mirror-bearer-token":
                    MirrorBearerToken = GetRequiredStringArgument(arg, args, index, "a mirror bearer token argument");
                    return index + 1;

                case "--log":
                    LogFile = GetRequiredStringArgument(arg, args, index, "a filename argument");
                    return index + 1;

                case "--results":
                case "--result":
                    ResultsFile = GetRequiredStringArgument(arg, args, index, "a results filename argument");
                    return index + 1;

                case "--depth":
                    HeadingDepth = GetRequiredIntArgument(arg, args, index, "a heading depth argument", 1, 6);
                    return index + 1;

                default:
                    return ParseCommandArgument(arg, index);
            }
        }

        /// <summary>
        ///     Resolves the subcommand name (if not already resolved) or collects the argument as
        ///     a subcommand argument.
        /// </summary>
        /// <param name="arg">The argument that did not match any recognized global option.</param>
        /// <param name="index">The current parser index (unchanged; this argument takes no value).</param>
        /// <returns>The unchanged <paramref name="index"/>.</returns>
        /// <exception cref="ArgumentException">
        ///     Thrown when no subcommand is resolved yet and <paramref name="arg"/> does not name
        ///     a recognized subcommand.
        /// </exception>
        private int ParseCommandArgument(string arg, int index)
        {
            if (Command == null)
            {
                if (CommandDispatch.Find(arg) == null)
                {
                    throw new ArgumentException($"Unsupported argument '{arg}'", nameof(arg));
                }

                Command = arg;
                return index;
            }

            CommandArgs.Add(arg);
            return index;
        }

        /// <summary>
        ///     Gets a required string argument value
        /// </summary>
        /// <param name="arg">Argument name</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <param name="description">Description of what's required</param>
        /// <returns>Argument value</returns>
        private static string GetRequiredStringArgument(string arg, string[] args, int index, string description)
        {
            if (index >= args.Length)
            {
                throw new ArgumentException($"{arg} requires {description}", nameof(args));
            }

            return args[index];
        }

        /// <summary>
        ///     Gets a required integer argument value
        /// </summary>
        /// <param name="arg">Argument name</param>
        /// <param name="args">All arguments</param>
        /// <param name="index">Current index</param>
        /// <param name="description">Description of what's required</param>
        /// <param name="min">Minimum valid value (inclusive)</param>
        /// <param name="max">Maximum valid value (inclusive)</param>
        /// <returns>Argument value as an integer in [min, max]</returns>
        private static int GetRequiredIntArgument(string arg, string[] args, int index, string description, int min = 1, int max = int.MaxValue)
        {
            var value = GetRequiredStringArgument(arg, args, index, description);
            if (!int.TryParse(value, out var result) || result < min || result > max)
            {
                throw new ArgumentException($"{arg} requires an integer between {min} and {max} for {description}", nameof(args));
            }

            return result;
        }
    }

    /// <summary>
    ///     Writes a line of output to the console and log file (if logging is enabled).
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <remarks>
    ///     Output is written to stdout. When <see cref="Silent"/> is <c>true</c>, stdout output is
    ///     suppressed, but the message is still written to the log file when one is open.
    /// </remarks>
    public void WriteLine(string message)
    {
        // Write to console unless silent mode is enabled
        if (!Silent)
        {
            Console.WriteLine(message);
        }

        // Write to log file if logging is enabled
        _logWriter?.WriteLine(message);
    }

    /// <summary>
    ///     Writes an error message to the error console and log file (if logging is enabled).
    /// </summary>
    /// <param name="message">The error message to write.</param>
    /// <remarks>
    ///     <c>_hasErrors</c> is set to <c>true</c> unconditionally, so <see cref="ExitCode"/> will
    ///     return 1 regardless of whether <see cref="Silent"/> suppresses the console output.
    ///     Stderr output is suppressed when <see cref="Silent"/> is <c>true</c>, but the message
    ///     is still written to the log file when one is open.
    /// </remarks>
    public void WriteError(string message)
    {
        // Mark that we have encountered errors
        _hasErrors = true;

        // Write to error console unless silent mode is enabled
        if (!Silent)
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ForegroundColor = previousColor;
        }

        // Write to log file if logging is enabled
        _logWriter?.WriteLine(message);
    }

    /// <summary>
    ///     Disposes resources used by the Context.
    /// </summary>
    public void Dispose()
    {
        // Close and dispose the log file writer if it exists
        _logWriter?.Dispose();
        _logWriter = null;
    }
}
