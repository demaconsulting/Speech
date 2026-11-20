using DemaConsulting.Speech.Proofreader.Session;

namespace DemaConsulting.Speech.Proofreader.Tests;

/// <summary>
///     Tests for the per-user settings file: loading damaged files, and the application composition supplying it.
/// </summary>
public class UserSettingsTests
{
    /// <summary>
    ///     Validates that the application's composition root remembers settings: composing and disposing
    ///     the view model the way the app does writes the settings file, and a second composition reads it.
    /// </summary>
    [Fact]
    public void App_Compose_SuppliesSettingsStore_SoSettingsPersist()
    {
        var directory = Path.Combine(Path.GetTempPath(), "proofreader-compose-" + Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(directory, "settings.json");
        var options = new LaunchOptions(Path.Combine(directory, "models"), null, null, false);
        try
        {
            // Act: start the app's view model, change something, and close it
            string chosen;
            using (var first = App.Compose(options, settingsPath))
            {
                first.SlotA.SelectedModel = first.SlotA.Models[^1];
                chosen = first.SlotA.SelectedModel.Descriptor.Id;
                first.AbMode = true;
            }

            // Assert: closing wrote the file, and the next start picks it up
            Assert.True(File.Exists(settingsPath));
            using var second = App.Compose(options, settingsPath);
            Assert.Equal(chosen, second.SlotA.SelectedModel!.Descriptor.Id);
            Assert.True(second.AbMode);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Validates that the default settings file lives under the per-user application data folder.
    /// </summary>
    [Fact]
    public void UserSettingsStore_DefaultPath_IsUnderLocalApplicationData()
    {
        // Act
        var path = UserSettingsStore.DefaultPath();

        // Assert
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), path, StringComparison.Ordinal);
        Assert.EndsWith("settings.json", path, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that valid JSON with null or wrongly typed members loads without throwing and without
    ///     keeping anything unusable.
    /// </summary>
    /// <param name="json">The settings file content.</param>
    /// <param name="slotAKept">Whether slot A is expected to survive loading.</param>
    [Theory]
    [InlineData("{ \"slotA\": { \"modelId\": \"m\", \"parameters\": null } }", true)]
    [InlineData("{ \"slotA\": { \"modelId\": \"m\", \"displayName\": null, \"parameters\": { \"speed\": null, \"voice\": \"x\", \"n\": 2, \"on\": true, \"o\": {} } } }", true)]
    [InlineData("{ \"slotA\": null, \"slotB\": null, \"abMode\": null, \"lastCategory\": null }", false)]
    [InlineData("{ \"slotA\": { \"modelId\": null, \"parameters\": {} } }", false)]
    [InlineData("{ \"slotA\": { \"modelId\": \"\" } }", false)]
    [InlineData("{ \"slotA\": \"not an object\" }", false)]
    [InlineData("{ \"slotA\": { \"modelId\": 5 } }", false)]
    [InlineData("{ \"abMode\": \"yes\", \"slotA\": { \"modelId\": \"m\" } }", false)]
    [InlineData("{ \"lastCategory\": \"no-such-category\" }", false)]
    [InlineData("{ \"lastCategory\": 99 }", false)]
    [InlineData("[1, 2, 3]", false)]
    [InlineData("null", false)]
    [InlineData("", false)]
    public void UserSettingsStore_Load_UnusableContent_YieldsUsableSettings(string json, bool slotAKept)
    {
        var path = Path.Combine(Path.GetTempPath(), "proofreader-settings-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        try
        {
            // Act
            var settings = new UserSettingsStore(path).Load();

            // Assert: nothing throws; a kept slot always has an id and parameters; a bad category is dropped
            Assert.Equal(slotAKept, settings.SlotA is not null);
            if (settings.SlotA is not null)
            {
                Assert.NotEmpty(settings.SlotA.ModelId);
                Assert.NotNull(settings.SlotA.Parameters);
            }

            Assert.Null(settings.LastCategory);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that a good settings file round-trips, including typed parameter values and the category.
    /// </summary>
    [Fact]
    public void UserSettingsStore_SaveThenLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), "proofreader-settings-" + Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            var store = new UserSettingsStore(path);

            // Act
            store.Save(new UserSettings
            {
                SlotA = new SessionModel { ModelId = "m", Parameters = new Dictionary<string, object> { ["speed"] = 1.25, ["voice"] = "af", ["on"] = true } },
                AbMode = true,
                LastCategory = FlagCategory.VoiceGlitch,
            });
            var loaded = store.Load();

            // Assert
            Assert.Equal("m", loaded.SlotA!.ModelId);
            Assert.Equal(1.25, Assert.IsType<double>(loaded.SlotA.Parameters["speed"]));
            Assert.Equal("af", loaded.SlotA.Parameters["voice"]);
            Assert.True(Assert.IsType<bool>(loaded.SlotA.Parameters["on"]));
            Assert.True(loaded.AbMode);
            Assert.Equal(FlagCategory.VoiceGlitch, loaded.LastCategory);
        }
        finally
        {
            if (Path.GetDirectoryName(path) is { } directory && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
