using System.Xml.Linq;

namespace DemaConsulting.Speech.Demo.Tests.ModelSettingsSubsystem;

/// <summary>
///     Structural tests for the model settings view's control selection.
/// </summary>
/// <remarks>
///     The demo has no headless UI host, so these tests read the view's markup and check the
///     bindings that decide which control a numeric parameter gets. They prove the control
///     selection stated by the subsystem requirement; they do not render the view.
/// </remarks>
public class ModelSettingsViewTests
{
    /// <summary>The Avalonia markup namespace used by the view.</summary>
    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";

    /// <summary>The path of the view markup relative to the repository root.</summary>
    private static readonly string[] ViewPath =
        ["src", "DemaConsulting.Speech.Demo", "ModelSettingsSubsystem", "ModelSettingsView.axaml"];

    /// <summary>
    ///     Locates the view markup by walking up from the test output folder to the repository root.
    /// </summary>
    /// <returns>The markup document.</returns>
    private static XDocument LoadView()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine([directory.FullName, .. ViewPath]);
            if (File.Exists(candidate))
            {
                return XDocument.Load(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("ModelSettingsView.axaml was not found above the test output folder.");
    }

    /// <summary>
    ///     Proves that the numeric parameter template shows a continuous slider only for a
    ///     parameter that is not whole-number, and a whole-number-only control for one that is.
    /// </summary>
    [Fact]
    public void ModelSettingsView_NumericTemplate_IntegerParameter_ShowsWholeNumberControlInsteadOfSlider()
    {
        // Arrange: the numeric parameter data template from the view markup
        var template = LoadView()
            .Descendants(Avalonia + "DataTemplate")
            .Single(t => ((string?)t.Attribute("DataType"))?.EndsWith(":NumericParameterViewModel", StringComparison.Ordinal) == true);

        // Act: find the controls offered for the numeric value
        var slider = Assert.Single(template.Descendants(Avalonia + "Slider"));
        var upDown = Assert.Single(template.Descendants(Avalonia + "NumericUpDown"));

        // Assert: the slider is visible only when the parameter is not whole-number, the
        // up-down only when it is, and the up-down formats and steps in whole numbers
        Assert.Equal("{Binding !IsInteger}", (string?)slider.Attribute("IsVisible"));
        Assert.Equal("{Binding IsInteger}", (string?)upDown.Attribute("IsVisible"));
        Assert.Equal("0", (string?)upDown.Attribute("FormatString"));
        Assert.Equal("{Binding Value}", (string?)upDown.Attribute("Value"));
        Assert.Equal("{Binding Minimum}", (string?)upDown.Attribute("Minimum"));
        Assert.Equal("{Binding Maximum}", (string?)upDown.Attribute("Maximum"));
    }
}
