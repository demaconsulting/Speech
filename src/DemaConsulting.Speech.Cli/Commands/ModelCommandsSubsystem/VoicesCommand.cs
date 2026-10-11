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
using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;

/// <summary>
///     Implements the <c>voices</c> subcommand: <c>voices packs</c> lists the installable
///     reference-voice packs with their state, <c>voices install &lt;packId&gt;</c> downloads and
///     installs one (honoring <c>--mirror-*</c> and <c>--models-dir</c>, with live progress), and
///     <c>voices uninstall &lt;packId&gt;</c> removes one.
/// </summary>
/// <remarks>
///     The Chatterbox Turbo voice list is read when the catalog is built, so a newly installed
///     pack's voices appear in <c>list-voices</c> and <c>speak</c> on the next invocation. Every
///     install prints <see cref="ReferenceVoiceLibrary.ConsentNotice"/>.
/// </remarks>
internal static class VoicesCommand
{
    /// <summary>
    ///     Runs the <c>voices</c> subcommand against a real, composed
    ///     <see cref="SpeechModelCatalogAdapter"/>, wiring <c>Ctrl+C</c> to cooperative cancellation.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the sub-action or its argument is missing or unsupported.</exception>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var catalog = CliModelCatalogFactory.Create(context);
        using var cancellationSource = new CancellationTokenSource();

        ConsoleCancelEventHandler onCancelKeyPress = (_, e) =>
        {
            e.Cancel = true;
            cancellationSource.Cancel();
        };

        Console.CancelKeyPress += onCancelKeyPress;
        try
        {
            RunAsync(context, catalog, cancellationSource.Token).GetAwaiter().GetResult();
        }
        finally
        {
            Console.CancelKeyPress -= onCancelKeyPress;
        }
    }

    /// <summary>
    ///     Runs the <c>voices</c> subcommand against an injected catalog seam, for unit testing
    ///     without a real store or network access.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="catalog">The catalog seam. Must not be null.</param>
    /// <param name="cancellationToken">A token that, when canceled, aborts an in-progress install.</param>
    /// <returns>A task that completes once the sub-action has finished.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="catalog"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the sub-action or its argument is missing, unsupported, or names an unknown pack.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an uninstall could not remove the pack's files.</exception>
    internal static async Task RunAsync(Context context, ICliModelCatalog catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);

        var args = context.CommandArgs;
        if (args.Count == 0)
        {
            throw new ArgumentException("voices requires a sub-action: packs, install <packId>, or uninstall <packId>.", nameof(context));
        }

        switch (args[0])
        {
            case "packs":
                RequireArgumentCount(args, 1);
                WritePacks(context, catalog);
                break;

            case "install":
                RequireArgumentCount(args, 2);
                await InstallAsync(context, catalog, args[1], cancellationToken).ConfigureAwait(false);
                break;

            case "uninstall":
                RequireArgumentCount(args, 2);
                Uninstall(context, catalog, args[1]);
                break;

            default:
                throw new ArgumentException($"Unsupported sub-action '{args[0]}' for 'voices'; expected packs, install, or uninstall.", nameof(context));
        }
    }

    /// <summary>Validates the sub-action's argument count (the sub-action token included).</summary>
    private static void RequireArgumentCount(IReadOnlyList<string> args, int expected)
    {
        if (args.Count < expected)
        {
            throw new ArgumentException($"voices {args[0]} requires a <packId> argument.", nameof(args));
        }

        if (args.Count > expected)
        {
            throw new ArgumentException($"Unsupported argument '{args[expected]}' for 'voices {args[0]}'.", nameof(args));
        }
    }

    /// <summary>Writes the installable packs with their state, size, and license.</summary>
    private static void WritePacks(Context context, ICliModelCatalog catalog)
    {
        var packs = catalog.VoicePacks;
        var idWidth = Math.Max("Id".Length, packs.Max(p => p.Id.Length));
        var states = packs.Select(p => catalog.GetVoicePackState(p.Id).ToString()).ToList();
        var stateWidth = Math.Max("State".Length, states.Max(s => s.Length));

        context.WriteLine($"{"Id".PadRight(idWidth)}  {"State".PadRight(stateWidth)}  Download  License");
        for (var i = 0; i < packs.Count; i++)
        {
            var megabytes = packs[i].SizeBytes / (1024.0 * 1024.0);
            context.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{packs[i].Id.PadRight(idWidth)}  {states[i].PadRight(stateWidth)}  {megabytes,6:F0} MB  {packs[i].LicenseName} - {packs[i].DisplayName}"));
        }

        context.WriteLine("");
        context.WriteLine($"Your own voices: put .wav/.flac files in '{catalog.UserVoicesDirectory}' or pass --voices-dir <path>.");
        context.WriteLine(ReferenceVoiceLibrary.ConsentNotice);
    }

    /// <summary>Installs one pack, reporting progress and the consent note.</summary>
    private static async Task InstallAsync(Context context, ICliModelCatalog catalog, string packId, CancellationToken cancellationToken)
    {
        // Resolve first so an unknown id gives the clean "not a known pack" error before any output
        _ = catalog.GetVoicePackState(packId);

        try
        {
            context.WriteLine($"Installing reference-voice pack '{packId}'...");
            var progress = CreateProgressReporter(context, packId);
            var result = await catalog.InstallVoicePackAsync(packId, progress, cancellationToken).ConfigureAwait(false);

            if (result.Outcome == SpeechModelDownloadOutcome.Installed)
            {
                context.WriteLine($"Voice pack '{packId}' installed. Run 'list-voices' to see the voices.");
                context.WriteLine(ReferenceVoiceLibrary.ConsentNotice);
            }
            else
            {
                context.WriteError($"Voice pack '{packId}' failed to install: {result.Outcome} ({result.Error?.Message}).");
            }
        }
        catch (OperationCanceledException)
        {
            context.WriteError($"Installation of voice pack '{packId}' was canceled.");
        }
    }

    /// <summary>Uninstalls one pack; a pack that is not installed is a safe no-op.</summary>
    private static void Uninstall(Context context, ICliModelCatalog catalog, string packId)
    {
        try
        {
            catalog.UninstallVoicePack(packId);
        }
        catch (SpeechModelStoreException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }

        context.WriteLine($"Voice pack '{packId}' uninstalled (or was already not installed).");
    }

    /// <summary>Creates a progress reporter that prints a throttled live progress line.</summary>
    private static Progress<SpeechModelDownloadProgress> CreateProgressReporter(Context context, string packId)
    {
        var lastReportedPercent = -1;
        var lastReportedKilobytes = -1L;
        return new Progress<SpeechModelDownloadProgress>(progress =>
        {
            if (progress.FractionComplete is { } fraction)
            {
                var percent = (int)(fraction * 100);
                if (percent == lastReportedPercent)
                {
                    return;
                }

                lastReportedPercent = percent;
                context.WriteLine($"  '{packId}': {percent}%");
            }
            else
            {
                var kilobytes = progress.BytesTransferred / 1024;
                if (kilobytes == lastReportedKilobytes)
                {
                    return;
                }

                lastReportedKilobytes = kilobytes;
                context.WriteLine($"  '{packId}': {kilobytes:N0} KB");
            }
        });
    }
}
