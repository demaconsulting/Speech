using System.Collections.Concurrent;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.LetterToSoundSubsystem;

/// <summary>
///     Pronounces a word that is in neither the lexicon nor reachable by the morphology rules, with the trained
///     <see cref="LtsModel"/>.
/// </summary>
/// <remarks>
///     <para>
///     The phonemizer only asks whether a word can be pronounced, never how, so the strategy (the trained model chosen
///     by the benchmark in <c>docs/evaluation/phonemes-en/oov.md</c>) can be exchanged without touching it. A word is
///     never silent: when the model yields no vowel for a word that has letters (an initialism or interjection such as
///     <c>www</c> or <c>sq</c>, which a voice cannot say as a word), the word is spoken letter by letter by letter
///     name. Characters other than letters and apostrophes are ignored, so arbitrary text is safe.
///     </para>
///     <para>
///     Results are cached per word (the cache only saves time: the answer depends on the word alone, so it is
///     deterministic) in a bounded, thread-safe map. The class is thread-safe.
///     </para>
/// </remarks>
internal sealed class LetterToSound
{
    /// <summary>The number of words the cache keeps; when it is full new words are simply not cached.</summary>
    private const int CacheLimit = 4096;

    /// <summary>The longest word that is cached; longer words are rare and not worth the memory.</summary>
    private const int MaxCachedLength = 40;

    /// <summary>The lazily loaded shared instance.</summary>
    private static readonly Lazy<LetterToSound> SharedInstance = new(() => new LetterToSound(LtsModel.Default), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The model.</summary>
    private readonly LtsModel _model;

    /// <summary>The per-word cache.</summary>
    private readonly ConcurrentDictionary<string, Pronunciation> _cache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a letter-to-sound converter around a model.
    /// </summary>
    /// <param name="model">The model.</param>
    public LetterToSound(LtsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    /// <summary>Gets the shared converter of the embedded model; the model loads on first use.</summary>
    public static LetterToSound Default => SharedInstance.Value;

    /// <summary>
    ///     Pronounces a word.
    /// </summary>
    /// <param name="word">The lower-case word; letters <c>a</c> to <c>z</c> and apostrophes (other characters are ignored).</param>
    /// <param name="pronunciation">Receives the pronunciation when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the word could be pronounced (it has at least one letter).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="word"/> is null.</exception>
    public bool TryConvert(string word, out Pronunciation pronunciation)
    {
        ArgumentNullException.ThrowIfNull(word);

        // A cached answer
        if (_cache.TryGetValue(word, out var cached))
        {
            pronunciation = cached;
            return cached.Count > 0;
        }

        // The trained model, or the letter names when the model gives nothing or only consonants (www, sq, hmm)
        var result = _model.Convert(word);
        if (!HasVowel(result))
        {
            result = Spell(word);
        }

        if (word.Length <= MaxCachedLength && _cache.Count < CacheLimit)
        {
            _cache.TryAdd(word, result);
        }

        pronunciation = result;
        return result.Count > 0;
    }

    /// <summary>
    ///     Tests whether a pronunciation has a vowel, the minimum for something a voice can say as a word.
    /// </summary>
    /// <param name="pronunciation">The pronunciation.</param>
    /// <returns><see langword="true"/> when at least one phone is a vowel.</returns>
    private static bool HasVowel(Pronunciation pronunciation)
    {
        for (var i = 0; i < pronunciation.Count; i++)
        {
            if (pronunciation[i].IsVowel)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Speaks a word letter by letter, by letter name.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>The names of its letters; empty when it has none.</returns>
    private static Pronunciation Spell(string word)
    {
        var result = Pronunciation.Empty;
        foreach (var letter in word)
        {
            if (LetterNames.TryGet(letter, out var name))
            {
                result = result.Append(name.Phones);
            }
        }

        return result;
    }
}
