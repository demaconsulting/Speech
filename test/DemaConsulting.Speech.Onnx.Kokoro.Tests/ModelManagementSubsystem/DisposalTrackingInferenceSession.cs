using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.Kokoro.Tests.ModelManagementSubsystem;

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
    ///     Initializes a new instance of the <see cref="DisposalTrackingInferenceSession"/> class over the model at
    ///     <paramref name="modelPath"/>.
    /// </summary>
    /// <param name="modelPath">The path of the <c>.onnx</c> model file to load.</param>
    public DisposalTrackingInferenceSession(string modelPath)
        : base(modelPath)
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
