using System.Diagnostics;
using DemaConsulting.Speech.Phonemes.En.LetterToSoundSubsystem;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.LetterToSoundSubsystem;

/// <summary>
///     The settings of the three networks of the letter-to-sound model.
/// </summary>
/// <param name="Phone">The phone network (letter to phone chunk).</param>
/// <param name="Primary">The primary stress network.</param>
/// <param name="Secondary">The secondary stress network.</param>
internal sealed record LtsTrainingSettings(NetworkSettings Phone, NetworkSettings Primary, NetworkSettings Secondary)
{
    /// <summary>
    ///     Gets the settings of the shipped model; the benchmark's held-out numbers are measured with exactly these.
    /// </summary>
    public static LtsTrainingSettings Shipped { get; } = new(
        new NetworkSettings(Hidden1: 256, Hidden2: 128, Epochs: 20, LearningRate: 2e-3f, BatchSize: 128, Seed: 1, Dropout: 0.2f),
        new NetworkSettings(Hidden1: 96, Hidden2: 48, Epochs: 8, LearningRate: 3e-3f, BatchSize: 64, Seed: 2, Dropout: 0.2f),
        new NetworkSettings(Hidden1: 64, Hidden2: 32, Epochs: 8, LearningRate: 3e-3f, BatchSize: 64, Seed: 3, Dropout: 0.2f));

    /// <summary>
    ///     Gets the variants the benchmark compares, by name: a smaller and a larger phone network around the shipped one.
    /// </summary>
    public static IReadOnlyDictionary<string, LtsTrainingSettings> Variants { get; } = new Dictionary<string, LtsTrainingSettings>(StringComparer.Ordinal)
    {
        ["small"] = new(
            new NetworkSettings(Hidden1: 128, Hidden2: 64, Epochs: 20, LearningRate: 2e-3f, BatchSize: 128, Seed: 1, Dropout: 0.1f),
            new NetworkSettings(Hidden1: 64, Hidden2: 32, Epochs: 8, LearningRate: 3e-3f, BatchSize: 64, Seed: 2, Dropout: 0.1f),
            new NetworkSettings(Hidden1: 32, Hidden2: 16, Epochs: 8, LearningRate: 3e-3f, BatchSize: 64, Seed: 3, Dropout: 0.1f)),
        ["shipped"] = Shipped,
        ["large"] = new(
            new NetworkSettings(Hidden1: 384, Hidden2: 192, Epochs: 20, LearningRate: 2e-3f, BatchSize: 128, Seed: 1, Dropout: 0.3f),
            new NetworkSettings(Hidden1: 128, Hidden2: 64, Epochs: 8, LearningRate: 3e-3f, BatchSize: 64, Seed: 2, Dropout: 0.3f),
            new NetworkSettings(Hidden1: 64, Hidden2: 32, Epochs: 8, LearningRate: 3e-3f, BatchSize: 64, Seed: 3, Dropout: 0.3f)),
    };
}

/// <summary>
///     Trains the letter-to-sound model from dictionary entries and serializes it.
/// </summary>
/// <remarks>
///     <para>
///     The pipeline: align the letters of every training word with its phones (<see cref="LtsAligner"/>), keep the
///     phone chunks seen at least three times as the classes of the phone network, build one sample per letter
///     (phone network), one per vowel (primary stress network) and one per non-primary vowel (secondary network),
///     train the three <see cref="FloatNetwork"/>s, and quantize them into the image <see cref="LtsModel"/> reads.
///     Only the first pronunciation variant of a word is trained on. The result is a pure function of the entries and
///     the settings (see <see cref="FloatNetwork"/> for what that requires).
///     </para>
/// </remarks>
internal static class LtsTrainer
{
    /// <summary>The fewest occurrences of a phone chunk for it to be a class of the phone network.</summary>
    private const int MinChunkCount = 3;

