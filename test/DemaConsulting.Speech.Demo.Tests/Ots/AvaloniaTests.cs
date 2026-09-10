using Avalonia;

namespace DemaConsulting.Speech.Demo.Tests.Ots;

/// <summary>
///     Integration tests for the Avalonia OTS software item.
/// </summary>
/// <remarks>
///     These tests prove only what a headless CI runner can honestly prove: that the Avalonia
///     packages are present and that the demo's application host can be configured through them.
///     They deliberately never start a windowing lifetime, because CI runners have no display and
///     starting one would prove nothing about the demo the ViewModel tests do not already prove.
/// </remarks>
public class AvaloniaTests
{
    /// <summary>
    ///     Proves that the demo's Avalonia application host configures successfully against the
    ///     referenced Avalonia packages, including the platform-detect backend and the bundled
    ///     Inter font package.
    /// </summary>
    [Fact]
    public void Avalonia_BuildAvaloniaApp_Invoked_ReturnsConfiguredApplicationBuilder()
    {
        // Act: configure the application host exactly as the process entry point does
        var builder = Program.BuildAvaloniaApp();

        // Assert: Avalonia produced a builder bound to the demo's own application type
        Assert.NotNull(builder);
        Assert.Equal(typeof(App), builder.ApplicationType);
    }

    /// <summary>
    ///     Proves that the demo's application type is a genuine Avalonia application, which is what
    ///     lets Avalonia own the demo's styling, resources, and desktop lifetime.
    /// </summary>
    [Fact]
    public void Avalonia_DemoApplication_Inspected_DerivesFromAvaloniaApplication()
    {
        // Act: create the demo application object without starting any lifetime
        var application = new App();

        // Assert: it is an Avalonia application Avalonia can host
        Assert.IsType<Application>(application, exactMatch: false);
    }
}
