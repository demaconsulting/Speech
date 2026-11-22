using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Piper.ModelManagementSubsystem;

namespace DemaConsulting.Speech.ModelBundler;

/// <summary>
///     The compiled facts the tool takes from the model packages: each bundle's vendor files (with their original
///     URLs and SHA-256 pins) and each published bundle's zip pin.
/// </summary>
/// <remarks>
///     The vendor files come from each package's internal <c>BundleContents</c> (visible to this tool through
///     <c>InternalsVisibleTo</c>), so the packages remain the single source of truth for every per-file pin and the
///     tool never declares a hash of its own. Stateless and thread-safe.
/// </remarks>
internal static class BundleCatalog
{
    /// <summary>
    ///     Gets the compiled vendor files of every bundle the model packages declare, keyed by bundle name (ordinal).
    /// </summary>
    /// <returns>A new dictionary of the five bundles' contents.</returns>
    public static IReadOnlyDictionary<string, SpeechModelDownloadDescriptor> GetContents()
    {
        // Kokoro, Nemotron and Piper each ship one bundle; Chatterbox Turbo ships a language-model and an auxiliary bundle
        var contents = new Dictionary<string, SpeechModelDownloadDescriptor>(StringComparer.Ordinal)
        {
            [OnnxKokoroEnglishSynthesisModel.BundleName] = OnnxKokoroEnglishSynthesisModel.BundleContents,
            [OnnxNemotronMultilingualRecognitionModel.BundleName] = OnnxNemotronMultilingualRecognitionModel.BundleContents,
            [OnnxPiperLibriTtsEnSynthesisModel.BundleName] = OnnxPiperLibriTtsEnSynthesisModel.BundleContents,
        };
        foreach (var (name, files) in OnnxChatterboxTurboEnglishSynthesisModel.BundleContents)
        {
            contents.Add(name, files);
        }

        return contents;
    }

    /// <summary>
    ///     Gets the zip SHA-256 pins the model packages currently publish, keyed by bundle name (ordinal), so a build
    ///     can report whether a rebuilt zip matches the catalog (<c>MATCH</c>) or the catalog needs the new pin
    ///     (<c>UPDATE CATALOG</c>).
    /// </summary>
    /// <remarks>
    ///     Taken from the packages' public <c>Bundles</c> lists (each bundle's <c>Name</c> to its
    ///     <c>Sha256Checksum</c>).
    /// </remarks>
    /// <returns>A new dictionary of the published zip pins (lowercase hexadecimal).</returns>
    public static IReadOnlyDictionary<string, string> GetPublishedChecksums() =>
        OnnxKokoroEnglishSynthesisModel.Bundles
            .Concat(OnnxNemotronMultilingualRecognitionModel.Bundles)
            .Concat(OnnxPiperLibriTtsEnSynthesisModel.Bundles)
            .Concat(OnnxChatterboxTurboEnglishSynthesisModel.Bundles)
            .ToDictionary(bundle => bundle.Name, bundle => bundle.Sha256Checksum, StringComparer.Ordinal);
}
