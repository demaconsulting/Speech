using System.Threading;
using DemaConsulting.Speech.Onnx.OnnxRuntimeSubsystem;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DemaConsulting.Speech.Onnx.Tests.OnnxRuntimeSubsystem;

/// <summary>
///     Unit tests for <see cref="OnnxExecutionProviderSelector"/>, the provider-probing
///     <see cref="InferenceSession"/> factory shared by every model package in this repository
///     that runs bare ONNX graphs directly through ONNX Runtime.
/// </summary>
/// <remarks>
///     This package references only the base, CPU-only <c>Microsoft.ML.OnnxRuntime</c> package
///     (see <see cref="OnnxExecutionProviderSelector"/>'s own remarks), so no accelerated
///     execution provider (CUDA, DirectML, etc.) can be genuinely exercised in this test project
///     or in CI. These tests therefore use the always-available, explicitly-named
///     <c>"CPUExecutionProvider"</c> string as a stand-in "candidate" to reach the exact same
///     construct/probe/dispose/fall-back code paths a real accelerated candidate would exercise,
///     and a syntactically-invalid provider name to reach the "construction itself fails" path -
///     neither requires any accelerated hardware or native library to be present. All tests run
///     against <c>TestData/tiny-identity-model.onnx</c>, a tiny, hand-built, valid single-node
///     ONNX model generated offline with the Python <c>onnx</c>/<c>onnxruntime</c> packages
///     purely as a test fixture (never shipped in production).
/// </remarks>
public sealed class OnnxExecutionProviderSelectorTests
{
    /// <summary>The tiny, valid, single-node ONNX test fixture model shared by every test in this class.</summary>
    private static readonly string ModelPath = Path.Combine(AppContext.BaseDirectory, "TestData", "tiny-identity-model.onnx");

