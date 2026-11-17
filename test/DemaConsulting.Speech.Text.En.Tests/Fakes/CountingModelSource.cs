using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.Fakes;

/// <summary>
///     Serves the bytes of a heteronym model to a <see cref="HeteronymModelProvider"/> and counts how many
///     times it is opened, so tests can prove the model is loaded lazily and at most once, and can inject
///     missing, failing and corrupt sources.
/// </summary>
internal sealed class CountingModelSource
{
    /// <summary>The bytes to serve, or null for "no model".</summary>
    private readonly byte[]? _bytes;

    /// <summary>The exception to throw on open instead of serving, or null.</summary>
    private readonly Exception? _failure;

    /// <summary>The number of opens so far.</summary>
    private int _count;

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

    /// <summary>Gets how many times the source has been opened.</summary>
    internal int Count => Volatile.Read(ref _count);

    /// <summary>
    ///     Opens the source: counts, then throws, returns null or returns a fresh read-only stream.
    /// </summary>
    /// <returns>A new stream over the bytes, or null when there is no model.</returns>
    internal Stream? Open()
    {
        Interlocked.Increment(ref _count);
        if (_failure is not null)
        {
            throw _failure;
        }

        return _bytes is null ? null : new MemoryStream(_bytes, writable: false);
    }
}
