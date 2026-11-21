using System.Globalization;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SttEvaluation;

/// <summary>
///     The <c>--engine-option Name=Value</c> settings of an evaluation run, and their conversion
///     into the parameter values a recognition model accepts.
/// </summary>
/// <remarks>
///     Lets a developer measure a model with a non-default setting (for example another language
///     selection) without editing code. Today the settings are applied through the public
///     parameter bag of <see cref="IRecognitionModel.CreateBackend(string, IReadOnlyDictionary{string, object})"/>,
///     so a name must be one of the model's declared <see cref="ISpeechModel.Parameters"/>; the
///     value is converted to the parameter's type (text for a choice, a number for a numeric
///     parameter, <c>true</c>/<c>false</c> for a boolean). An unknown name is an error rather than
///     being ignored, so a typo can never produce a silently default measurement. Immutable and
///     thread-safe.
/// </remarks>
internal sealed class EngineOptionOverrides
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="EngineOptionOverrides"/> class.
    /// </summary>
    /// <param name="values">The parsed settings, by name.</param>
    private EngineOptionOverrides(IReadOnlyDictionary<string, string> values) => Values = values;

    /// <summary>Gets the empty set of overrides (every model runs with its defaults).</summary>
    public static EngineOptionOverrides None { get; } = new(new Dictionary<string, string>());

    /// <summary>Gets the raw settings, by name (ordinal, case-sensitive).</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    /// <summary>
    ///     Parses <c>Name=Value</c> arguments.
    /// </summary>
    /// <param name="arguments">The arguments given after each <c>--engine-option</c>. Must not be <see langword="null"/>.</param>
    /// <returns>The parsed overrides.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">
    ///     Thrown when an argument has no <c>=</c>, an empty name, or repeats a name already given.
    /// </exception>
    public static EngineOptionOverrides Parse(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var argument in arguments)
        {
            var separator = argument.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                throw new FormatException($"Engine option '{argument}' must have the form Name=Value.");
            }

            var name = argument[..separator].Trim();
            if (!values.TryAdd(name, argument[(separator + 1)..].Trim()))
            {
                throw new FormatException($"Engine option '{name}' is given more than once.");
            }
        }

        return new EngineOptionOverrides(values);
    }

    /// <summary>
    ///     Converts the settings into the parameter values of one model.
    /// </summary>
    /// <param name="model">The model the values are for. Must not be <see langword="null"/>.</param>
    /// <returns>
    ///     The typed values keyed by parameter id, or <see langword="null"/> when there are no
    ///     settings (the model then runs exactly as a host that passes no parameters).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">
    ///     Thrown when a name is not a parameter the model declares, or a value cannot be converted
    ///     to the parameter's type. The message lists the declared parameters.
    /// </exception>
    public IReadOnlyDictionary<string, object>? ToParameterValues(IRecognitionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (Values.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (name, text) in Values)
        {
            // Only declared parameters are accepted, so a typo fails instead of being ignored
            var parameter = model.Parameters.FirstOrDefault(p => p.Id == name)
                ?? throw new FormatException(
                    $"Model '{model.Id}' has no parameter '{name}'. Declared parameters: " +
                    $"{string.Join(", ", model.Parameters.Select(p => p.Id))}.");
            result[name] = Convert(parameter, text);
        }

        return result;
    }

    /// <summary>Converts one setting's text to the value type of its parameter.</summary>
    /// <param name="parameter">The declared parameter.</param>
    /// <param name="text">The setting's text.</param>
    /// <returns>A <see cref="double"/>, <see cref="bool"/> or <see cref="string"/> value.</returns>
    /// <exception cref="FormatException">Thrown when the text is not a valid value of the parameter's type.</exception>
    private static object Convert(ISpeechModelParameter parameter, string text) => parameter switch
    {
        NumericParameter when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
        BooleanParameter when bool.TryParse(text, out var flag) => flag,
        NumericParameter or BooleanParameter => throw new FormatException(
            $"'{text}' is not a valid value of parameter '{parameter.Id}'."),
        _ => text,
    };
}
