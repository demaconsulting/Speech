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
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;

/// <summary>
///     Implements the <c>download</c> subcommand: downloads, verifies, and installs one or more
///     known models sequentially, reporting live progress, and optionally forcing a
///     re-download of an already-installed model via <c>--force</c>.
/// </summary>
internal static class DownloadCommand
{
    /// <summary>
    ///     Runs the <c>download</c> subcommand against a real, composed
    ///     <see cref="SpeechModelCatalogAdapter"/>, wiring <c>Ctrl+C</c> to cooperative
    ///     cancellation for the duration of the call.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when no <c>&lt;modelId&gt;</c> argument is given, or an unsupported flag is given.</exception>
    public static async Task RunAsync(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var catalog = CliModelCatalogFactory.Create(context);
        using var cancellationSource = new CancellationTokenSource();

        // Ask the in-progress download to cancel cooperatively rather than letting the runtime
        // kill the process outright, so a partially-staged download is cleaned up honestly by
        // the store's own staging/abandon logic instead of leaving orphaned scratch files.
        using var cancelRegistration = ConsoleCancellation.Register(cancellationSource);
        await RunAsync(context, catalog, cancellationSource.Token).ConfigureAwait(false);
    }

    /// <summary>
    ///     Runs the <c>download</c> subcommand against an injected catalog seam, for unit testing
    ///     without a real <see cref="SpeechModelCatalog"/> or network access.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="catalog">The catalog seam to download through. Must not be null.</param>
    /// <param name="cancellationToken">A token that, when canceled, aborts the in-progress download.</param>
    /// <returns>A task that completes once every requested model has been attempted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="catalog"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when no <c>&lt;modelId&gt;</c> argument is given, or an unsupported flag is given.</exception>
    internal static async Task RunAsync(Context context, ICliModelCatalog catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);

        var (modelIds, force) = ParseArguments(context.CommandArgs);

        foreach (var modelId in modelIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                context.WriteError($"Download of '{modelId}' was canceled before it started.");
                break;
            }

            // A genuine cancellation stops the whole batch; every other failure (unknown model
            // id, checksum mismatch, transport failure) is reported and the remaining requested
            // models are still attempted, mirroring the ModelDownloader CI tool's own per-model
            // failure isolation.
            var completed = await DownloadOneAsync(context, catalog, modelId, force, cancellationToken).ConfigureAwait(false);
            if (!completed)
            {
                break;
            }
        }
    }

    /// <summary>
    ///     Parses <c>download</c>'s own arguments: one or more positional <c>&lt;modelId&gt;</c>
    ///     values and the optional <c>--force</c> flag, in any order.
    /// </summary>
    /// <param name="args">The subcommand arguments following <c>download</c>.</param>
    /// <returns>The requested model ids in order, and whether <c>--force</c> was given.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when an unsupported flag is given or no model id is present.
    /// </exception>
    private static (List<string> ModelIds, bool Force) ParseArguments(IReadOnlyList<string> args)
    {
        var modelIds = new List<string>();
        var force = false;

        foreach (var arg in args)
        {
            if (arg == "--force")
            {
                force = true;
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unsupported argument '{arg}' for 'download'.", nameof(args));
            }
            else
            {
                modelIds.Add(arg);
            }
        }

        if (modelIds.Count == 0)
        {
            throw new ArgumentException("download requires at least one <modelId> argument.", nameof(args));
        }

        return (modelIds, force);
    }

    /// <summary>
    ///     Downloads one model, honoring <c>--force</c> by uninstalling an already-downloaded
    ///     model first (<see cref="SpeechModelCatalog.DownloadAsync"/> is otherwise a no-op for an
    ///     already-installed model).
    /// </summary>
    /// <param name="context">The invocation context that receives progress and error lines.</param>
    /// <param name="catalog">The catalog seam to download through.</param>
    /// <param name="modelId">The model id to download.</param>
    /// <param name="force">Whether to uninstall an already-downloaded model first.</param>
    /// <param name="cancellationToken">A token that, when canceled, aborts the download.</param>
    /// <returns>
    ///     <see langword="true"/> when the download attempt completed (successfully or not,
    ///     already reported via <see cref="Context.WriteLine"/>/<see cref="Context.WriteError"/>);
    ///     <see langword="false"/> when it was canceled, signaling the caller to stop the batch.
    /// </returns>
    private static async Task<bool> DownloadOneAsync(
        Context context,
        ICliModelCatalog catalog,
        string modelId,
        bool force,
        CancellationToken cancellationToken)
    {
        try
        {
            if (force)
            {
                var existing = catalog.Enumerate()
                    .FirstOrDefault(descriptor => string.Equals(descriptor.Id, modelId, StringComparison.Ordinal));
                if (existing?.State == SpeechModelState.Downloaded)
                {
                    context.WriteLine($"Uninstalling existing '{modelId}' before forced re-download...");
                    await catalog.UninstallAsync(modelId, cancellationToken).ConfigureAwait(false);
                }
            }

            context.WriteLine($"Downloading model '{modelId}'...");
            var progress = DownloadProgressReporter.Create(
                context,
                p => $"'{modelId}' file {p.FileIndex + 1}/{p.FileCount}");
            var result = await catalog.DownloadAsync(modelId, progress, cancellationToken).ConfigureAwait(false);

            if (result.Outcome == SpeechModelDownloadOutcome.Installed)
            {
                context.WriteLine($"Model '{modelId}' installed successfully.");
            }
            else
            {
                context.WriteError($"Model '{modelId}' failed to install: {result.Outcome} ({result.Error?.Message}).");
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            context.WriteError($"Download of '{modelId}' was canceled.");
            return false;
        }
        // Only the documented operator-facing errors are isolated per model (an unknown model
        // id surfaces as ArgumentException; a store state problem as InvalidOperationException).
        // Any other exception is unexpected and propagates instead of being masked.
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            context.WriteError($"Model '{modelId}' failed: {ex.Message}");
            return true;
        }
    }
}
