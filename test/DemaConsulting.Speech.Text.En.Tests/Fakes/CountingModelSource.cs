using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.Fakes;

/// <summary>
///     Serves the bytes of a heteronym model to a <see cref="HeteronymModelProvider"/> and lets tests
///     inject missing, failing and corrupt sources. (The class name is historical: it once counted
///     the opens, which no test asserts any more.)
/// </summary>
internal sealed class CountingModelSource
{
    /// <summary>The bytes to serve, or null for "no model".</summary>
    private readonly byte[]? _bytes;

    /// <summary>The exception to throw on open instead of serving, or null.</summary>
    private readonly Exception? _failure;

    /// <summary>
    ///     Initializes a new instance that serves a model.
    /// </summary>
    /// <param name="bytes">The model bytes, or <see langword="null"/> to report that no model exists.</param>
    /// <param name="failure">An exception thrown by every open, or <see langword="null"/>.</param>
    internal CountingModelSource(byte[]? bytes, Exception? failure = null)
    {
        _bytes = bytes;
        _failure = failure;
    }

    /// <summary>
    ///     Opens the source: throws, returns null or returns a fresh read-only stream.
    /// </summary>
    /// <returns>A new stream over the bytes, or null when there is no model.</returns>
    internal Stream? Open()
    {
        if (_failure is not null)
        {
            throw _failure;
        }

        return _bytes is null ? null : new MemoryStream(_bytes, writable: false);
    }
}
