using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.ModelBundler;

/// <summary>
///     One license that applies to part of a bundle's vendor files, as declared in <c>bundles.json</c>.
/// </summary>
/// <param name="Name">The SPDX-style license name, for example <c>Apache-2.0</c>.</param>
/// <param name="Url">The license's canonical URL.</param>
/// <param name="AppliesTo">A short description of the files or work the license covers.</param>
/// <param name="Text">The full license text, with LF line endings and exactly one trailing newline.</param>
internal sealed record BundleLicense(string Name, string Url, string AppliesTo, string Text);

/// <summary>
///     The declaration of one model bundle from <c>bundles.json</c>: its name parts, the catalog ids that install it,
///     its upstream source, licenses, attribution and notice text.
/// </summary>
/// <remarks>
///     The vendor files themselves are not declared here: they come from the model packages' compiled
///     <c>BundleContents</c> (see <see cref="BundleCatalog"/>), so the definitions never duplicate a SHA-256 pin.
///     Instances are immutable.
/// </remarks>
/// <param name="Name">The bundle name (the zip file name without <c>.zip</c>), equal to <c>{Model}-{UpstreamRelease}-{Quant}-b{BundleRevision}</c>.</param>
/// <param name="Model">The model token of the name.</param>
/// <param name="UpstreamRelease">The upstream release token of the name.</param>
/// <param name="Quant">The quantization token of the name (<c>fp32</c>, <c>fp16</c>, <c>int8</c> or <c>int4</c>).</param>
/// <param name="BundleRevision">The bundle revision (the <c>N</c> of <c>bN</c>), at least 1.</param>
/// <param name="Date">The fixed bundle date: the README date and the zip entry timestamps (never the wall clock).</param>
/// <param name="ModelIds">The catalog model ids that install the bundle once it is published, in declared order.</param>
/// <param name="UpstreamRepository">The upstream repository URL the vendor files come from.</param>
/// <param name="UpstreamRevision">The upstream revision description (the pinned snapshot).</param>
/// <param name="Licenses">The licenses of the vendor files, in declared order (at least one).</param>
/// <param name="Attribution">The required attribution text.</param>
/// <param name="NoticeLines">The NOTICE text lines (attribution, copyright and notices of origin).</param>
internal sealed record BundleDefinition(
    string Name,
    string Model,
    string UpstreamRelease,
    string Quant,
    int BundleRevision,
    DateOnly Date,
    IReadOnlyList<string> ModelIds,
    string UpstreamRepository,
    string UpstreamRevision,
    IReadOnlyList<BundleLicense> Licenses,
    string Attribution,
    IReadOnlyList<string> NoticeLines)
{
    /// <summary>Gets the bundle zip file name, <c>{Name}.zip</c>.</summary>
    public string FileName => Name + ".zip";
}

/// <summary>
///     Loads and validates <c>bundles.json</c>, the declaration of every model bundle the tool can build.
/// </summary>
/// <remarks>
///     Validation is strict and eager so a malformed declaration never produces a bundle: every field is required,
///     the name must equal its parts and the published naming rule, the date must be <c>yyyy-MM-dd</c>, each model id
///     must be a single safe directory name (it becomes a source-store path segment), and each license text file must
///     be a relative path without <c>..</c> segments that resolves inside the definitions file's directory and
///     exists, so a definition can never embed an arbitrary local file in a public bundle. License texts are read as
///     UTF-8 and normalized to LF line endings with one trailing newline, so a checkout's line-ending setting can never
///     change a bundle's bytes. Stateless and thread-safe.
/// </remarks>
internal static partial class BundleDefinitions
{
    /// <summary>The definitions file name, which sits beside the executable.</summary>
    internal const string FileName = "bundles.json";

