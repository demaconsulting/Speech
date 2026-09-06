using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.Demo.ShellSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.ShellSubsystem;

/// <summary>
///     Unit tests for <see cref="DemoPanelViewModel"/>.
/// </summary>
public class DemoPanelViewModelTests
{
    /// <summary>
    ///     Proves that a navigation entry carries the caption and panel it was composed with.
    /// </summary>
    [Fact]
    public void DemoPanelViewModel_Constructor_ValidArguments_CarriesTitleAndContent()
    {
        // Arrange: a panel ViewModel to host
        var content = new StubPanel();

        // Act: build the navigation entry
        var entry = new DemoPanelViewModel("Audio Devices", content);

        // Assert: both values are exposed to the shell unchanged
        Assert.Equal("Audio Devices", entry.Title);
        Assert.Same(content, entry.Content);
    }

    /// <summary>
    ///     Proves that a blank caption is rejected, since a tab with no readable caption is not
    ///     navigable.
    /// </summary>
    /// <param name="title">The unusable caption under test.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DemoPanelViewModel_Constructor_BlankTitle_ThrowsArgumentException(string title)
    {
        // Act & Assert: an unnamed navigation entry is a programming error
        Assert.Throws<ArgumentException>(() => new DemoPanelViewModel(title, new StubPanel()));
    }

    /// <summary>
    ///     Proves that a missing caption is rejected.
    /// </summary>
    [Fact]
    public void DemoPanelViewModel_Constructor_NullTitle_ThrowsArgumentNullException()
    {
        // Act & Assert: a null caption is a programming error
        Assert.Throws<ArgumentNullException>(() => new DemoPanelViewModel(null!, new StubPanel()));
    }

    /// <summary>
    ///     Proves that a missing panel is rejected, since an entry with no content would render a
    ///     blank page.
    /// </summary>
    [Fact]
    public void DemoPanelViewModel_Constructor_NullContent_ThrowsArgumentNullException()
    {
        // Act & Assert: an entry with no panel is a programming error
        Assert.Throws<ArgumentNullException>(() => new DemoPanelViewModel("Panel", null!));
    }

    /// <summary>
    ///     Minimal observable object standing in for a real panel ViewModel.
    /// </summary>
    private sealed class StubPanel : ObservableObject;
}
