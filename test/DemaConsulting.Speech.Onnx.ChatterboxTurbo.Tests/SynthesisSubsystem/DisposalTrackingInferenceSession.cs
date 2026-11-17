using DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;
using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.SynthesisSubsystem;

/// <summary>
///     An <see cref="InferenceSession"/> that records its own disposal through the public
///     <c>Dispose(bool)</c> override point, so a test can observe that an owner released the session
///     without reading ONNX Runtime's private state or touching a disposed native session.
/// </summary>
internal sealed class DisposalTrackingInferenceSession : InferenceSession
{
    /// <summary>Non-zero once <see cref="Dispose(bool)"/> has been called with <c>disposing</c> true.</summary>
    private int _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DisposalTrackingInferenceSession"/> class.
    /// </summary>
    /// <param name="modelPath">The path of the <c>.onnx</c> model file to load.</param>
    /// <param name="options">The session options.</param>
    public DisposalTrackingInferenceSession(string modelPath, SessionOptions options)
        : base(modelPath, options)
    {
    }

    /// <summary>Gets a value indicating whether the session has been disposed.</summary>
    public bool WasDisposed => Volatile.Read(ref _disposed) != 0;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Volatile.Write(ref _disposed, 1);
        }

        base.Dispose(disposing);
    }
}

/// <summary>
///     Installs <see cref="OnnxChatterboxTurboNetworks.SessionFactory"/> so every session the networks create is a
///     <see cref="DisposalTrackingInferenceSession"/>, and restores the default on dispose.
/// </summary>
internal sealed class DisposalTrackingSessionScope : IDisposable
{
    /// <summary>Initializes a new scope, installing the tracking factory.</summary>
    public DisposalTrackingSessionScope() =>
        OnnxChatterboxTurboNetworks.SessionFactory = (path, options) =>
        {
            var session = new DisposalTrackingInferenceSession(path, options);
            lock (Sessions)
            {
                Sessions.Add(session);
            }

            return session;
        };

    /// <summary>Gets every session created while this scope was installed, in creation order.</summary>
    public List<DisposalTrackingInferenceSession> Sessions { get; } = [];

    /// <inheritdoc/>
    public void Dispose() => OnnxChatterboxTurboNetworks.SessionFactory = null;
}
