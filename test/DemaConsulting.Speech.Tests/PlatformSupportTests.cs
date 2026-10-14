using System.Reflection;
using System.Runtime.Versioning;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests;

/// <summary>
///     Platform and runtime evidence tests for the Speech system. Each test executes only on the
///     operating system or .NET runtime it provides evidence for and is skipped elsewhere, so the
///     requirement trace matrix can accept its result solely from a matching platform run (via the
///     <c>windows@</c>, <c>ubuntu@</c>, <c>macos@</c>, and <c>net8.0@</c>/<c>net9.0@</c>/<c>net10.0@</c>
///     source filters).
/// </summary>
public class PlatformSupportTests
{
    /// <summary>
    ///     Proves that the library assembly under test was built for, and loads on, the current
    ///     operating system and runtime: its target framework is declared and its types resolve.
    /// </summary>
    /// <returns>The target framework name the library assembly was built for.</returns>
    private static string AssertLibraryBuiltAndLoaded()
    {
        // Act
        var assembly = typeof(AudioTagParser).Assembly;
        var framework = assembly.GetCustomAttribute<TargetFrameworkAttribute>();

        // Assert
        Assert.NotNull(framework);
        Assert.True(File.Exists(assembly.Location));
        return framework.FrameworkName;
    }

    /// <summary>
    ///     Proves that the library performs representative work on the current platform: it parses
    ///     audio tags, resamples audio, and probes the native audio stack without throwing.
    /// </summary>
    private static void AssertLibraryRuns()
    {
        // Act: parse tagged text, resample one second of audio, and compose the default audio devices
        var spans = AudioTagParser.Parse("Hello [laughs] world.");
        var resampled = AudioResampler.Resample(new float[48000], 48000, 16000);
        var factory = new AudioDeviceFactory();
        var exception = Record.Exception(() =>
        {
            _ = factory.CreateCaptureDevice();
            _ = factory.CreatePlaybackDevice();
        });

        // Assert
        Assert.Equal(3, spans.Count);
        Assert.Equal(16000, resampled.Length);
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that the library builds and loads on Windows.
    /// </summary>
    [Fact]
    public void PlatformSupport_Build_Windows_LibraryBuiltAndLoaded()
    {
        // Arrange
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Evidence applies to Windows only.");

        // Act & Assert
        Assert.NotEmpty(AssertLibraryBuiltAndLoaded());
    }

    /// <summary>
    ///     Proves that the library runs on Windows.
    /// </summary>
    [Fact]
    public void PlatformSupport_Run_Windows_LibraryRuns()
    {
        // Arrange
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Evidence applies to Windows only.");

        // Act & Assert
        AssertLibraryRuns();
    }

    /// <summary>
    ///     Proves that the library builds and loads on Linux.
    /// </summary>
    [Fact]
    public void PlatformSupport_Build_Linux_LibraryBuiltAndLoaded()
    {
        // Arrange
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Evidence applies to Linux only.");

        // Act & Assert
        Assert.NotEmpty(AssertLibraryBuiltAndLoaded());
    }

    /// <summary>
    ///     Proves that the library runs on Linux.
    /// </summary>
    [Fact]
    public void PlatformSupport_Run_Linux_LibraryRuns()
    {
        // Arrange
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Evidence applies to Linux only.");

        // Act & Assert
        AssertLibraryRuns();
    }

    /// <summary>
    ///     Proves that the library builds and loads on macOS.
    /// </summary>
    [Fact]
    public void PlatformSupport_Build_MacOS_LibraryBuiltAndLoaded()
    {
        // Arrange
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "Evidence applies to macOS only.");

        // Act & Assert
        Assert.NotEmpty(AssertLibraryBuiltAndLoaded());
    }

    /// <summary>
    ///     Proves that the library runs on macOS.
    /// </summary>
    [Fact]
    public void PlatformSupport_Run_MacOS_LibraryRuns()
    {
        // Arrange
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "Evidence applies to macOS only.");

        // Act & Assert
        AssertLibraryRuns();
    }

    /// <summary>
    ///     Proves that the library is built for .NET 8.
    /// </summary>
    [Fact]
    public void PlatformSupport_Build_Net8_LibraryBuiltForNet8()
    {
        // Arrange
        Assert.SkipUnless(Environment.Version.Major == 8, "Evidence applies to the .NET 8 runtime only.");

        // Act
        var framework = AssertLibraryBuiltAndLoaded();

        // Assert
        Assert.Equal(".NETCoreApp,Version=v8.0", framework);
    }

    /// <summary>
    ///     Proves that the library runs on the .NET 8 runtime.
    /// </summary>
    [Fact]
    public void PlatformSupport_Run_Net8_LibraryRunsOnNet8()
    {
        // Arrange
        Assert.SkipUnless(Environment.Version.Major == 8, "Evidence applies to the .NET 8 runtime only.");

        // Act & Assert
        AssertLibraryRuns();
    }

    /// <summary>
    ///     Proves that the library is built for .NET 9.
    /// </summary>
    [Fact]
    public void PlatformSupport_Build_Net9_LibraryBuiltForNet9()
    {
        // Arrange
        Assert.SkipUnless(Environment.Version.Major == 9, "Evidence applies to the .NET 9 runtime only.");

        // Act
        var framework = AssertLibraryBuiltAndLoaded();

        // Assert
        Assert.Equal(".NETCoreApp,Version=v9.0", framework);
    }

    /// <summary>
    ///     Proves that the library runs on the .NET 9 runtime.
    /// </summary>
    [Fact]
    public void PlatformSupport_Run_Net9_LibraryRunsOnNet9()
    {
        // Arrange
        Assert.SkipUnless(Environment.Version.Major == 9, "Evidence applies to the .NET 9 runtime only.");

        // Act & Assert
        AssertLibraryRuns();
    }

    /// <summary>
    ///     Proves that the library is built for .NET 10.
    /// </summary>
    [Fact]
    public void PlatformSupport_Build_Net10_LibraryBuiltForNet10()
    {
        // Arrange
        Assert.SkipUnless(Environment.Version.Major == 10, "Evidence applies to the .NET 10 runtime only.");

        // Act
        var framework = AssertLibraryBuiltAndLoaded();

        // Assert
        Assert.Equal(".NETCoreApp,Version=v10.0", framework);
    }

    /// <summary>
    ///     Proves that the library runs on the .NET 10 runtime.
    /// </summary>
    [Fact]
    public void PlatformSupport_Run_Net10_LibraryRunsOnNet10()
    {
        // Arrange
        Assert.SkipUnless(Environment.Version.Major == 10, "Evidence applies to the .NET 10 runtime only.");

        // Act & Assert
        AssertLibraryRuns();
    }
}
