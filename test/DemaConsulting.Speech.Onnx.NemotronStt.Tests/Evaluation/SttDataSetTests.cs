using DemaConsulting.Speech.SttEvaluation;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.Evaluation;

/// <summary>
///     Unit tests for <see cref="SttDataSet"/>, proving that the committed speech-to-text
///     evaluation data (<c>data/stt-en</c>, copied beside the test assembly) matches its manifest
///     and that tampered data is refused rather than measured.
/// </summary>
public sealed class SttDataSetTests : IDisposable
{
    /// <summary>The scratch folder of the tests that build their own manifest.</summary>
    private readonly string _scratch = Path.Join(Path.GetTempPath(), "stt-data-set-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Gets the copy of the committed data beside the test assembly.</summary>
    private static string DataDirectory => Path.Join(AppContext.BaseDirectory, "TestData", "stt-en");

    /// <summary>Deletes the scratch folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_scratch))
        {
            Directory.Delete(_scratch, recursive: true);
        }
    }

    /// <summary>
    ///     Proves that every file of the committed data matches its pinned size and SHA-256, and
    ///     that both sets are present with the expected number of recordings.
    /// </summary>
    [Fact]
    public void SttDataSet_Load_RepositoryManifest_EveryFileMatchesItsChecksum()
    {
        // Arrange / Act: loading verifies every file
        var data = SttDataSet.Load(DataDirectory);

        // Assert: 24 LibriSpeech utterances and the poem
        Assert.Equal(24, data.GetSet(SttDataSet.LibriSpeechSet).Count);
        Assert.Single(data.GetSet(SttDataSet.CrossingTheBarSet));
        Assert.Equal(25, data.Items.Count);
    }

    /// <summary>
    ///     Proves that every LibriSpeech file carries the CC BY 4.0 license and an upper-case,
    ///     unpunctuated corpus reference, and the poem carries CC0 and a punctuated reference.
    /// </summary>
    [Fact]
    public void SttDataSet_Load_LibriSpeechSet_HasCcBy40License()
    {
        // Arrange / Act
        var data = SttDataSet.Load(DataDirectory);

        // Assert
        Assert.All(data.GetSet(SttDataSet.LibriSpeechSet), item =>
        {
            Assert.Equal("CC-BY-4.0", item.License);
            Assert.False(item.HasPunctuatedReference);
            Assert.StartsWith("https://www.openslr.org/resources/12/test-clean.tar.gz", item.Source, StringComparison.Ordinal);
        });
        var poem = Assert.Single(data.GetSet(SttDataSet.CrossingTheBarSet));
        Assert.Equal("CC0-1.0", poem.License);
        Assert.True(poem.HasPunctuatedReference);
    }

    /// <summary>
    ///     Proves that a committed recording decodes to 16 kHz mono audio of the duration the
    ///     manifest records.
    /// </summary>
    [Fact]
    public void SttDataSet_LoadSamples_LibriSpeechFile_DecodesManifestDuration()
    {
        // Arrange
        var item = SttDataSet.Load(DataDirectory).GetSet(SttDataSet.LibriSpeechSet)[0];

        // Act
        var samples = SttDataSet.LoadSamples(item, 16000);

        // Assert: the decoded length matches the manifest duration to the millisecond
        Assert.Equal(item.Seconds, samples.Length / 16000.0, precision: 3);
    }

    /// <summary>
    ///     Proves that a file whose content differs from its pinned SHA-256 is refused with an
    ///     <see cref="InvalidDataException"/> naming the file.
    /// </summary>
    [Fact]
    public void SttDataSet_Load_ChecksumMismatch_ThrowsInvalidDataException()
    {
        // Arrange: a four-byte file pinned with the right size but a wrong hash
        Directory.CreateDirectory(_scratch);
        File.WriteAllBytes(Path.Join(_scratch, "a.wav"), [1, 2, 3, 4]);
        File.WriteAllText(
            Path.Join(_scratch, SttDataSet.ManifestFileName),
            "# comment\na.wav\tset\t" + new string('0', 64) + "\t4\t0.1\tCC0-1.0\thttps://example.com\tREFERENCE\n");

        // Act / Assert
        var ex = Assert.Throws<InvalidDataException>(() => SttDataSet.Load(_scratch));
        Assert.Contains("a.wav", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a manifest path leaving the data folder is refused, so a corrupt manifest
    ///     can never make the tool read an arbitrary file.
    /// </summary>
    [Fact]
    public void SttDataSet_Load_PathOutsideFolder_ThrowsInvalidDataException()
    {
        // Arrange
        Directory.CreateDirectory(_scratch);
        File.WriteAllText(
            Path.Join(_scratch, SttDataSet.ManifestFileName),
            "../outside.wav\tset\t" + new string('0', 64) + "\t4\t0.1\tCC0-1.0\thttps://example.com\tREFERENCE\n");

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => SttDataSet.Load(_scratch));
    }
}
