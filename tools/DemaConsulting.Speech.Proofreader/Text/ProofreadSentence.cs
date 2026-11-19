namespace DemaConsulting.Speech.Proofreader.Text;

/// <summary>
///     One sentence of the proofread text, located in the source text.
/// </summary>
/// <param name="Index">The zero-based position of the sentence in the whole text; flags refer to it.</param>
/// <param name="Start">The UTF-16 offset of the sentence's first character in the source text.</param>
/// <param name="Length">The length of the sentence in UTF-16 code units.</param>
/// <param name="Text">The sentence exactly as it appears in the source text (a slice, never rewritten).</param>
internal sealed record ProofreadSentence(int Index, int Start, int Length, string Text);
