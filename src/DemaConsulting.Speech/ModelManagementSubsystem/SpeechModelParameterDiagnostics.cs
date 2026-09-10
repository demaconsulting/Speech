using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Validates a caller-supplied runtime parameter value bag against one model's declared
///     <see cref="ISpeechModelParameter"/> descriptors at composition time, splitting invalid
///     input into two deliberately different outcomes: an unrecognized parameter id is reported
///     and silently ignored (preserving cross-model settings-dictionary reuse), while an invalid
///     value for a parameter the model does declare throws immediately.
/// </summary>
/// <remarks>
///     Per this library's "typed, self-describing descriptor set" grouping, this type is
///     co-located with <see cref="ISpeechModelParameter"/>/<see cref="NumericParameter"/>/
///     <see cref="ChoiceParameter"/>/<see cref="BooleanParameter"/>. It is called exactly once,
///     up front, from <c>SpeechRecognizerFactory.Create</c> and
///     <c>SpeechSynthesizerFactory.Create</c>, before either factory constructs a recognizer or
///     synthesizer - so an invalid recognized value is surfaced synchronously from <c>Create</c>
///     rather than later, silently, from a per-call runtime hook.
///     <para>
///     <b>Deliberate split, and why</b>: a supplied key with no matching declared parameter
///     <see cref="ISpeechModelParameter.Id"/> is a legitimate, expected situation - the same host
///     settings dictionary is commonly reused across multiple models with different declared
///     parameter sets - so it is reported at
///     <see cref="SpeechDiagnosticLevel.Info"/> and otherwise ignored, never thrown. A supplied
///     key that <em>does</em> match a declared parameter, but whose value is invalid for it
///     (wrong CLR type, out of range, a non-integral value for an integer-only parameter, or an
///     unrecognized choice/boolean value), means the host explicitly targeted this parameter on
///     this model - an invalid value here is a caller bug, not a compatibility gap, so it throws
///     <see cref="ArgumentException"/> immediately. This is a deliberate, user-approved breaking
///     change from this library's earlier "never throw, silently default" behavior for this
///     specific case; the per-call runtime hooks (<c>ISynthesisModel.ResolveSpeakerId</c>,
///     <c>SherpaOnnxSpeechSynthesizer.ResolveOverrideRatios</c>) keep their own, unrelated,
///     unconditional never-throw contract unchanged.
///     </para>
/// </remarks>
internal static class SpeechModelParameterDiagnostics
{
    /// <summary>
    ///     Validates every supplied value against the model's declared parameters, reporting an
    ///     <see cref="SpeechDiagnosticLevel.Info"/> diagnostic for each unrecognized key and
    ///     throwing <see cref="ArgumentException"/> for the first invalid value found for a
    ///     recognized parameter.
    /// </summary>
    /// <param name="modelId">The model's identifier, named in every thrown/reported message.</param>
    /// <param name="declaredParameters">
    ///     The model's own declared parameters (<see cref="ISpeechModel.Parameters"/>), consulted
    ///     in the given order so the first invalid recognized value throws deterministically.
    ///     Must not be null.
    /// </param>
    /// <param name="parameterValues">
    ///     The caller's untyped parameter value bag, or <see langword="null"/>/empty to skip
    ///     validation entirely (there is nothing to validate). Named to match the caller-facing
    ///     <c>parameterValues</c> parameter on every <c>SpeechRecognizerFactory.Create</c>/
    ///     <c>SpeechSynthesizerFactory.Create</c> overload, so a thrown
    ///     <see cref="ArgumentException"/>'s <see cref="ArgumentException.ParamName"/> names the
    ///     argument the caller actually supplied.
    /// </param>
    /// <param name="diagnostics">The sink to report an unrecognized parameter id to. Must not be null.</param>
    /// <param name="category">The diagnostics category to report under. Must not be null.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="modelId"/>, <paramref name="declaredParameters"/>,
    ///     <paramref name="diagnostics"/>, or <paramref name="category"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameterValues"/> contains a value for a declared
    ///     parameter that is invalid for it - wrong CLR type, out of
    ///     <see cref="NumericParameter.Minimum"/>/<see cref="NumericParameter.Maximum"/> range, a
    ///     non-integral value for a <see cref="NumericParameter"/> with
    ///     <see cref="NumericParameter.IsInteger"/> <see langword="true"/>, a
    ///     <see cref="ChoiceParameter"/> value matching no declared
    ///     <see cref="ChoiceParameterOption.Value"/>, or a non-<see cref="bool"/> value for a
    ///     <see cref="BooleanParameter"/>.
    /// </exception>
    public static void ValidateAndReport(
        string modelId,
        IReadOnlyList<ISpeechModelParameter> declaredParameters,
        IReadOnlyDictionary<string, object>? parameterValues,
        ISpeechDiagnostics diagnostics,
        string category)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(declaredParameters);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(category);

        if (parameterValues is null || parameterValues.Count == 0)
        {
            return;
        }