    /// <summary>
    ///     Trains a model and returns its uncompressed image.
    /// </summary>
    /// <param name="training">The training entries.</param>
    /// <param name="validation">Entries to report accuracy on while training (never trained on), or empty.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="log">Receives progress lines.</param>
    /// <returns>The image bytes.</returns>
    public static byte[] Train(IReadOnlyList<LexiconEntry> training, IReadOnlyList<LexiconEntry> validation, LtsTrainingSettings settings, Action<string> log)
    {
        var clock = Stopwatch.StartNew();
        var all = training.Concat(validation).Select(Prepare).ToList();
        var trainCount = training.Count;

        // Align letters with phones, and build the chunk classes from the training words
        var alignments = LtsAligner.Align(all.Select(w => (w.Letters, w.BaseIds)).ToList());
        var counts = new Dictionary<int, int>();
        for (var i = 0; i < trainCount; i++)
        {
            foreach (var key in alignments[i] ?? [])
            {
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }

        var keys = new List<int> { 0 };
        keys.AddRange(counts.Where(c => c.Key != 0 && c.Value >= MinChunkCount).OrderByDescending(c => c.Value).ThenBy(c => c.Key).Select(c => c.Key).Take(LtsModel.MaxClasses - 1));
        var classOf = new Dictionary<int, int>();
        for (var i = 0; i < keys.Count; i++)
        {
            classOf[keys[i]] = i;
        }

        log($"Aligned {all.Count} words in {clock.Elapsed.TotalSeconds:F1} s; {keys.Count} phone chunk classes.");

        // The phone network
        var phoneTrain = PhoneSamples(all.Take(trainCount).ToList(), alignments, 0, classOf);
        var phoneValidation = validation.Count == 0 ? null : PhoneSamples(all.Skip(trainCount).ToList(), alignments, trainCount, classOf);
        log($"Phone network: {phoneTrain.Count} letter samples.");
        var phoneNet = new FloatNetwork([.. LtsModel.PhoneGroups], settings.Phone.Hidden1, settings.Phone.Hidden2, keys.Count, settings.Phone.Seed);
        phoneNet.Train(phoneTrain, settings.Phone, phoneValidation, log);

        // The stress networks
        var (primaryTrain, secondaryTrain) = StressSamples(all.Take(trainCount).ToList());
        TrainingSet? primaryValidation = null;
        TrainingSet? secondaryValidation = null;
        if (validation.Count > 0)
        {
            (primaryValidation, secondaryValidation) = StressSamples(all.Skip(trainCount).ToList());
        }
        log($"Primary stress network: {primaryTrain.Count} vowel samples; secondary: {secondaryTrain.Count}.");
        var primaryNet = new FloatNetwork([.. LtsModel.PrimaryGroups], settings.Primary.Hidden1, settings.Primary.Hidden2, 2, settings.Primary.Seed);
        primaryNet.Train(primaryTrain, settings.Primary, primaryValidation, log);
        var secondaryNet = new FloatNetwork([.. LtsModel.SecondaryGroups], settings.Secondary.Hidden1, settings.Secondary.Hidden2, 2, settings.Secondary.Seed);
        secondaryNet.Train(secondaryTrain, settings.Secondary, secondaryValidation, log);

        // Serialize
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("LTS1"u8);
            writer.Write((ushort)keys.Count);
            foreach (var key in keys)
            {
                var phones = LtsAligner.Phones(key);
                writer.Write((byte)phones.Length);
                writer.Write(phones);
            }

            phoneNet.WriteQuantized(writer, phoneTrain);
            primaryNet.WriteQuantized(writer, primaryTrain);
            secondaryNet.WriteQuantized(writer, secondaryTrain);
        }

        log($"Trained in {clock.Elapsed.TotalSeconds:F0} s; image {stream.Length} bytes.");
        return stream.ToArray();
    }

    /// <summary>
    ///     Reads the letters, phones and stress of the first variant of an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The prepared word.</returns>
    internal static PreparedWord Prepare(LexiconEntry entry)
    {
        var letters = entry.Word.Select(LtsModel.LetterId).ToArray();
        var phones = entry.Pronunciations[0];
        var baseIds = new byte[phones.Count];
        var stress = new int[phones.Count];
        for (var i = 0; i < phones.Count; i++)
        {
            baseIds[i] = (byte)LtsModel.BaseId(phones[i]);
            stress[i] = phones[i].Stress;
        }

        return new PreparedWord(letters, baseIds, stress);
    }

