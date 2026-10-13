using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioTagCatalog"/>, proving completeness of the closed tag
///     vocabulary's alias table and the normalization routine it shares with
///     <see cref="AudioTagParser"/>.
/// </summary>
public class AudioTagCatalogTests
{
    /// <summary>
    ///     Proves every declared <see cref="NaturalLanguageAudioTag"/> value appears in
    ///     <see cref="AudioTagCatalog.Tags"/> with at least one alias, so no canonical tag is
    ///     silently unreachable through <see cref="AudioTagCatalog.TryResolve"/>.
    /// </summary>
    [Fact]
    public void AudioTagCatalog_Tags_Always_CoversEveryCanonicalTagWithAtLeastOneAlias()
    {
        // Arrange: every declared enum value
        var allTags = Enum.GetValues<NaturalLanguageAudioTag>();

        // Act: the catalog's reported tags, indexed by canonical value
        var descriptors = AudioTagCatalog.Tags.ToDictionary(d => d.Tag);

        // Assert: every enum value is present with a non-empty alias list
        Assert.Multiple(() =>
        {
            foreach (var tag in allTags)
            {
                var found = descriptors.TryGetValue(tag, out var descriptor);
                Assert.True(found, $"{tag} is missing from AudioTagCatalog.Tags.");
                if (found)
                {
                    Assert.NotEmpty(descriptor!.Aliases);
                }
            }
        });
    }

    /// <summary>
    ///     Proves no two canonical tags share an alias, so resolving one piece of bracket text
    ///     can never be ambiguous between two different tags.
    /// </summary>
    [Fact]
    public void AudioTagCatalog_Tags_Always_NoAliasIsSharedByTwoDifferentTags()
    {
        // Arrange: every (alias, tag) pair across the whole catalog
        var aliasOwners = new Dictionary<string, NaturalLanguageAudioTag>();

        // Act / Assert: each alias is claimed by exactly one tag
        foreach (var descriptor in AudioTagCatalog.Tags)
        {
            foreach (var alias in descriptor.Aliases)
            {
                if (aliasOwners.TryGetValue(alias, out var owner))
                {
                    Assert.Fail($"Alias '{alias}' is claimed by both {owner} and {descriptor.Tag}.");
                }

                aliasOwners[alias] = descriptor.Tag;
            }
        }
    }

    /// <summary>
    ///     Proves every documented alias for every documented tag resolves through
    ///     <see cref="AudioTagCatalog.TryResolve"/>, so the catalog's own reported alias list is
    ///     never stale relative to its own lookup table.
    /// </summary>
    [Fact]
    public void AudioTagCatalog_TryResolve_EveryDocumentedAlias_ResolvesToItsOwnTag()
    {
        // Arrange / Act / Assert: resolve every documented alias in turn
        Assert.Multiple(() =>
        {
            foreach (var descriptor in AudioTagCatalog.Tags)
            {
                foreach (var alias in descriptor.Aliases)
                {
                    var resolved = AudioTagCatalog.TryResolve(alias, out var tag, out var kind);
                    Assert.True(resolved, $"Alias '{alias}' failed to resolve.");
                    Assert.Equal(descriptor.Tag, tag);
                    Assert.Equal(descriptor.Kind, kind);
                }
            }
        });
    }

