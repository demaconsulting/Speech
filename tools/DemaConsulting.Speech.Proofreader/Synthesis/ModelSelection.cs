using System.Globalization;
using System.Text;

namespace DemaConsulting.Speech.Proofreader.Synthesis;

/// <summary>
///     A synthesis model plus the parameter values it is loaded with: the identity of one audible
///     configuration.
/// </summary>
/// <remarks>
///     The cache, the engine host, flags, and sessions all need the same notion of "this model with
///     these settings", so it lives in one small immutable type. <see cref="Key"/> is canonical
///     (parameters sorted by id, invariant number formatting) so equal selections always produce
///     equal keys.
/// </remarks>
internal sealed class ModelSelection
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ModelSelection"/> class.
    /// </summary>
    /// <param name="modelId">The catalog id of the model. Must not be empty.</param>
    /// <param name="displayName">The human-readable model name. Must not be <see langword="null"/>.</param>
    /// <param name="parameters">The parameter values (string, double, or bool). Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="displayName"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    public ModelSelection(string modelId, string displayName, IReadOnlyDictionary<string, object> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(parameters);

        ModelId = modelId;
        DisplayName = displayName;
        Parameters = new Dictionary<string, object>(parameters, StringComparer.Ordinal);
        Key = BuildKey(modelId, Parameters);
    }

    /// <summary>Gets the catalog id of the model.</summary>
    public string ModelId { get; }

    /// <summary>Gets the human-readable model name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the parameter values the model is loaded with.</summary>
    public IReadOnlyDictionary<string, object> Parameters { get; }

    /// <summary>Gets the canonical key: equal for equal model ids and parameter values.</summary>
    public string Key { get; }

    /// <summary>
    ///     Gets a compact one-line description of the parameter values, for the TSV export and the UI.
    /// </summary>
    /// <returns>Sorted <c>id=value</c> pairs joined by <c>; </c>, or an empty string without parameters.</returns>
    public string DescribeSettings() => string.Join("; ", SortedPairs(Parameters));

    /// <summary>
    ///     Formats one parameter value with invariant culture.
    /// </summary>
    /// <param name="value">A string, double, or bool value.</param>
    /// <returns>The invariant text form of the value.</returns>
    public static string FormatValue(object value) => value switch
    {
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    /// <summary>
    ///     Builds the canonical key.
    /// </summary>
    /// <param name="modelId">The model id.</param>
    /// <param name="parameters">The parameter values.</param>
    /// <returns>The key string.</returns>
    private static string BuildKey(string modelId, IReadOnlyDictionary<string, object> parameters)
    {
        var builder = new StringBuilder(modelId);
        foreach (var pair in SortedPairs(parameters))
        {
            builder.Append('|').Append(pair);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Gets the parameters as <c>id=value</c> text sorted by id.
    /// </summary>
    /// <param name="parameters">The parameter values.</param>
    /// <returns>The sorted pairs.</returns>
    private static IEnumerable<string> SortedPairs(IReadOnlyDictionary<string, object> parameters) =>
        parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={FormatValue(pair.Value)}");
}