    /// <summary>
    ///     Builds the samples of the phone network: one per letter of every aligned word whose chunks are all classes.
    /// </summary>
    /// <param name="words">The words.</param>
    /// <param name="alignments">The alignment of all words.</param>
    /// <param name="alignmentOffset">The index in <paramref name="alignments"/> of the first of <paramref name="words"/>.</param>
    /// <param name="classOf">The class of every chunk key.</param>
    /// <returns>The samples.</returns>
    private static TrainingSet PhoneSamples(List<PreparedWord> words, IReadOnlyList<int[]?> alignments, int alignmentOffset, Dictionary<int, int> classOf)
    {
        var groups = LtsModel.PhoneGroups.Count;
        var values = new List<int>();
        var labels = new List<int>();
        var features = new int[groups];
        for (var w = 0; w < words.Count; w++)
        {
            var alignment = alignments[alignmentOffset + w];
            if (alignment is null || alignment.Any(key => !classOf.ContainsKey(key)))
            {
                continue;
            }

            var classes = alignment.Select(key => classOf[key]).ToArray();
            for (var i = 0; i < alignment.Length; i++)
            {
                LtsModel.PhoneFeatures(words[w].Letters, i, classes, features);
                values.AddRange(features);
                labels.Add(classes[i]);
            }
        }

        return new TrainingSet(groups, [.. values], [.. labels]);
    }

    /// <summary>
    ///     Builds the samples of the stress networks from the gold phones: words of two or more vowels with exactly one primary stress.
    /// </summary>
    /// <param name="words">The words.</param>
    /// <returns>The primary network's samples (a vowel is primary or not) and the secondary network's (a non-primary vowel has secondary stress or not).</returns>
    private static (TrainingSet Primary, TrainingSet Secondary) StressSamples(List<PreparedWord> words)
    {
        var primaryValues = new List<int>();
        var primaryLabels = new List<int>();
        var secondaryValues = new List<int>();
        var secondaryLabels = new List<int>();
        var primaryFeatures = new int[LtsModel.PrimaryGroups.Count];
        var secondaryFeatures = new int[LtsModel.SecondaryGroups.Count];
        foreach (var word in words)
        {
            var vowels = Enumerable.Range(0, word.BaseIds.Length).Where(p => word.BaseIds[p] >= LtsModel.FirstVowelId).ToArray();
            var primary = Array.FindIndex(vowels, p => word.Stress[p] == 1);
            if (vowels.Length < 2 || primary < 0 || vowels.Count(p => word.Stress[p] == 1) != 1)
            {
                continue;
            }

            for (var v = 0; v < vowels.Length; v++)
            {
                LtsModel.StressFeatures(word.Letters, word.BaseIds, vowels, v, -1, primaryFeatures);
                primaryValues.AddRange(primaryFeatures);
                primaryLabels.Add(v == primary ? 1 : 0);
                if (v == primary)
                {
                    continue;
                }

                LtsModel.StressFeatures(word.Letters, word.BaseIds, vowels, v, primary, secondaryFeatures);
                secondaryValues.AddRange(secondaryFeatures);
                secondaryLabels.Add(word.Stress[vowels[v]] == 2 ? 1 : 0);
            }
        }

        return (
            new TrainingSet(primaryFeatures.Length, [.. primaryValues], [.. primaryLabels]),
            new TrainingSet(secondaryFeatures.Length, [.. secondaryValues], [.. secondaryLabels]));
    }
}

/// <summary>
///     A word prepared for training: letter ids, base phone ids and the stress digit of every phone (<c>-1</c> for a consonant).
/// </summary>
/// <param name="Letters">The letter ids.</param>
/// <param name="BaseIds">The base phone ids of the first pronunciation variant.</param>
/// <param name="Stress">The stress digit of every phone.</param>
internal sealed record PreparedWord(int[] Letters, byte[] BaseIds, int[] Stress);
