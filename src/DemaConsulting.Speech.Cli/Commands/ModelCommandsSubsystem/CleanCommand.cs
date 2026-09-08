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
///     Implements the <c>clean</c> subcommand: best-effort removes any leftover partial-install
///     or staging directories left behind by a prior interrupted download/install for one model.
/// </summary>
/// <remarks>
///     Per <see cref="SpeechModelStore.CleanUpLeftovers"/>'s own documented behavior, this never
///     throws for a deletion that still fails (for example an open file handle); the leftover
///     directory is simply left in place for a future call to retry. It is always safe to call,
///     including for a model with nothing left to clean up.
/// </remarks>
internal static class CleanCommand
{
    /// <summary>
    ///     Runs the <c>clean</c> subcommand against a real, composed
    ///     <see cref="SpeechModelCatalogAdapter"/>.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when no <c>&lt;modelId&gt;</c> argument (or more than one) is given.</exception>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var catalog = CliModelCatalogFactory.Create(context);
        Run(context, catalog);
    }

    /// <summary>
    ///     Runs the <c>clean</c> subcommand against an injected catalog seam, for unit testing
    ///     without a real <see cref="SpeechModelCatalog"/>.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="catalog">The catalog seam to clean up through. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="catalog"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when no <c>&lt;modelId&gt;</c> argument (or more than one) is given.</exception>
    internal static void Run(Context context, ICliModelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);

        var args = context.CommandArgs;
        if (args.Count == 0)
        {
            throw new ArgumentException("clean requires a <modelId> argument.", nameof(context));
        }

        if (args.Count > 1)
        {
            throw new ArgumentException($"Unsupported argument '{args[1]}' for 'clean'.", nameof(context));
        }

        var modelId = args[0];
        catalog.CleanUpLeftovers(modelId);
        context.WriteLine($"Cleaned up leftover files for model '{modelId}' (if any were present).");
    }
}
