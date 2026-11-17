namespace DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

/// <summary>
///     One violation found by a check: which check found it, where, and what is wrong.
/// </summary>
/// <param name="Check">The check that produced the finding, for example <c>schema</c> or <c>license</c>.</param>
/// <param name="Location">The file (and optionally the 1-based line) the finding refers to, for example <c>train.tsv:12</c>.</param>
/// <param name="Message">A human-readable description of the violation.</param>
internal sealed record Finding(string Check, string Location, string Message)
{
    /// <summary>
    ///     Renders the finding in the <c>check-data: check: location: message</c> output format.
    /// </summary>
    /// <returns>The one-line text of the finding.</returns>
    public override string ToString() => $"check-data: {Check}: {Location}: {Message}";
}

/// <summary>
///     Collects the findings and informational lines of a <c>check-data</c> run and renders them in a
///     deterministic order.
/// </summary>
/// <remarks>
///     Every check adds its violations here instead of throwing, so that one run reports all problems at once.
///     Findings keep their insertion order so the output is stable from run to run; the console output caps the
///     number of findings printed per check, so a systematic defect (for example a wrong column in every row)
///     does not drown the rest of the output, while <see cref="FindingCount"/> and the exit code always count
///     every finding. Not thread-safe; one instance belongs to one run.
/// </remarks>
internal sealed class CheckReport
{
    /// <summary>The default number of findings printed per check.</summary>
    public const int DefaultMaxFindingsPerCheck = 25;

    /// <summary>The findings in insertion order.</summary>
    private readonly List<Finding> _findings = [];

    /// <summary>The informational lines in insertion order.</summary>
    private readonly List<string> _info = [];

    /// <summary>The number of findings printed per check.</summary>
    private readonly int _maxFindingsPerCheck;

    /// <summary>
    ///     Creates an empty report.
    /// </summary>
    /// <param name="maxFindingsPerCheck">The number of findings printed per check; must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxFindingsPerCheck"/> is less than 1.</exception>
    public CheckReport(int maxFindingsPerCheck = DefaultMaxFindingsPerCheck)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFindingsPerCheck, 1);
        _maxFindingsPerCheck = maxFindingsPerCheck;
    }

    /// <summary>
    ///     Gets the findings in insertion order.
    /// </summary>
    public IReadOnlyList<Finding> Findings => _findings;

    /// <summary>
    ///     Gets the informational lines in insertion order.
    /// </summary>
    public IReadOnlyList<string> Info => _info;

    /// <summary>
    ///     Gets the total number of findings, including those the console output does not print.
    /// </summary>
    public int FindingCount => _findings.Count;

    /// <summary>
    ///     Gets the process exit code for the findings: <c>0</c> when there are none, <c>1</c> otherwise.
    /// </summary>
    public int ExitCode => _findings.Count == 0 ? 0 : 1;

    /// <summary>
    ///     Gets the final one-line verdict, <c>check-data: OK (0 findings)</c> or <c>check-data: FAILED (n findings)</c>.
    /// </summary>
    public string Summary => _findings.Count == 0
        ? "check-data: OK (0 findings)"
        : $"check-data: FAILED ({_findings.Count} findings)";

    /// <summary>
    ///     Records a violation.
    /// </summary>
    /// <param name="check">The check that found the violation.</param>
    /// <param name="location">The file (and optionally the line) the violation refers to.</param>
    /// <param name="message">A description of the violation.</param>
    public void Add(string check, string location, string message) =>
        _findings.Add(new Finding(check, location, message));

    /// <summary>
    ///     Records an informational line that is printed before the findings.
    /// </summary>
    /// <param name="line">The text to print.</param>
    public void AddInfo(string line) => _info.Add(line);

    /// <summary>
    ///     Writes the informational lines, the findings (capped per check) and the summary.
    /// </summary>
    /// <param name="writer">The destination writer.</param>
    public void WriteTo(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // Informational lines first, in the order the checks produced them
        foreach (var line in _info)
        {
            writer.WriteLine(line);
        }

        // Findings next: print up to the cap per check and remember how many were suppressed
        var printed = new Dictionary<string, int>(StringComparer.Ordinal);
        var suppressed = new Dictionary<string, int>(StringComparer.Ordinal);
        var checkOrder = new List<string>();
        foreach (var finding in _findings)
        {
            if (!printed.TryGetValue(finding.Check, out var count))
            {
                checkOrder.Add(finding.Check);
            }

            if (count < _maxFindingsPerCheck)
            {
                writer.WriteLine(finding);
                printed[finding.Check] = count + 1;
            }
            else
            {
                suppressed[finding.Check] = suppressed.GetValueOrDefault(finding.Check) + 1;
            }
        }

        // Tell the reader what was cut so a capped list is never mistaken for a complete one
        foreach (var check in checkOrder.Where(suppressed.ContainsKey))
        {
            writer.WriteLine($"check-data: {check}: ({suppressed[check]} more findings not shown)");
        }

        writer.WriteLine(Summary);
    }
}
