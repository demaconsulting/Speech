using System.Globalization;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.SttEvaluation;

/// <summary>
///     The <c>--engine-option Name=Value</c> settings of an evaluation run, and the backend they
///     produce.
/// </summary>
/// <remarks>
///     Lets a developer measure a model with a non-default setting without editing code. Two kinds
///     of names are accepted:
///     <list type="bullet">
///         <item>
///             <description>
///                 A declared public parameter of the model (for example <c>language</c>); the
///                 value is converted to the parameter's type (text for a choice, a number for a
///                 numeric parameter, <c>true</c>/<c>false</c> for a boolean) and passed through
///                 <see cref="IRecognitionModel.CreateBackend(string, IReadOnlyDictionary{string, object})"/>.
///             </description>
///         </item>
///         <item>
///             <description>
///                 An internal engine constant of the ONNX Nemotron model, which is not public API
///                 and is reached through its internal options overload: <see cref="BlankPenalty"/>
///                 (a non-negative number), <see cref="FlushTailChunks"/> (a non-negative integer),
///                 and the endpoint rules <see cref="TrailingSilenceMs"/>,
///                 <see cref="TrailingSilenceAfterSpeechMs"/> and <see cref="MaxUtteranceMs"/>
///                 (non-negative integer milliseconds, zero disables a rule). This is how the
///                 blank-penalty default is chosen by measurement.
///             </description>
///         </item>
///     </list>
///     An unknown name, or an engine constant given for another model, is an error rather than
///     being ignored, so a typo can never produce a silently default measurement. Immutable and
///     thread-safe.
/// </remarks>
internal sealed class EngineOptionOverrides
{
    /// <summary>The engine option name of <see cref="NemotronEngineOptions.BlankPenalty"/>.</summary>
    public const string BlankPenalty = "BlankPenalty";

    /// <summary>The engine option name of <see cref="NemotronEngineOptions.FlushTailChunks"/>.</summary>
    public const string FlushTailChunks = "FlushTailChunks";

    /// <summary>The engine option name of <see cref="NemotronEndpointRules.TrailingSilenceMs"/>.</summary>
    public const string TrailingSilenceMs = "TrailingSilenceMs";

    /// <summary>The engine option name of <see cref="NemotronEndpointRules.TrailingSilenceAfterSpeechMs"/>.</summary>
    public const string TrailingSilenceAfterSpeechMs = "TrailingSilenceAfterSpeechMs";

    /// <summary>The engine option name of <see cref="NemotronEndpointRules.MaxUtteranceMs"/>.</summary>
    public const string MaxUtteranceMs = "MaxUtteranceMs";

    /// <summary>The ONNX Nemotron engine option names, in documentation order.</summary>
    private static readonly string[] NemotronEngineOptionNames =
    [
        BlankPenalty, FlushTailChunks, TrailingSilenceMs, TrailingSilenceAfterSpeechMs, MaxUtteranceMs,
    ];

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
    ///     Creates the loaded backend of one model with these settings applied.
    /// </summary>
    /// <param name="model">The model. Must not be <see langword="null"/>.</param>
    /// <param name="installedModelDirectory">The model's installed <c>current</c> folder.</param>
    /// <returns>The loaded backend; the caller disposes it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">
    ///     Thrown when a name is neither a declared parameter nor (for the ONNX Nemotron model) an
    ///     engine option, or a value cannot be converted.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an engine option value is out of range (for example negative).</exception>
    /// <remarks>Exceptions from loading the model propagate unchanged.</remarks>
    public IRecognitionBackend CreateBackend(IRecognitionModel model, string installedModelDirectory)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Split the settings into engine constants and public parameters
        var engineValues = Values.Where(v => NemotronEngineOptionNames.Contains(v.Key)).ToList();
        var parameterValues = ToParameterValues(model, Values.Where(v => !NemotronEngineOptionNames.Contains(v.Key)));
        if (engineValues.Count == 0)
        {
            return model.CreateBackend(installedModelDirectory, parameterValues);
        }

        // Engine constants exist only for the ONNX Nemotron model
        if (model is not OnnxNemotronMultilingualRecognitionModel nemotron)
        {
            throw new FormatException(
                $"Engine options {string.Join(", ", engineValues.Select(v => v.Key))} apply only to " +
                $"'{OnnxNemotronMultilingualRecognitionModel.ModelId}', not to '{model.Id}'.");
        }

        return nemotron.CreateBackend(installedModelDirectory, parameterValues, ToEngineOptions(engineValues));
    }

    /// <summary>Converts the engine constant settings into ONNX Nemotron engine options (unset values keep their defaults).</summary>
    /// <param name="values">The engine constant settings.</param>
    /// <returns>The options.</returns>
    /// <exception cref="FormatException">Thrown when a value is not a number of the right kind.</exception>
    private static NemotronEngineOptions ToEngineOptions(IReadOnlyList<KeyValuePair<string, string>> values)
    {
        var options = new NemotronEngineOptions();
        var rules = new NemotronEndpointRules();
        foreach (var (name, text) in values)
        {
            switch (name)
            {
                case BlankPenalty:
                    options = options with { BlankPenalty = ParseFloat(name, text) };
                    break;
                case FlushTailChunks:
                    options = options with { FlushTailChunks = ParseInteger(name, text) };
                    break;
                case TrailingSilenceMs:
                    rules = rules with { TrailingSilenceMs = ParseInteger(name, text) };
                    break;
                case TrailingSilenceAfterSpeechMs:
                    rules = rules with { TrailingSilenceAfterSpeechMs = ParseInteger(name, text) };
                    break;
                default:
                    rules = rules with { MaxUtteranceMs = ParseInteger(name, text) };
                    break;
            }
        }

        return options with { Endpoint = rules };
    }

    /// <summary>Converts the public parameter settings into the typed values of one model.</summary>
    /// <param name="model">The model the values are for.</param>
    /// <param name="values">The parameter settings.</param>
    /// <returns>The typed values keyed by parameter id, or <see langword="null"/> when there are none.</returns>
    /// <exception cref="FormatException">
    ///     Thrown when a name is not a parameter the model declares, or a value cannot be converted
    ///     to the parameter's type. The message lists the declared parameters and engine options.
    /// </exception>
    private static Dictionary<string, object>? ToParameterValues(
        IRecognitionModel model,
        IEnumerable<KeyValuePair<string, string>> values)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (name, text) in values)
        {
            // Only declared parameters are accepted, so a typo fails instead of being ignored
            var parameter = model.Parameters.FirstOrDefault(p => p.Id == name)
                ?? throw new FormatException(
                    $"Model '{model.Id}' has no parameter '{name}'. Declared parameters: " +
                    $"{string.Join(", ", model.Parameters.Select(p => p.Id))}; ONNX Nemotron engine options: " +
                    $"{string.Join(", ", NemotronEngineOptionNames)}.");
            result[name] = Convert(parameter, text);
        }

        return result.Count == 0 ? null : result;
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

    /// <summary>Parses an engine option given as a number.</summary>
    /// <param name="name">The option name (for the message).</param>
    /// <param name="text">The value text.</param>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">Thrown when the text is not a number.</exception>
    private static float ParseFloat(string name, string text) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"'{text}' is not a valid value of engine option '{name}' (a number).");

    /// <summary>Parses an engine option given as an integer.</summary>
    /// <param name="name">The option name (for the message).</param>
    /// <param name="text">The value text.</param>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">Thrown when the text is not an integer.</exception>
    private static int ParseInteger(string name, string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"'{text}' is not a valid value of engine option '{name}' (an integer).");
}
