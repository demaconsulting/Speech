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
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Cli;

/// <summary>
///     Parses and validates the <c>speak</c> subcommand's repeatable <c>--param key=value</c>
///     flags against a resolved model's declared <see cref="ISpeechModelParameter"/> set, into
///     the untyped, boxed key-value bag <c>SpeechSynthesizerFactory.Create</c> expects.
/// </summary>
/// <remarks>
///     Deliberately stricter than <c>SpeechSynthesizerFactory.Create</c>'s own "unrecognized key
///     silently ignored, Info-logged" library-level contract: an unrecognized <c>--param</c> key
///     is a CLI operator typo, which should fail loudly at the command line rather than silently
///     mistune synthesis. This divergence is intentional; see this subsystem's design
///     documentation for the full rationale.
/// </remarks>
internal static class ParameterBagParser
{
    /// <summary>
    ///     Splits one raw <c>key=value</c> token into its key and value halves.
    /// </summary>
    /// <param name="token">The raw <c>--param</c> value token, e.g. <c>rate=1.2</c>.</param>
    /// <returns>The split key and value.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="token"/> contains no <c>=</c> separator, or when the key
    ///     half is empty.
    /// </exception>
    internal static (string Key, string Value) ParseToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var separatorIndex = token.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            throw new ArgumentException(
                $"--param value '{token}' must be in the form key=value.",
                nameof(token));
        }

        var key = token[..separatorIndex];
        var value = token[(separatorIndex + 1)..];
        return (key, value);
    }

    /// <summary>
    ///     Resolves a list of raw <c>(key, value)</c> tokens against a model's declared
    ///     parameters into a boxed <see cref="IReadOnlyDictionary{TKey,TValue}"/>.
    /// </summary>
    /// <param name="rawValues">The raw, unresolved tokens parsed by <see cref="ParseToken"/>.</param>
    /// <param name="declaredParameters">The resolved model's own declared parameter set.</param>
    /// <returns>
    ///     A boxed parameter-value bag: boxed <see cref="double"/> for a <see cref="NumericParameter"/>,
    ///     boxed <see cref="string"/> for a <see cref="ChoiceParameter"/>, boxed <see cref="bool"/>
    ///     for a <see cref="BooleanParameter"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="rawValues"/> or <paramref name="declaredParameters"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when a key matches no declared parameter, or when a value is malformed,
    ///     out of range, non-integral for an integer-only <see cref="NumericParameter"/>, or
    ///     does not match any declared <see cref="ChoiceParameter"/> option.
    /// </exception>
    internal static IReadOnlyDictionary<string, object> Resolve(
        IReadOnlyList<(string Key, string Value)> rawValues,
        IReadOnlyList<ISpeechModelParameter> declaredParameters)
    {
        ArgumentNullException.ThrowIfNull(rawValues);
        ArgumentNullException.ThrowIfNull(declaredParameters);

        var result = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var (key, value) in rawValues)
        {
            var parameter = declaredParameters.FirstOrDefault(
                candidate => string.Equals(candidate.Id, key, StringComparison.Ordinal));

            if (parameter is null)
            {
                throw new ArgumentException(
                    $"--param key '{key}' is not a parameter declared by this model.",
                    nameof(rawValues));
            }

            result[key] = ResolveValue(parameter, value);
        }

        return result;
    }

    /// <summary>
    ///     Resolves one raw string value against a single declared parameter's kind.
    /// </summary>
    private static object ResolveValue(ISpeechModelParameter parameter, string value) =>
        parameter switch
        {
            NumericParameter numeric => ResolveNumeric(numeric, value),
            ChoiceParameter choice => ResolveChoice(choice, value),
            BooleanParameter boolean => ResolveBoolean(boolean, value),
            _ => throw new ArgumentException(
                $"Parameter '{parameter.Id}' has an unrecognized kind and cannot be set via --param.",
                nameof(parameter))
        };

    /// <summary>
    ///     Resolves a raw value against a <see cref="NumericParameter"/>'s range and
    ///     integer-only constraint.
    /// </summary>
    private static object ResolveNumeric(NumericParameter numeric, string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException(
                $"--param {numeric.Id}='{value}' is not a valid number.",
                nameof(value));
        }

        if (parsed < numeric.Minimum || parsed > numeric.Maximum)
        {
            throw new ArgumentException(
                $"--param {numeric.Id}='{value}' must be within [{numeric.Minimum}, {numeric.Maximum}].",
                nameof(value));
        }

        if (numeric.IsInteger && !double.IsInteger(parsed))
        {
            throw new ArgumentException(
                $"--param {numeric.Id}='{value}' must be a whole number.",
                nameof(value));
        }

        return parsed;
    }

    /// <summary>
    ///     Resolves a raw value against a <see cref="ChoiceParameter"/>'s declared options.
    /// </summary>
    private static object ResolveChoice(ChoiceParameter choice, string value)
    {
        if (!choice.Options.Any(option => string.Equals(option.Value, value, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"--param {choice.Id}='{value}' does not match any declared option.",
                nameof(value));
        }

        return value;
    }

    /// <summary>
    ///     Resolves a raw value against a <see cref="BooleanParameter"/>.
    /// </summary>
    private static object ResolveBoolean(BooleanParameter boolean, string value)
    {
        if (!bool.TryParse(value, out var parsed))
        {
            throw new ArgumentException(
                $"--param {boolean.Id}='{value}' must be 'true' or 'false'.",
                nameof(value));
        }

        return parsed;
    }
}