    /// <summary>The path separators recognized in a license text path on every platform.</summary>
    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>
    ///     Loads every bundle definition from a definitions file.
    /// </summary>
    /// <param name="path">The path of the <c>bundles.json</c> file. Must exist.</param>
    /// <returns>The definitions, in file order.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file or a license text file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not valid JSON or a definition breaks a rule.</exception>
    public static IReadOnlyList<BundleDefinition> Load(string path)
    {
        // Parse the whole document up front; a JSON error is reported as invalid data with the file name
        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllBytes(path));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"'{path}' is not valid JSON.", ex);
        }

        using (document)
        {
            var bundles = GetArray(document.RootElement, "bundles", "the root");
            var definitions = bundles.EnumerateArray().Select(element => Parse(element, directory)).ToList();

            // Bundle names identify assets on the release, so a duplicate could never be published twice
            var duplicate = definitions.GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null)
            {
                throw new InvalidDataException($"The bundle '{duplicate.Key}' is declared more than once in '{path}'.");
            }

            return definitions;
        }
    }

    /// <summary>Gets the path of the definitions file shipped beside the executable.</summary>
    /// <returns>The full path of <c>bundles.json</c> in the application base directory.</returns>
    public static string GetDefaultPath() => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>Normalizes text to LF line endings with exactly one trailing newline.</summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text.</returns>
    internal static string NormalizeText(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n') + "\n";

    /// <summary>Parses and validates one bundle definition element.</summary>
    /// <param name="element">The JSON object of the definition.</param>
    /// <param name="directory">The directory license text paths are relative to.</param>
    /// <returns>The validated definition.</returns>
    /// <exception cref="InvalidDataException">Thrown when a field is missing, malformed or inconsistent.</exception>
    private static BundleDefinition Parse(JsonElement element, string directory)
    {
        // Read the name parts and check the name is exactly their composition and follows the naming rule
        var name = GetString(element, "name", "a bundle");
        var model = GetString(element, "model", name);
        var upstreamRelease = GetString(element, "upstreamRelease", name);
        var quant = GetString(element, "quant", name);
        var revision = GetInt(element, "bundleRevision", name);
        if (revision < 1 || !string.Equals(name, $"{model}-{upstreamRelease}-{quant}-b{revision}", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The bundle '{name}' does not equal its parts '{model}-{upstreamRelease}-{quant}-b{revision}'.");
        }

        if (!NamePattern().IsMatch(name))
        {
            throw new InvalidDataException($"The bundle name '{name}' does not follow the bundle naming rule.");
        }

        // The date is fixed in the file so the zip timestamps never depend on the wall clock
        var dateText = GetString(element, "date", name);
        if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date.Year < 1980 || date.Year > 2107)
        {
            throw new InvalidDataException($"The bundle '{name}' has the invalid date '{dateText}' (expected yyyy-MM-dd, 1980-2107).");
        }

        // Model ids, upstream source, licenses, attribution and notice are all required
        var modelIds = GetStrings(element, "modelIds", name);
        var unsafeModelId = modelIds.FirstOrDefault(modelId => !ModelIdPattern().IsMatch(modelId));
        if (unsafeModelId is not null)
        {
            throw new InvalidDataException(
                $"The model id '{unsafeModelId}' of bundle '{name}' is not a single safe directory name "
                + "(lowercase letters and digits separated by single '.', '_' or '-').");
        }

        var upstream = GetObject(element, "upstream", name);
        var licenses = GetArray(element, "licenses", name).EnumerateArray()
            .Select(license => ParseLicense(license, name, directory))
            .ToList();
        if (licenses.Count == 0)
        {
            throw new InvalidDataException($"The bundle '{name}' declares no license.");
        }

        return new BundleDefinition(
            name,
            model,
            upstreamRelease,
            quant,
            revision,
            date,
            modelIds,
            GetString(upstream, "repository", name),
            GetString(upstream, "revision", name),
            licenses,
            GetString(element, "attribution", name),
            GetStrings(element, "notice", name));
    }

    /// <summary>Parses one license entry and reads its text file.</summary>
    /// <param name="element">The JSON object of the license.</param>
    /// <param name="bundleName">The bundle being parsed, for error messages.</param>
    /// <param name="directory">The directory the text file path is relative to.</param>
    /// <returns>The license with its normalized text.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a field is missing, the text file path escapes <paramref name="directory"/> or the text file is empty.
    /// </exception>
    /// <exception cref="FileNotFoundException">Thrown when the text file does not exist.</exception>
    private static BundleLicense ParseLicense(JsonElement element, string bundleName, string directory)
    {
        var textFile = GetString(element, "textFile", bundleName);
        var text = NormalizeText(File.ReadAllText(ResolveLicensePath(directory, textFile, bundleName)));
        if (text.Trim().Length == 0)
        {
            throw new InvalidDataException($"The license text '{textFile}' of bundle '{bundleName}' is empty.");
        }

        return new BundleLicense(
            GetString(element, "name", bundleName),
            GetString(element, "url", bundleName),
            GetString(element, "appliesTo", bundleName),
            text);
    }

    /// <summary>
    ///     Resolves a license text file path, confining it to the definitions directory so a definition can never read
    ///     an arbitrary local file into a public bundle.
    /// </summary>
    /// <param name="directory">The definitions directory the path is relative to.</param>
    /// <param name="textFile">The declared relative path.</param>
    /// <param name="bundleName">The bundle being parsed, for error messages.</param>
    /// <returns>The full path of the text file, beneath <paramref name="directory"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the path is rooted, has a <c>..</c> segment or does not resolve beneath <paramref name="directory"/>.
    /// </exception>
    private static string ResolveLicensePath(string directory, string textFile, string bundleName)
    {
        // Reject the obvious escapes outright: a rooted path replaces the base, a '..' segment climbs out of it
        if (Path.IsPathRooted(textFile) || textFile.Split(PathSeparators).Any(segment => segment == ".."))
        {
            throw new InvalidDataException(
                $"The license text '{textFile}' of bundle '{bundleName}' must be a relative path without '..' segments.");
        }

        // Canonicalize and require the result to stay beneath the definitions directory (defense in depth)
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, textFile));
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(root, comparison))
        {
            throw new InvalidDataException(
                $"The license text '{textFile}' of bundle '{bundleName}' resolves outside the definitions directory '{directory}'.");
        }

        return fullPath;
    }

    /// <summary>Gets a required, non-empty string property.</summary>
    /// <param name="element">The object to read.</param>
    /// <param name="property">The property name.</param>
    /// <param name="owner">The owner, for error messages.</param>
    /// <returns>The string value.</returns>
    /// <exception cref="InvalidDataException">Thrown when the property is missing, not a string or blank.</exception>
    private static string GetString(JsonElement element, string property, string owner)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"'{property}' of {owner} must be a non-empty string.");
        }

        return value.GetString()!;
    }

    /// <summary>Gets a required integer property.</summary>
    /// <param name="element">The object to read.</param>
    /// <param name="property">The property name.</param>
    /// <param name="owner">The owner, for error messages.</param>
    /// <returns>The integer value.</returns>
    /// <exception cref="InvalidDataException">Thrown when the property is missing or not an integer.</exception>
    private static int GetInt(JsonElement element, string property, string owner)
    {
        if (!element.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var number))
        {
            throw new InvalidDataException($"'{property}' of {owner} must be an integer.");
        }

        return number;
    }

    /// <summary>Gets a required object property.</summary>
    /// <param name="element">The object to read.</param>
    /// <param name="property">The property name.</param>
    /// <param name="owner">The owner, for error messages.</param>
    /// <returns>The object value.</returns>
    /// <exception cref="InvalidDataException">Thrown when the property is missing or not an object.</exception>
    private static JsonElement GetObject(JsonElement element, string property, string owner)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"'{property}' of {owner} must be an object.");
        }

        return value;
    }

    /// <summary>Gets a required array property.</summary>
    /// <param name="element">The object to read.</param>
    /// <param name="property">The property name.</param>
    /// <param name="owner">The owner, for error messages.</param>
    /// <returns>The array value.</returns>
    /// <exception cref="InvalidDataException">Thrown when the property is missing or not an array.</exception>
    private static JsonElement GetArray(JsonElement element, string property, string owner)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"'{property}' of {owner} must be an array.");
        }

        return value;
    }

    /// <summary>Gets a required, non-empty array of non-empty strings.</summary>
    /// <param name="element">The object to read.</param>
    /// <param name="property">The property name.</param>
    /// <param name="owner">The owner, for error messages.</param>
    /// <returns>The strings, in order.</returns>
    /// <exception cref="InvalidDataException">Thrown when the property is missing, empty or holds a non-string.</exception>
    private static List<string> GetStrings(JsonElement element, string property, string owner)
    {
        var values = GetArray(element, property, owner).EnumerateArray().ToList();
        if (values.Count == 0 || values.Any(value => value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())))
        {
            throw new InvalidDataException($"'{property}' of {owner} must be a non-empty array of non-empty strings.");
        }

        return [.. values.Select(value => value.GetString()!)];
    }

    /// <summary>
    ///     The bundle naming rule (the same rule <c>SpeechModelBundle</c> applies to the zip file name), without the
    ///     <c>.zip</c> extension.
    /// </summary>
    /// <returns>The compiled, culture-invariant, case-sensitive pattern.</returns>
    [GeneratedRegex(@"^[a-z0-9]+([.-][a-z0-9]+)*-(fp32|fp16|int8|int4)-b[1-9][0-9]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    /// <summary>
    ///     The model id rule: lowercase letters and digits separated by single <c>.</c>, <c>_</c> or <c>-</c>
    ///     characters, so an id is always one safe directory name in the source store (never <c>..</c>, a separator or
    ///     a rooted path).
    /// </summary>
    /// <returns>The compiled, culture-invariant, case-sensitive pattern.</returns>
    [GeneratedRegex(@"^[a-z0-9]+([._-][a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ModelIdPattern();
}
