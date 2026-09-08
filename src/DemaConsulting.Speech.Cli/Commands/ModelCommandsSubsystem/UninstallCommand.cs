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
///     Implements the <c>uninstall</c> subcommand: removes one model's installed content,
///     manifest, and any leftover scratch directories.
/// </summary>
/// <remarks>
///     Per <see cref="SpeechModelStore.Uninstall"/>'s own documented behavior, uninstalling a
///     model that is not currently installed is a safe no-op (there is simply nothing to
///     remove), not an error; this command follows that same documented semantic rather than
///     inventing a new "not installed" error case.
/// </remarks>
internal static class UninstallCommand
{
    /// <summary>
    ///     Runs the <c>uninstall</c> subcommand against a real, composed
    ///     <see cref="SpeechModelCatalogAdapter"/>.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when no <c>&lt;modelId&gt;</c> argument (or more than one) is given.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the model's installed directory or manifest could not be removed, most
    ///     likely because another process still has an open file handle into it.
    /// </exception>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var catalog = CliModelCatalogFactory.Create(context);
        Run(context, catalog);
    }

    /// <summary>
    ///     Runs the <c>uninstall</c> subcommand against an injected catalog seam, for unit
    ///     testing without a real <see cref="SpeechModelCatalog"/>.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="catalog">The catalog seam to uninstall through. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="catalog"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when no <c>&lt;modelId&gt;</c> argument (or more than one) is given.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the model's installed directory or manifest could not be removed, most
    ///     likely because another process still has an open file handle into it.
    /// </exception>
    internal static void Run(Context context, ICliModelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);

        var args = context.CommandArgs;
        if (args.Count == 0)
        {
            throw new ArgumentException("uninstall requires a <modelId> argument.", nameof(context));
        }

        if (args.Count > 1)
        {
            throw new ArgumentException($"Unsupported argument '{args[1]}' for 'uninstall'.", nameof(context));
        }

        var modelId = args[0];
        try
        {
            catalog.Uninstall(modelId);
        }
        catch (SpeechModelStoreException ex)
        {
            // Re-thrown as InvalidOperationException so Program.Main's clean-error convention
            // (message + non-zero exit, no stack trace) applies to this genuinely expected
            // "file still in use" failure, rather than the generic-exception path.
            throw new InvalidOperationException(ex.Message, ex);
        }

        context.WriteLine($"Model '{modelId}' uninstalled (or was already not installed).");
    }
}
