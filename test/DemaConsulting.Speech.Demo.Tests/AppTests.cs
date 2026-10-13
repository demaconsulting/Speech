// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace DemaConsulting.Speech.Demo.Tests;

/// <summary>
///     Unit tests for <see cref="App"/>'s composition and teardown seams, exercised without a
///     running desktop lifetime.
/// </summary>
public sealed class AppTests
{
    /// <summary>
    ///     Builds launch options whose model store is rooted in a fresh directory under the test
    ///     output folder, so no test touches a developer's real installed-model store.
    /// </summary>
    /// <returns>Launch options with an isolated models directory.</returns>
    private static AppLaunchOptions IsolatedLaunchOptions() => AppLaunchOptions.Parse(
        ["--models-dir", Path.Join(AppContext.BaseDirectory, "AppTests", Guid.NewGuid().ToString("N"))],
        _ => null);

    /// <summary>
    ///     Test that <c>App.Compose</c> produces the shell with all four panels, opening on the first.
    /// </summary>
    [Fact]
    public void App_Compose_IsolatedLaunchOptions_ComposesShellWithAllPanels()
    {
        // Act
        var viewModel = App.Compose(IsolatedLaunchOptions(), out var catalogService);
        using var disposable = catalogService;

        // Assert
        Assert.Equal(4, viewModel.Panels.Count);
        Assert.Same(viewModel.Panels[0], viewModel.SelectedPanel);
        Assert.Same(viewModel.DeviceSelection, viewModel.Panels[0].Content);
        Assert.Same(viewModel.ModelCatalog, viewModel.Panels[1].Content);
        Assert.Same(viewModel.Synthesis, viewModel.Panels[2].Content);
        Assert.Same(viewModel.Recognition, viewModel.Panels[3].Content);
    }

    /// <summary>
    ///     Test that the composed catalog registers every model family the demo offers, through the
    ///     same service the synthesis and recognition panels share.
    /// </summary>
    [Fact]
    public void App_Compose_IsolatedLaunchOptions_CatalogListsEveryRegisteredModel()
    {
        // Act
        var viewModel = App.Compose(IsolatedLaunchOptions(), out var catalogService);
        using var disposable = catalogService;

        // Assert: three recognition and four synthesis models
        Assert.Equal(7, viewModel.ModelCatalog.Models.Count);
        Assert.False(viewModel.ModelCatalog.IsCatalogEmpty);
        Assert.Equal(7, catalogService.Enumerate().Count);
    }

    /// <summary>
    ///     Test that composing with no launch options (the design-time case) still succeeds.
    /// </summary>
    [Fact]
    public void App_Compose_NullLaunchOptions_ComposesShell()
    {
        // Act
        var exception = Record.Exception(() =>
        {
            var viewModel = App.Compose(null, out var catalogService);
            catalogService.Dispose();
            Assert.Equal(4, viewModel.Panels.Count);
        });

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Test that teardown runs every step in order when none fails.
    /// </summary>
    [Fact]
    public async Task App_RunTeardownAsync_AllStepsSucceed_RunsEveryStepInOrder()
    {
        // Arrange
        var order = new List<int>();

        // Act
        await App.RunTeardownAsync(
            () => { order.Add(1); return ValueTask.CompletedTask; },
            () => { order.Add(2); return ValueTask.CompletedTask; },
            () => { order.Add(3); return ValueTask.CompletedTask; });

        // Assert
        Assert.Equal([1, 2, 3], order);
    }

    /// <summary>
    ///     Test that a step that throws synchronously, and one that faults asynchronously, are each
    ///     contained so every later step still runs.
    /// </summary>
    [Fact]
    public async Task App_RunTeardownAsync_StepsFail_ContainsEachFailureAndRunsRemainingSteps()
    {
        // Arrange
        var order = new List<int>();

        // Act
        var exception = await Record.ExceptionAsync(() => App.RunTeardownAsync(
            () => { order.Add(1); throw new InvalidOperationException("first"); },
            async () => { order.Add(2); await Task.Yield(); throw new InvalidOperationException("second"); },
            () => { order.Add(3); return ValueTask.CompletedTask; }));

        // Assert
        Assert.Null(exception);
        Assert.Equal([1, 2, 3], order);
    }
}
