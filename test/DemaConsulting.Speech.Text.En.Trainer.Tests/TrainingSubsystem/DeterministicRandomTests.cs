using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingSubsystem;

/// <summary>
///     Unit tests for <see cref="DeterministicRandom"/>: the SplitMix64 sequence, unbiased bounded draws and the
///     seeded shuffle that fix the order training rows and bootstrap clusters are visited in.
/// </summary>
public sealed class DeterministicRandomTests
{
    /// <summary>Proves the sequence from seed zero equals the published SplitMix64 reference outputs.</summary>
    [Fact]
    public void DeterministicRandom_NextUInt64_SeedZero_MatchesReferenceSequence()
    {
        // Arrange
        var random = new DeterministicRandom(0);

        // Act
        var outputs = Enumerable.Range(0, 4).Select(_ => random.NextUInt64()).ToArray();

        // Assert
        Assert.Equal([16294208416658607535UL, 7960286522194355700UL, 487617019471545679UL, 17909611376780542444UL], outputs);
    }

    /// <summary>Proves a second reference seed gives the reference outputs, so the recurrence, not a coincidence of zero, is right.</summary>
    [Fact]
    public void DeterministicRandom_NextUInt64_Seed1234567_MatchesReferenceSequence()
    {
        // Arrange
        var random = new DeterministicRandom(1234567);

        // Act
        var outputs = Enumerable.Range(0, 3).Select(_ => random.NextUInt64()).ToArray();

        // Assert
        Assert.Equal([6457827717110365317UL, 3203168211198807973UL, 9817491932198370423UL], outputs);
    }

    /// <summary>Proves the bounded draws of a seed are a fixed sequence of in-range values.</summary>
    [Fact]
    public void DeterministicRandom_NextInt_Seed42_MatchesReferenceSequence()
    {
        // Arrange
        var random = new DeterministicRandom(42);

        // Act
        var draws = Enumerable.Range(0, 5).Select(_ => random.NextInt(1000)).ToArray();

        // Assert
        Assert.Equal([413, 291, 858, 764, 250], draws);
    }

    /// <summary>Proves a bound of one always gives zero and a larger bound never reaches it.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(1000003)]
    public void DeterministicRandom_NextInt_AnyBound_StaysBelowTheBound(int bound)
    {
        // Arrange
        var random = new DeterministicRandom(99);

        // Act
        var draws = Enumerable.Range(0, 500).Select(_ => random.NextInt(bound)).ToList();

        // Assert
        Assert.All(draws, draw => Assert.InRange(draw, 0, bound - 1));
    }

    /// <summary>Proves a non-positive bound is rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void DeterministicRandom_NextInt_NonPositiveBound_ThrowsArgumentOutOfRangeException(int bound)
    {
        // Arrange
        var random = new DeterministicRandom(1);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(bound));
    }

    /// <summary>Proves every value below the bound is drawn about equally often.</summary>
    [Fact]
    public void DeterministicRandom_NextInt_ManyDraws_AreRoughlyUniform()
    {
        // Arrange
        var random = new DeterministicRandom(2024);
        var counts = new int[6];

        // Act
        for (var i = 0; i < 60000; i++)
        {
            counts[random.NextInt(6)]++;
        }

        // Assert: each of six values is expected 10000 times
        Assert.All(counts, count => Assert.InRange(count, 9500, 10500));
    }

    /// <summary>Proves a shuffle is a permutation that depends only on the seed and equals the reference order.</summary>
    [Fact]
    public void DeterministicRandom_Shuffle_Seed42_ProducesReferencePermutation()
    {
        // Arrange
        var items = Enumerable.Range(0, 10).ToArray();

        // Act
        new DeterministicRandom(42).Shuffle(items.AsSpan());

        // Assert: the order equals an independent implementation of Fisher-Yates over SplitMix64
        Assert.Equal([0, 9, 5, 8, 6, 4, 7, 2, 1, 3], items);
    }

    /// <summary>Proves a shuffle keeps every item exactly once, repeats for the same seed and changes with the seed.</summary>
    [Fact]
    public void DeterministicRandom_Shuffle_SameSeedRepeatsAndDifferentSeedDiffers()
    {
        // Arrange
        var first = Enumerable.Range(0, 100).ToArray();
        var again = Enumerable.Range(0, 100).ToArray();
        var other = Enumerable.Range(0, 100).ToArray();

        // Act
        new DeterministicRandom(5).Shuffle(first.AsSpan());
        new DeterministicRandom(5).Shuffle(again.AsSpan());
        new DeterministicRandom(6).Shuffle(other.AsSpan());

        // Assert
        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
        Assert.Equal(Enumerable.Range(0, 100), first.Order());
        Assert.NotEqual(Enumerable.Range(0, 100), first);
    }

    /// <summary>Proves shuffling nothing or one item leaves it as it is.</summary>
    [Fact]
    public void DeterministicRandom_Shuffle_EmptyAndSingleItem_AreUnchanged()
    {
        // Arrange
        var empty = Array.Empty<int>();
        var single = new[] { 7 };

        // Act
        new DeterministicRandom(3).Shuffle(empty.AsSpan());
        new DeterministicRandom(3).Shuffle(single.AsSpan());

        // Assert
        Assert.Empty(empty);
        Assert.Equal([7], single);
    }
}