        // Validate every recognized parameter first, iterating in declared order so the first
        // invalid recognized value throws deterministically regardless of dictionary enumeration
        // order.
        foreach (var parameter in declaredParameters.Where(parameter =>
                     parameterValues.ContainsKey(parameter.Id)))
        {
            ValidateValue(modelId, parameter, parameterValues[parameter.Id], nameof(parameterValues));
        }

        // Every recognized value validated successfully - now report (but never throw for) any
        // supplied key this model does not declare, preserving the existing, deliberate
        // cross-model-compatibility contract while making it observable.
        var declaredIds = declaredParameters
            .Select(parameter => parameter.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var key in parameterValues.Keys.Where(key => !declaredIds.Contains(key)))
        {
            diagnostics.Report(
                SpeechDiagnosticLevel.Info,
                category,
                $"Parameter '{key}' is not declared by this model and was ignored.");
        }
    }

    /// <summary>Dispatches to the type-specific validator for one recognized parameter/value pair.</summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="parameter"/> is a descriptor type other than
    ///     <see cref="NumericParameter"/>/<see cref="ChoiceParameter"/>/<see cref="BooleanParameter"/>.
    ///     <see cref="ISpeechModelParameter"/> is public, so a custom model could otherwise supply
    ///     an unrecognized descriptor subtype whose values would silently bypass validation.
    /// </exception>
    private static void ValidateValue(string modelId, ISpeechModelParameter parameter, object? value, string paramName)
    {
        switch (parameter)
        {
            case NumericParameter numeric:
                ValidateNumeric(modelId, numeric, value, paramName);
                break;

            case ChoiceParameter choice:
                ValidateChoice(modelId, choice, value, paramName);
                break;

            case BooleanParameter boolean:
                ValidateBoolean(modelId, boolean, value, paramName);
                break;

            default:
                throw new ArgumentException(
                    $"Parameter '{parameter.Id}' declares an unsupported descriptor type " +
                    $"'{parameter.GetType()}' for model '{modelId}' and cannot be validated.",
                    paramName);
        }
    }

    /// <summary>
    ///     Describes a supplied value's runtime type for an error message, without throwing when
    ///     the value is <see langword="null"/> (a <see cref="IReadOnlyDictionary{TKey,TValue}"/>
    ///     of <see cref="object"/> is not itself immune to a caller storing a null entry at
    ///     runtime, even though the declared value type is non-nullable).
    /// </summary>
    private static string DescribeType(object? value) => value is null ? "null" : value.GetType().ToString();

    /// <summary>
    ///     Validates a value supplied for a <see cref="NumericParameter"/>: it must be a
    ///     <see cref="double"/>/<see cref="int"/>/<see cref="float"/> (the same set
    ///     <c>ResolveSpeakerId</c> already accepts), finite, within
    ///     <see cref="NumericParameter.Minimum"/>/<see cref="NumericParameter.Maximum"/>, and,
    ///     when <see cref="NumericParameter.IsInteger"/> is <see langword="true"/>, a whole
    ///     number - a fractional value is rejected outright rather than silently rounded.
    /// </summary>
    private static void ValidateNumeric(string modelId, NumericParameter parameter, object? value, string paramName)
    {
        double? numericValue = value switch
        {
            double doubleValue => doubleValue,
            int intValue => intValue,
            float floatValue => floatValue,
            _ => null,
        };

        if (numericValue is not double resolved || !double.IsFinite(resolved))
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Id}' expected a numeric value but received {DescribeType(value)} for model '{modelId}'.",
                paramName);
        }

        if (resolved < parameter.Minimum || resolved > parameter.Maximum)
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Id}' value {resolved} is outside the valid range " +
                $"[{parameter.Minimum}, {parameter.Maximum}] for model '{modelId}'.",
                paramName);
        }

        if (parameter.IsInteger && !double.IsInteger(resolved))
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Id}' value {resolved} must be a whole number for model '{modelId}'.",
                paramName);
        }
    }

    /// <summary>
    ///     Validates a value supplied for a <see cref="ChoiceParameter"/>: it must be a
    ///     <see cref="string"/> matching one declared <see cref="ChoiceParameterOption.Value"/>
    ///     exactly (ordinal comparison).
    /// </summary>
    private static void ValidateChoice(string modelId, ChoiceParameter parameter, object? value, string paramName)
    {
        if (value is not string stringValue)
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Id}' expected a string value but received {DescribeType(value)} for model '{modelId}'.",
                paramName);
        }

        if (!parameter.Options.Any(option => string.Equals(option.Value, stringValue, StringComparison.Ordinal)))
        {
            var expectedValues = string.Join(", ", parameter.Options.Select(option => option.Value));
            throw new ArgumentException(
                $"Parameter '{parameter.Id}' value '{stringValue}' is not a valid option for model " +
                $"'{modelId}'; expected one of: {expectedValues}.",
                paramName);
        }
    }

    /// <summary>Validates a value supplied for a <see cref="BooleanParameter"/>: it must be a <see cref="bool"/>.</summary>
    private static void ValidateBoolean(string modelId, BooleanParameter parameter, object? value, string paramName)
    {
        if (value is not bool)
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Id}' expected a boolean value but received {DescribeType(value)} for model '{modelId}'.",
                paramName);
        }
    }
}
