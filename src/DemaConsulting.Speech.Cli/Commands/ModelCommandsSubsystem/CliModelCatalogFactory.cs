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
///     Composes a real <see cref="SpeechModelCatalogAdapter"/> honoring the CLI's
///     <c>--models-dir</c> global option, for use by each model-management command's public
///     <c>Run(Context)</c> entry point.
/// </summary>
internal static class CliModelCatalogFactory
{
    /// <summary>
    ///     Creates a new <see cref="SpeechModelCatalogAdapter"/> for the given invocation context.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <returns>
    ///     A new adapter composed against <see cref="Context.ModelsDir"/> when supplied, or the
    ///     library's default per-user model store root otherwise. Caller owns disposal.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static SpeechModelCatalogAdapter Create(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = string.IsNullOrEmpty(context.ModelsDir)
            ? null
            : new SpeechModelStoreOptions { RootPathOverride = context.ModelsDir };

        return new SpeechModelCatalogAdapter(options);
    }
}
