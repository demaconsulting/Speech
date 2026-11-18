using System.Globalization;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.MorphologySubsystem;

/// <summary>
///     The <c>measure-morphology</c> command: measures the morphology rules on the embedded lexicon and prints the
///     agreement per ending with the disagreement classes.
/// </summary>
/// <remarks>
///     Reads only the lexicon embedded in the library, so it needs no external files. With <c>--min-class-rate</c>
///     the command also decides a gate: exit code <c>1</c> when the plural or past ending's class agreement is
///     below the rate. The method is described on <see cref="MorphologyAudit"/>.
/// </remarks>
internal static class MeasureMorphologyCommand
{
    /// <summary>The name of the command as typed on the command line.</summary>
    public const string Name = "measure-morphology";

    /// <summary>The usage text.</summary>
    public const string Usage = """
        Usage: measure-morphology [options]

        Derives every lexicon entry that is a listed stem plus an ending from the stem and reports the agreement
        with the lexicon per ending class, with the disagreement classes.

        Options:
          --examples <n>          Disagreement classes listed per ending (default 6).
          --min-class-rate <r>    Exit 1 when the class agreement of the plural or past ending is below r (0 to 1).
          --help                  Shows this text.

        Exit codes: 0 = measured (and gate met), 1 = gate not met, 2 = usage error.
        """;

    /// <summary>
    ///     Runs the command.
    /// </summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <param name="output">Receives the report.</param>
    /// <param name="error">Receives usage errors.</param>
    /// <returns>The exit code: 0, 1 or 2.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var examples = 6;
        double? gate = null;
        var pending = new Queue<string>(args);
        while (pending.TryDequeue(out var option))
        {
            switch (option)
            {
                case "--help" or "-h":
                    output.WriteLine(Usage);
                    return 0;
                case "--examples" when pending.Count > 0 && int.TryParse(pending.Peek(), NumberStyles.None, CultureInfo.InvariantCulture, out var n):
                    examples = n;
                    pending.Dequeue();
                    break;
                case "--min-class-rate" when pending.Count > 0 && double.TryParse(pending.Peek(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rate):
                    gate = rate;
                    pending.Dequeue();
                    break;
                default:
                    error.WriteLine($"Unknown or invalid option '{option}'.");
                    error.WriteLine(Usage);
                    return 2;
            }
        }

        var reports = MorphologyAudit.Run(CmuLexicon.Default);
        output.WriteLine("Morphology agreement with the lexicon (stems listed in the lexicon only)");
        output.WriteLine("ending | candidates | no stem prefix | compared | exact agree | class agree (reduced vowel ignored)");
        foreach (var report in reports)
        {
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{report.Kind} | {report.Candidates} | {report.NoStemPrefix} | {report.Compared} | {report.ExactAgree} ({report.ExactRate:P2}) | {report.ClassAgree} ({report.ClassRate:P2})"));
        }

        foreach (var report in reports)
        {
            output.WriteLine();
            output.WriteLine($"{report.Kind}: top disagreement classes");
            foreach (var cls in report.Disagreements.Take(examples))
            {
                output.WriteLine($"  {cls.Count,6}  {cls.Pattern}  e.g. {string.Join(", ", cls.Examples)}");
            }
        }

        // The gate covers the two decisions the owner named: s/z/iz and t/d/id
        var failed = gate is { } minimum
            && reports.Any(r => r.Kind is SuffixKind.PluralS or SuffixKind.PastEd && r.ClassRate < minimum);
        if (failed)
        {
            error.WriteLine($"Gate not met: class agreement of the plural or past ending is below {gate}.");
        }

        return failed ? 1 : 0;
    }
}
