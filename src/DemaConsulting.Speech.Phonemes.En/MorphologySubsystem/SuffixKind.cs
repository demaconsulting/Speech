namespace DemaConsulting.Speech.Phonemes.En.MorphologySubsystem;

/// <summary>
///     The kinds of word ending that <see cref="Morphology"/> can derive from a stem, each with a fixed
///     sound rule.
/// </summary>
internal enum SuffixKind
{
    /// <summary>Plural noun or third-person singular verb ending <c>-s</c>/<c>-es</c>: <c>S</c>, <c>Z</c> or <c>IH0 Z</c> chosen by the stem's last phone.</summary>
    PluralS,

    /// <summary>Past tense and participle ending <c>-ed</c>: <c>T</c>, <c>D</c> or <c>IH0 D</c> chosen by the stem's last phone.</summary>
    PastEd,

    /// <summary>Present participle and gerund ending <c>-ing</c>: <c>IH0 NG</c>.</summary>
    Ing,

    /// <summary>Possessive <c>'s</c> (also the contraction of <c>is</c>/<c>has</c>), the same sounds as <see cref="PluralS"/>.</summary>
    PossessiveS,

    /// <summary>Plural possessive <c>s'</c>: the plural itself, with no extra sound.</summary>
    PluralPossessive,

    /// <summary>Ending <c>-er</c> (comparative, or the agent noun): <c>ER0</c>.</summary>
    ComparativeEr,

    /// <summary>Superlative ending <c>-est</c>: <c>AH0 S T</c>.</summary>
    SuperlativeEst,

    /// <summary>Adverb ending <c>-ly</c>: <c>L IY0</c>.</summary>
    AdverbLy,

    /// <summary>Contraction <c>'d</c> (would, had).</summary>
    ContractionD,

    /// <summary>Contraction <c>'ll</c> (will).</summary>
    ContractionLl,

    /// <summary>Contraction <c>'ve</c> (have).</summary>
    ContractionVe,

    /// <summary>Contraction <c>'re</c> (are).</summary>
    ContractionRe,

    /// <summary>Contraction <c>'m</c> (am).</summary>
    ContractionM,

    /// <summary>Contraction <c>n't</c> (not).</summary>
    ContractionNt,
}