    /// <summary>
    ///     Proves that a null model path throws <see cref="ArgumentNullException"/> (itself an
    ///     <see cref="ArgumentException"/>) before any execution provider is attempted.
    /// </summary>
    [Fact]
    public void Create_NullModelPath_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => OnnxExecutionProviderSelector.Create(null!));
    }

    /// <summary>
    ///     Proves that an empty model path throws <see cref="ArgumentException"/> before any
    ///     execution provider is attempted.
    /// </summary>
    [Fact]
    public void Create_EmptyModelPath_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => OnnxExecutionProviderSelector.Create(string.Empty));
    }

    /// <summary>
    ///     Proves that calling <see cref="OnnxExecutionProviderSelector.Create"/> with no
    ///     preferred providers (the default) creates a working CPU-provider session for a valid
    ///     model, and that the returned session can actually run the model's single node.
    /// </summary>
    [Fact]
    public void Create_NoPreferredProviders_CreatesWorkingCpuSession()
    {
        // Arrange & Act
        using var session = OnnxExecutionProviderSelector.Create(ModelPath);

        // Assert
        Assert.NotNull(session);
        Assert.Contains("input", session.InputMetadata.Keys);
        Assert.Contains("output", session.OutputMetadata.Keys);
    }

    /// <summary>
    ///     Proves that a syntactically-unknown/unavailable execution provider name fails to
    ///     construct and <see cref="OnnxExecutionProviderSelector.Create"/> falls back to a
    ///     working CPU session rather than throwing.
    /// </summary>
    [Fact]
    public void Create_UnknownProviderName_FallsBackToCpuSession()
    {
        // Arrange
        var preferredProviderNames = new[] { "NotARealExecutionProvider" };

        // Act
        using var session = OnnxExecutionProviderSelector.Create(ModelPath, preferredProviderNames);

        // Assert
        Assert.NotNull(session);
        Assert.Contains("input", session.InputMetadata.Keys);
    }

    /// <summary>
    ///     Proves that a <c>validateSession</c> probe throwing <see cref="OnnxRuntimeException"/>
    ///     for a candidate causes that candidate to be discarded (and its session disposed) and
    ///     the final CPU fallback used instead, and that the CPU fallback itself is never passed
    ///     to the probe - exercising the exact DirectML-style "constructs fine, fails at probe"
    ///     path this mechanism exists for, using the always-available CPU provider as the stand-in
    ///     "accelerated" candidate name.
    /// </summary>
    [Fact]
    public void Create_ValidateSessionProbeThrowsOnnxRuntimeException_DisposesCandidateAndFallsBackToCpuWithoutProbingFallback()
    {
        // Arrange
        var preferredProviderNames = new[] { "CPUExecutionProvider" };
        var probeInvocationCount = 0;
        void ThrowingProbe(InferenceSession probedSession)
        {
            Interlocked.Increment(ref probeInvocationCount);

            // Feeding a wrong-typed tensor (long instead of the graph's declared float "input")
            // throws a genuine OnnxRuntimeException from ONNX Runtime's own input validation,
            // standing in for a real accelerated-provider probe failure (e.g. DirectML's
            // ConvTranspose) without needing any accelerated hardware or native library present.
            var mismatchedInput = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", new DenseTensor<long>(new long[] { 1 }, [1])),
            };
            probedSession.Run(mismatchedInput);
        }

        // Act
        using var session = OnnxExecutionProviderSelector.Create(ModelPath, preferredProviderNames, ThrowingProbe);

        // Assert: the final returned session is a working fallback, and the probe ran exactly
        // once - for the failed candidate, never for the CPU fallback itself.
        Assert.NotNull(session);
        Assert.Contains("input", session.InputMetadata.Keys);
        Assert.Equal(1, probeInvocationCount);
    }

    /// <summary>
    ///     Proves that a non-<see cref="OnnxRuntimeException"/>/<see cref="DllNotFoundException"/>
    ///     thrown by a <c>validateSession</c> probe propagates to the caller rather than being
    ///     treated as "try the next candidate".
    /// </summary>
    [Fact]
    public void Create_ValidateSessionProbeThrowsOtherException_Rethrows()
    {
        // Arrange
        var preferredProviderNames = new[] { "CPUExecutionProvider" };
        void ThrowingProbe(InferenceSession probedSession) =>
            throw new InvalidOperationException("Simulated unexpected probe failure.");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(
            () => OnnxExecutionProviderSelector.Create(ModelPath, preferredProviderNames, ThrowingProbe));
    }

    /// <summary>
    ///     Proves that every candidate's <see cref="SessionOptions"/> instance - both a failed
    ///     candidate's and the eventually winning candidate's - is disposed by the time
    ///     <see cref="OnnxExecutionProviderSelector.Create"/> returns, using the internal
    ///     <see cref="OnnxExecutionProviderSelector.OnCandidateOptionsCreated"/> test hook to
    ///     observe each instance's own public, <see cref="System.Runtime.InteropServices.SafeHandle"/>-inherited
    ///     <c>IsClosed</c> property. There is no way to observe this from outside the class
    ///     without this hook, since the real <see cref="InferenceSession"/> constructor does not
    ///     take ownership of (and therefore never itself disposes) the options instance passed to
    ///     it.
    /// </summary>
    [Fact]
    public void Create_MultipleCandidates_DisposesEveryCandidateOptionsInstance()
    {
        // Arrange
        var preferredProviderNames = new[] { "NotARealExecutionProvider", "CPUExecutionProvider" };
        var capturedOptions = new List<SessionOptions>();
        void CaptureOptions(SessionOptions options) => capturedOptions.Add(options);
        OnnxExecutionProviderSelector.OnCandidateOptionsCreated = CaptureOptions;

        try
        {
            // Act
            using var session = OnnxExecutionProviderSelector.Create(ModelPath, preferredProviderNames);

            // Assert: both the failed "NotARealExecutionProvider" candidate and the winning
            // "CPUExecutionProvider" candidate created their own options instance, and every one
            // of them is disposed - none survives as a leaked native OrtSessionOptions handle.
            Assert.NotNull(session);
            Assert.Equal(2, capturedOptions.Count);
            Assert.All(capturedOptions, options => Assert.True(options.IsClosed));
        }
        finally
        {
            OnnxExecutionProviderSelector.OnCandidateOptionsCreated = null;
        }
    }

    /// <summary>
    ///     Proves that the CPU fallback is never probed when no preferred providers are supplied
    ///     at all (an empty candidate list), confirming the probe is strictly scoped to the
    ///     caller-supplied candidate loop, never the final guaranteed-success fallback.
    /// </summary>
    [Fact]
    public void Create_EmptyPreferredProviders_NeverInvokesProbe()
    {
        // Arrange
        var probeInvocationCount = 0;
        void CountingProbe(InferenceSession probedSession) => Interlocked.Increment(ref probeInvocationCount);

        // Act
        using var session = OnnxExecutionProviderSelector.Create(ModelPath, [], CountingProbe);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(0, probeInvocationCount);
    }
}
