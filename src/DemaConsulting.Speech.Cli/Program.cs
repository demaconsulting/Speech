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

using System.Reflection;
using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.Cli.SelfTest;

namespace DemaConsulting.Speech.Cli;

/// <summary>
///     Main program entry point for the Speech CLI (<c>speech-cli</c>).
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Gets the application version string.
    /// </summary>
    /// <remarks>
    ///     The version is read from the <see cref="AssemblyInformationalVersionAttribute"/> via
    ///     reflection on every access. There is no caching; callers that need the value more than
    ///     once should store the result locally.
    /// </remarks>
    public static string Version
    {
        get
        {
            // Get the assembly containing this program
            var assembly = typeof(Program).Assembly;

            // Try to get version from assembly attributes, fallback to AssemblyVersion, or default to 0.0.0
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString()
                   ?? "0.0.0";
        }
    }

    /// <summary>
    ///     Main entry point for the Speech CLI.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code: 0 for success, non-zero for failure.</returns>
    /// <exception cref="Exception">Thrown when an unexpected error occurs; re-thrown after writing to stderr.</exception>
    /// <remarks>
    ///     <see cref="ArgumentException"/> and <see cref="InvalidOperationException"/> are treated as
    ///     expected errors: their messages are written to stderr and exit code 1 is returned without
    ///     a stack trace. Any other exception is written to stderr and then re-thrown so that the
    ///     runtime can record it in event logs. All three of these stderr writes honor
    ///     <c>--silent</c> (see <see cref="IsSilentRequested"/>), consistent with how
    ///     <see cref="Context.WriteError"/> suppresses console output elsewhere in the CLI;
    ///     <c>--silent</c> only ever suppresses the console write, never the exit code or the
    ///     unexpected-exception rethrow.
    /// </remarks>
    public static int Main(string[] args)
    {
        // Declared outside the try block (rather than `using var context = ...`) so the catch
        // blocks below can consult context.Silent when Context.Create succeeded but a later
        // step failed - `using var` scopes its variable to the try block alone, which would
        // make it unreachable exactly where the silence-aware error handling needs it.
        Context? context = null;
        try
        {
            // Create context from command-line arguments
            context = Context.Create(args);
            using var contextLease = context;

            // Run the program logic
            Run(context);

            // Return the exit code from the context
            return context.ExitCode;
        }
        catch (ArgumentException ex)
        {
            // Print expected argument exceptions and return error code, honoring --silent
            if (!IsSilentRequested(context, args))
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }

            return 1;
        }
        catch (InvalidOperationException ex)
        {
            // Print expected operation exceptions and return error code, honoring --silent
            if (!IsSilentRequested(context, args))
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }

            return 1;
        }
        catch (Exception ex)
        {
            // Print unexpected exceptions (honoring --silent) and re-throw to generate event
            // logs. --silent only suppresses the console write here; the rethrow still
            // preserves diagnostics via crash/event logs regardless of --silent.
            if (!IsSilentRequested(context, args))
            {
                Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            }

            throw;
        }
    }

    /// <summary>
    ///     Determines whether console error output should be suppressed for <c>--silent</c>.
    /// </summary>
    /// <param name="context">
    ///     The constructed context, or <see langword="null"/> when <see cref="Context.Create"/>
    ///     itself threw before a context could be built (for example, an unrecognized
    ///     subcommand name).
    /// </param>
    /// <param name="args">The original, unparsed command-line arguments.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="context"/> reports <see cref="Context.Silent"/>,
    ///     or, when no context exists yet, when <paramref name="args"/> contains the literal
    ///     <c>--silent</c> token; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///     A raw token scan is only ever used as a fallback for the case where argument parsing
    ///     itself failed before <see cref="Context.Silent"/> could be resolved through the full
    ///     parser (which also recognizes <c>--silent</c> anywhere among the arguments); once a
    ///     <see cref="Context"/> exists, its already-parsed <see cref="Context.Silent"/> value is
    ///     used directly instead of re-scanning.
    /// </remarks>
    private static bool IsSilentRequested(Context? context, string[] args)
    {
        return context?.Silent ?? Array.IndexOf(args, "--silent") >= 0;
    }

    /// <summary>
    ///     Runs the program logic based on the provided context.
    /// </summary>
    /// <param name="context">The context containing command line arguments and program state.</param>
    /// <remarks>
    ///     Dispatch is priority-ordered: version check first, then help, then self-validation,
    ///     then subcommand dispatch. Only the highest-priority matching action is executed per
    ///     invocation.
    /// </remarks>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Priority 1: Version query
        if (context.Version)
        {
            context.WriteLine(Version);
            return;
        }

        // Print application banner
        PrintBanner(context);

        // Priority 2: Help
        if (context.Help)
        {
            PrintHelp(context);
            return;
        }

        // Priority 3: Self-Validation
        if (context.Validate)
        {
            Validation.Run(context);
            return;
        }

        // Priority 4: Subcommand dispatch
        Dispatch(context);
    }

    /// <summary>
    ///     Prints the application banner.
    /// </summary>
    /// <param name="context">The context for output.</param>
    private static void PrintBanner(Context context)
    {
        context.WriteLine($"Speech CLI version {Version}");
        context.WriteLine("Copyright (c) DEMA Consulting");
        context.WriteLine("");
    }

    /// <summary>
    ///     Prints usage information, including every recognized subcommand and its expected flags.
    /// </summary>
    /// <param name="context">The context for output.</param>
    private static void PrintHelp(Context context)
    {
        context.WriteLine("Usage: speech-cli [global options] <command> [command options]");
        context.WriteLine("");
        context.WriteLine("Global options:");
        context.WriteLine("  -v, --version              Display version information");
        context.WriteLine("  -?, -h, --help             Display this help message");
        context.WriteLine("  --silent                   Suppress console output");
        context.WriteLine("  --validate                 Run self-validation");
        context.WriteLine("  --results <file>           Write validation results to file (.trx or .xml)");
        context.WriteLine("  --depth <#>                Set heading depth for markdown output (default: 1)");
        context.WriteLine("  --log <file>               Write output to log file");
        context.WriteLine("  --models-dir <path>        Override the model store root directory");
        context.WriteLine("  --verbose, --diagnostics   Enable verbose diagnostics output");
        context.WriteLine("");
        context.WriteLine("Commands:");
        foreach (var command in CommandDispatch.Commands)
        {
            context.WriteLine($"  {command.UsageSummary}");
        }

        context.WriteLine("");
        context.WriteLine("Each command above is listed with its full flag summary; run");
        context.WriteLine("'speech-cli doctor' to check the local audio, model, and native-runtime environment.");
    }

    /// <summary>
    ///     Dispatches to the resolved subcommand's handler.
    /// </summary>
    /// <param name="context">The context containing command line arguments and program state.</param>
    /// <remarks>
    ///     When no subcommand was given, prints the same usage information as <c>--help</c>
    ///     rather than treating the absence of a subcommand as an error. <see cref="Context.Create"/>
    ///     already rejects an unrecognized subcommand name as an <see cref="ArgumentException"/>
    ///     before <see cref="Run"/> is ever reached, so every <see cref="Context.Command"/> value
    ///     that reaches this method is guaranteed to resolve via <see cref="CommandDispatch.Find"/>.
    /// </remarks>
    private static void Dispatch(Context context)
    {
        if (context.Command == null)
        {
            PrintHelp(context);
            return;
        }

        var command = CommandDispatch.Find(context.Command) ??
            throw new InvalidOperationException($"Internal error: unresolved command '{context.Command}'.");

        command.Handler(context);
    }
}
