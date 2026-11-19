using System.Text;
using System.Text.Json;

namespace DemaConsulting.Speech.Proofreader.Session;

/// <summary>
///     What the Proofreader remembers between launches: the models and their settings, A/B mode,
///     and the category last used for a flag.
/// </summary>
internal sealed class UserSettings
{
    /// <summary>Gets or sets the model and settings of slot A, or <see langword="null"/> when none was used yet.</summary>
    public SessionModel? SlotA { get; set; }

    /// <summary>Gets or sets the model and settings of slot B, or <see langword="null"/> when none was used yet.</summary>
    public SessionModel? SlotB { get; set; }

    /// <summary>Gets or sets a value indicating whether A/B mode was on.</summary>
    public bool AbMode { get; set; }

    /// <summary>Gets or sets the category of the last flag that was saved, or <see langword="null"/> when none was.</summary>
    public FlagCategory? LastCategory { get; set; }
}

/// <summary>
///     Reads and writes the per-user settings file.
/// </summary>
/// <remarks>
///     A small JSON file (the session file's conventions) under the per-user application data folder.
///     Settings are a convenience, never a failure: a missing, unreadable, or damaged file yields
///     defaults, and a file that cannot be written is silently skipped.
/// </remarks>
internal sealed class UserSettingsStore
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="UserSettingsStore"/> class.
    /// </summary>
    /// <param name="path">The settings file path. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    public UserSettingsStore(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        Path = path;
    }

    /// <summary>Gets the settings file path.</summary>
    public string Path { get; }

    /// <summary>
    ///     Gets the default per-user settings file path.
    /// </summary>
    /// <returns>A path under the local application data folder.</returns>
    public static string DefaultPath() =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DemaConsulting",
            "Speech.Proofreader",
            "settings.json");

    /// <summary>
    ///     Reads the settings.
    /// </summary>
    /// <returns>The saved settings, or defaults when there are none or the file is unusable.</returns>
    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return new UserSettings();
            }

            var settings = SessionFile.DeserializeValue<UserSettings>(File.ReadAllText(Path, Encoding.UTF8)) ?? new UserSettings();
            SessionFile.NormalizeParameters(settings.SlotA);
            SessionFile.NormalizeParameters(settings.SlotB);
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new UserSettings();
        }
    }

    /// <summary>
    ///     Writes the settings; a failure to write is ignored.
    /// </summary>
    /// <param name="settings">The settings to remember.</param>
    public void Save(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            if (System.IO.Path.GetDirectoryName(Path) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(Path, SessionFile.SerializeValue(settings), new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Remembering settings is a convenience; losing it must never interrupt proofreading.
        }
    }
}
