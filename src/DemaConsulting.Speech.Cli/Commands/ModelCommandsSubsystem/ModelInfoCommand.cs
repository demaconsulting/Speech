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
///     Implements the <c>model-info</c> subcommand: prints one known model's full identity,
///     install state, license, audio-tag support, and every declared tunable parameter.
/// </summary>
internal static class ModelInfoCommand
{
    /// <summary>
    ///     Runs the <c>model-info</c> subcommand against a real, composed
    ///     <see cref="SpeechModelCatalogAdapter"/>.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when no <c>&lt;modelId&gt;</c> argument (or more than one) is given, or when it
    ///     matches no known model.
    /// </exception>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var catalog = CliModelCatalogFactory.Create(context);
        Run(context, catalog);
    }

    /// <summary>
    ///     Runs the <c>model-info</c> subcommand against an injected catalog seam, for unit
    ///     testing without a real <see cref="SpeechModelCatalog"/>.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="catalog">The catalog seam to look up the requested model in. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="catalog"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when no <c>&lt;modelId&gt;</c> argument (or more than one) is given, or when it
    ///     matches no known model.
    /// </exception>
    internal static void Run(Context context, ICliModelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);

        var args = context.CommandArgs;
        if (args.Count == 0)
        {
            throw new ArgumentException("model-info requires a <modelId> argument.", nameof(context));
        }

        if (args.Count > 1)
        {
            throw new ArgumentException($"Unsupported argument '{args[1]}' for 'model-info'.", nameof(context));
        }

        var modelId = args[0];
        var descriptor = catalog.Enumerate()
            .FirstOrDefault(candidate => string.Equals(candidate.Id, modelId, StringComparison.Ordinal));

        if (descriptor is null)
        {
            throw new ArgumentException(
                $"Unknown model id '{modelId}'. Use 'list-models' to see available models.",
                nameof(context));
        }

        WriteModelInfo(context, descriptor);
    }

    /// <summary>
    ///     Writes a model descriptor's identity, state, license, audio-tag support, and every
    ///     declared parameter.
    /// </summary>
    private static void WriteModelInfo(Context context, SpeechModelDescriptor descriptor)
    {
        context.WriteLine($"Id: {descriptor.Id}");
        context.WriteLine($"Display Name: {descriptor.DisplayName}");
        context.WriteLine($"Role: {descriptor.Role}");
        context.WriteLine($"State: {descriptor.State}");
        context.WriteLine(descriptor.LicenseUrl is null
            ? $"License: {descriptor.LicenseName}"
            : $"License: {descriptor.LicenseName} ({descriptor.LicenseUrl})");
        context.WriteLine($"Audio Tag Support: {descriptor.Model.AudioTagSupport}");
        context.WriteLine("");

        var parameters = descriptor.Model.Parameters;
        if (parameters.Count == 0)
        {
            context.WriteLine("Parameters: (none)");
            return;
        }

        context.WriteLine("Parameters:");
        foreach (var parameter in parameters)
        {
            WriteParameter(context, parameter);
        }
    }

    /// <summary>
    ///     Writes one declared parameter's identity, description, and kind-specific
    ///     min/max/step/choices/default detail.
    /// </summary>
    private static void WriteParameter(Context context, ISpeechModelParameter parameter)
    {
        context.WriteLine($"  {parameter.Id} ({parameter.DisplayName})");
        if (!string.IsNullOrEmpty(parameter.Description))
        {
            context.WriteLine($"    {parameter.Description}");
        }

        switch (parameter)
        {
            case NumericParameter numeric:
                var kind = numeric.IsInteger ? "integer" : "numeric";
                var unit = numeric.Unit is null ? string.Empty : $" {numeric.Unit}";
                context.WriteLine(
                    $"    Type: {kind}, min={numeric.Minimum.ToString(CultureInfo.InvariantCulture)}, " +
                    $"max={numeric.Maximum.ToString(CultureInfo.InvariantCulture)}, " +
                    $"step={numeric.Step.ToString(CultureInfo.InvariantCulture)}, " +
                    $"default={numeric.Default.ToString(CultureInfo.InvariantCulture)}{unit}");
                break;

            case ChoiceParameter choice:
                var options = string.Join(", ", choice.Options.Select(option => $"{option.Value} ({option.Label})"));
                context.WriteLine($"    Type: choice, options=[{options}], default={choice.Default}");
                break;

            case BooleanParameter boolean:
                context.WriteLine($"    Type: boolean, default={boolean.Default}");
                break;

            default:
                context.WriteLine("    Type: (unrecognized parameter kind)");
                break;
        }
    }
}