    /// <summary>
    ///     Proves <see cref="AudioTagCatalog.Normalize"/> trims, lower-cases, and collapses
    ///     internal whitespace, since the parser relies on this to match aliases regardless of
    ///     the exact whitespace/casing a caller used inside brackets.
    /// </summary>
    [Theory]
    [InlineData("  Very   Slow  ", "very slow")]
    [InlineData("EXCITED", "excited")]
    [InlineData("Short\tPause", "short pause")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void AudioTagCatalog_Normalize_VariousInputs_ProducesExpectedNormalForm(string input, string expected)
    {
        // Act
        var normalized = AudioTagCatalog.Normalize(input);

        // Assert
        Assert.Equal(expected, normalized);
    }

    /// <summary>
    ///     Proves resolution is case-insensitive and tolerant of extra internal whitespace,
    ///     confirming the catalog's normalization is actually applied before lookup, not just
    ///     available as a separate helper.
    /// </summary>
    [Fact]
    public void AudioTagCatalog_TryResolve_MixedCaseWithExtraWhitespace_ResolvesToCanonicalTag()
    {
        // Act
        var resolved = AudioTagCatalog.TryResolve("  VERY    FAST  ", out var tag, out var kind);

        // Assert
        Assert.True(resolved);
        Assert.Equal(NaturalLanguageAudioTag.VeryFast, tag);
        Assert.Equal(NaturalLanguageAudioTagKind.Pace, kind);
    }

    /// <summary>
    ///     Proves null bracket content is rejected by both <see cref="AudioTagCatalog.Normalize"/>
    ///     and <see cref="AudioTagCatalog.TryResolve"/>.
    /// </summary>
    [Fact]
    public void AudioTagCatalog_NormalizeAndTryResolve_NullContent_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => AudioTagCatalog.Normalize(null!));
        Assert.Throws<ArgumentNullException>(() => AudioTagCatalog.TryResolve(null!, out _, out _));
    }

    /// <summary>
    ///     Proves an unrecognized word does not resolve, so callers can distinguish a genuine tag
    ///     from arbitrary bracketed text.
    /// </summary>
    [Fact]
    public void AudioTagCatalog_TryResolve_UnknownWord_ReturnsFalse()
    {
        // Act
        var resolved = AudioTagCatalog.TryResolve("gibberish", out _, out _);

        // Assert
        Assert.False(resolved);
    }

    /// <summary>
    ///     Proves empty or whitespace-only bracket content never resolves.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AudioTagCatalog_TryResolve_EmptyOrWhitespace_ReturnsFalse(string content)
    {
        // Act
        var resolved = AudioTagCatalog.TryResolve(content, out _, out _);

        // Assert
        Assert.False(resolved);
    }

    /// <summary>
    ///     Proves <see cref="AudioTagCatalog.Tags"/> is a genuine read-only wrapper - not the
    ///     mutable backing <see cref="List{T}"/> itself - so a caller cannot cast the property's
    ///     runtime instance back to <c>List&lt;AudioTagDescriptor&gt;</c> and mutate the
    ///     supposedly-static, single-source-of-truth tag table.
    /// </summary>
    [Fact]
    public void AudioTagCatalog_Tags_Always_IsNotCastableToMutableList()
    {
        // Act
        var tags = AudioTagCatalog.Tags;

        // Assert
        Assert.Null(tags as List<AudioTagDescriptor>);
    }

    /// <summary>
    ///     Proves each descriptor's <see cref="AudioTagDescriptor.Aliases"/> is a genuine read-only
    ///     wrapper - not the backing <c>string[]</c> - so a caller cannot cast it back to an array
    ///     and mutate the supposedly-immutable alias table.
    /// </summary>
    [Fact]
    public void AudioTagCatalog_Tags_Always_AliasesAreNotCastableToMutableArray()
    {
        // Act
        var descriptors = AudioTagCatalog.Tags;

        // Assert
        Assert.All(descriptors, d =>
        {
            Assert.Null(d.Aliases as string[]);
            Assert.Null(d.Aliases as List<string>);
        });
    }

    /// <summary>
    ///     Proves every canonical tag is assigned the independently expected kind, so a wrong
    ///     kind assignment in the catalog cannot pass merely because the test reads the same table.
    /// </summary>
    [Theory]
    [InlineData(NaturalLanguageAudioTag.Excited, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Serious, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Sarcastic, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Panicked, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Bored, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Sad, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Slow, NaturalLanguageAudioTagKind.Pace)]
    [InlineData(NaturalLanguageAudioTag.VerySlow, NaturalLanguageAudioTagKind.Pace)]
    [InlineData(NaturalLanguageAudioTag.Fast, NaturalLanguageAudioTagKind.Pace)]
    [InlineData(NaturalLanguageAudioTag.VeryFast, NaturalLanguageAudioTagKind.Pace)]
    [InlineData(NaturalLanguageAudioTag.ShortPause, NaturalLanguageAudioTagKind.Pause)]
    [InlineData(NaturalLanguageAudioTag.LongPause, NaturalLanguageAudioTagKind.Pause)]
    [InlineData(NaturalLanguageAudioTag.Emphasis, NaturalLanguageAudioTagKind.Emphasis)]
    [InlineData(NaturalLanguageAudioTag.Whispers, NaturalLanguageAudioTagKind.DeliveryVolume)]
    [InlineData(NaturalLanguageAudioTag.Soft, NaturalLanguageAudioTagKind.DeliveryVolume)]
    [InlineData(NaturalLanguageAudioTag.Loud, NaturalLanguageAudioTagKind.DeliveryVolume)]
    [InlineData(NaturalLanguageAudioTag.Breathy, NaturalLanguageAudioTagKind.DeliveryVolume)]
    [InlineData(NaturalLanguageAudioTag.Laughs, NaturalLanguageAudioTagKind.NonVerbal)]
    [InlineData(NaturalLanguageAudioTag.Sighs, NaturalLanguageAudioTagKind.NonVerbal)]
    [InlineData(NaturalLanguageAudioTag.Gasp, NaturalLanguageAudioTagKind.NonVerbal)]
    [InlineData(NaturalLanguageAudioTag.ClearsThroat, NaturalLanguageAudioTagKind.NonVerbal)]
    [InlineData(NaturalLanguageAudioTag.Snorts, NaturalLanguageAudioTagKind.NonVerbal)]
    [InlineData(NaturalLanguageAudioTag.Angry, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Calm, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Happy, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Enthused, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Surprised, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Worried, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Confused, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Frustrated, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData(NaturalLanguageAudioTag.Neutral, NaturalLanguageAudioTagKind.Emotion)]
    public void AudioTagCatalog_Tags_EveryCanonicalTag_HasExpectedKind(
        NaturalLanguageAudioTag tag,
        NaturalLanguageAudioTagKind expectedKind)
    {
        // Act
        var descriptor = Assert.Single(AudioTagCatalog.Tags, d => d.Tag == tag);

        // Assert
        Assert.Equal(expectedKind, descriptor.Kind);
    }

    /// <summary>
    ///     Proves every catalog mood tag (and the <c>shout</c> alias) resolves to its canonical
    ///     tag and the Emotion/DeliveryVolume kind, so mood-aware models can rely on them.
    /// </summary>
    [Theory]
    [InlineData("angry", NaturalLanguageAudioTag.Angry, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData("anger", NaturalLanguageAudioTag.Angry, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData("calm", NaturalLanguageAudioTag.Calm, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData("happy", NaturalLanguageAudioTag.Happy, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData("enthused", NaturalLanguageAudioTag.Enthused, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData("Enthusiastic", NaturalLanguageAudioTag.Enthused, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData("surprised", NaturalLanguageAudioTag.Surprised, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData("worried", NaturalLanguageAudioTag.Worried, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData("anxious", NaturalLanguageAudioTag.Worried, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData("confused", NaturalLanguageAudioTag.Confused, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData("frustrated", NaturalLanguageAudioTag.Frustrated, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData("neutral", NaturalLanguageAudioTag.Neutral, NaturalLanguageAudioTagKind.Emotion)]
    [InlineData("shout", NaturalLanguageAudioTag.Loud, NaturalLanguageAudioTagKind.DeliveryVolume)]
    public void AudioTagCatalog_TryResolve_MoodTagsAndShoutAlias_ResolveToExpectedTag(
        string content,
        NaturalLanguageAudioTag expectedTag,
        NaturalLanguageAudioTagKind expectedKind)
    {
        // Act
        var resolved = AudioTagCatalog.TryResolve(content, out var tag, out var kind);

        // Assert
        Assert.True(resolved);
        Assert.Equal(expectedTag, tag);
        Assert.Equal(expectedKind, kind);
    }
}
