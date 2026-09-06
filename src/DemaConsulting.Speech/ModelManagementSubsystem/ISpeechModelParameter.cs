namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Common contract for a single tunable synthesis/recognition parameter a model declares,
///     letting a host build a generic settings UI without knowing the model in advance.
/// </summary>
/// <remarks>
///     Per this library's "per-model tunable parameters use a typed, self-describing
///     descriptor set" decision, a host renders an appropriate control per concrete
///     implementation (<see cref="NumericParameter"/> → slider, <see cref="ChoiceParameter"/> →
///     dropdown, <see cref="BooleanParameter"/> → checkbox) using only this common shape plus a
///     type check/pattern match - it never needs to know a specific model's parameter set ahead
///     of time. Values are supplied to synthesis/recognition as an untyped key-value bag keyed
///     by <see cref="Id"/>; a model's own backing class validates, clamps, and silently ignores
///     unrecognized keys, so a host built against one model does not break when switching to a
///     model with a different declared parameter set.
/// </remarks>
public interface ISpeechModelParameter
{
    /// <summary>
    ///     Gets the stable key used to reference this parameter in the untyped key-value bag
    ///     supplied to synthesis/recognition. Never null or empty.
    /// </summary>
    string Id { get; }

    /// <summary>
    ///     Gets the short, human-readable label a host UI shows for this parameter (for example
    ///     "Speaking rate"). Never null.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    ///     Gets a longer, human-readable explanation of what this parameter controls, suitable
    ///     for a tooltip or help text. Never null; may be empty when no further detail is useful.
    /// </summary>
    string Description { get; }
}
