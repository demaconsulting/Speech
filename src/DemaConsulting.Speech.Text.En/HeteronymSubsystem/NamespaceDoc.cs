namespace DemaConsulting.Speech.Text.En.HeteronymSubsystem;

/// <summary>
///     The heteronym subsystem of the English text analysis: the reading inventory (which readings a
///     word such as <c>record</c> can have), the deterministic feature extraction that turns a
///     tagged sentence into hashed features, the sparse averaged-perceptron model format and its strict
///     loader, and the classifier that picks one of a word's candidate readings with a calibrated
///     confidence.
/// </summary>
/// <remarks>
///     Only <see cref="ReadingInventory"/> is public (it is how a host checks a reading id). Everything
///     else is internal so the feature set can change with a feature-set version instead of a
///     breaking release. The subsystem knows nothing of the analyzer, the part-of-speech package
///     or the diagnostics sink: the analysis subsystem depends on it, never the reverse.
/// </remarks>
internal static class NamespaceDoc
{
}
