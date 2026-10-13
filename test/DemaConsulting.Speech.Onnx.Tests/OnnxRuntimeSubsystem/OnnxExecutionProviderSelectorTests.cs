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
///     purely as a test fixture (never shipped in production). The class belongs to the
///     serialized <see cref="OnnxSelectorHookCollection"/> because the selector's test hook is
///     process-wide static state.
/// </remarks>
[Collection(OnnxSelectorHookCollection.Name)]
public sealed class OnnxExecutionProviderSelectorTests
{
    /// <summary>The tiny, valid, single-node ONNX test fixture model shared by every test in this class.</summary>
    private static readonly string ModelPath = Path.Combine(AppContext.BaseDirectory, "TestData", "tiny-identity-model.onnx");

    /// <summary>
    ///     Proves that a null model path throws <see cref="ArgumentNullException"/> (itself an
    ///     <see cref="ArgumentException"/>) naming the parameter, before any execution provider
    ///     is attempted.
    /// </summary>
    [Fact]
    public void OnnxExecutionProviderSelector_Create_NullModelPath_ThrowsArgumentNullException()
    {
        // Arrange
        var attemptedCandidates = 0;
        using var scope = SelectorHookScope.Install(_ => attemptedCandidates++);

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => OnnxExecutionProviderSelector.Create(null!, ["CPUExecutionProvider"]));

        // Assert
        Assert.Equal("modelPath", exception.ParamName);
        Assert.Equal(0, attemptedCandidates);
    }

    /// <summary>
    ///     Proves that an empty model path throws <see cref="ArgumentException"/> naming the
    ///     parameter before any execution provider is attempted.
    /// </summary>
    [Fact]
    public void OnnxExecutionProviderSelector_Create_EmptyModelPath_ThrowsArgumentException()
    {
        // Arrange
        var attemptedCandidates = 0;
        using var scope = SelectorHookScope.Install(_ => attemptedCandidates++);

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => OnnxExecutionProviderSelector.Create(string.Empty, ["CPUExecutionProvider"]));

        // Assert
        Assert.Equal("modelPath", exception.ParamName);
        Assert.Equal(0, attemptedCandidates);
    }

    /// <summary>
    ///     Proves that calling <see cref="OnnxExecutionProviderSelector.Create"/> with no
    ///     preferred providers (the default) creates a working CPU-provider session for a valid
    ///     model, and that the returned session can actually run the model's single node.
    /// </summary>
    [Fact]
    public void OnnxExecutionProviderSelector_Create_NoPreferredProviders_CreatesWorkingCpuSession()
    {
        // Arrange
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", new DenseTensor<float>(new[] { 2.5f }, [1])),
        };

        // Act
        using var session = OnnxExecutionProviderSelector.Create(ModelPath);
        using var results = session.Run(inputs);

        // Assert
        Assert.NotNull(session);
        Assert.Contains("input", session.InputMetadata.Keys);
        Assert.Contains("output", session.OutputMetadata.Keys);
        Assert.Equal(new[] { 2.5f }, results.Single().AsTensor<float>().ToArray());
    }

    /// <summary>
    ///     Proves that a syntactically-unknown/unavailable execution provider name fails to
    ///     construct and <see cref="OnnxExecutionProviderSelector.Create"/> falls back to a
    ///     working CPU session rather than throwing.
    /// </summary>
    [Fact]
    public void OnnxExecutionProviderSelector_Create_UnknownProviderName_FallsBackToCpuSession()
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
    public void OnnxExecutionProviderSelector_Create_ValidateSessionProbeThrowsOnnxRuntimeException_DisposesCandidateAndFallsBackToCpuWithoutProbingFallback()
    {
        // Arrange
        var preferredProviderNames = new[] { "CPUExecutionProvider" };
        var probeInvocationCount = 0;
        InferenceSession? rejectedCandidate = null;
        void ThrowingProbe(InferenceSession probedSession)
        {
            Interlocked.Increment(ref probeInvocationCount);
            rejectedCandidate = probedSession;
            RunWithMismatchedInput(probedSession);
        }

        // Act
        using var session = OnnxExecutionProviderSelector.Create(ModelPath, preferredProviderNames, ThrowingProbe);

        // Assert: the final returned session is a distinct, working fallback; the probe ran
        // exactly once (for the failed candidate, never for the CPU fallback); and the rejected
        // candidate's session was disposed.
        Assert.NotNull(session);
        Assert.Contains("input", session.InputMetadata.Keys);
        Assert.Equal(1, probeInvocationCount);
        Assert.NotNull(rejectedCandidate);
        Assert.NotSame(rejectedCandidate, session);
        AssertSessionIsDisposed(rejectedCandidate);
    }

    /// <summary>
    ///     Proves that a <c>validateSession</c> probe throwing <see cref="DllNotFoundException"/>
    ///     (how some provider native libraries report a missing dependency) is treated as an
    ///     unavailable provider: the candidate is abandoned and a working CPU session returned.
    /// </summary>
    [Fact]
    public void OnnxExecutionProviderSelector_Create_ValidateSessionProbeThrowsDllNotFoundException_FallsBackToCpuSession()
    {
        // Arrange
        var preferredProviderNames = new[] { "CPUExecutionProvider" };
        void ThrowingProbe(InferenceSession probedSession) =>
            throw new DllNotFoundException("Simulated missing provider native library.");

        // Act
        using var session = OnnxExecutionProviderSelector.Create(ModelPath, preferredProviderNames, ThrowingProbe);

        // Assert
        Assert.NotNull(session);
        Assert.Contains("input", session.InputMetadata.Keys);
    }

    /// <summary>
    ///     Proves that a non-<see cref="OnnxRuntimeException"/>/<see cref="DllNotFoundException"/>
    ///     thrown by a <c>validateSession</c> probe propagates to the caller unchanged rather than
    ///     being treated as "try the next candidate", and that the probed candidate's session is
    ///     disposed before the exception reaches the caller.
    /// </summary>
    [Fact]
    public void OnnxExecutionProviderSelector_Create_ValidateSessionProbeThrowsOtherException_RethrowsAndDisposesCandidateSession()
    {
        // Arrange
        var preferredProviderNames = new[] { "CPUExecutionProvider", "CPUExecutionProvider" };
        var probeInvocationCount = 0;
        InferenceSession? probedCandidate = null;
        var unexpected = new InvalidOperationException("Simulated unexpected probe failure.");
        void ThrowingProbe(InferenceSession probedSession)
        {
            Interlocked.Increment(ref probeInvocationCount);
            probedCandidate = probedSession;
            throw unexpected;
        }

        // Act
        var thrown = Assert.Throws<InvalidOperationException>(
            () => OnnxExecutionProviderSelector.Create(ModelPath, preferredProviderNames, ThrowingProbe));

        // Assert: the very same exception reached the caller, no later candidate was tried, and
        // the candidate's session was disposed rather than leaked.
        Assert.Same(unexpected, thrown);
        Assert.Equal(1, probeInvocationCount);
        Assert.NotNull(probedCandidate);
        AssertSessionIsDisposed(probedCandidate);
    }

    /// <summary>
    ///     Proves that a model file that does not exist is reported to the caller as an ONNX
    ///     Runtime error from the final CPU attempt, exactly as a direct
    ///     <c>new InferenceSession(modelPath)</c> call would, rather than being swallowed.
    /// </summary>
    [Fact]
    public void OnnxExecutionProviderSelector_Create_MissingModelFile_PropagatesOnnxRuntimeException()
    {
        // Arrange
        var missingPath = Path.Combine(AppContext.BaseDirectory, "TestData", "does-not-exist.onnx");

        // Act & Assert
        Assert.Throws<OnnxRuntimeException>(() => OnnxExecutionProviderSelector.Create(missingPath));
    }

    /// <summary>
    ///     Proves that a malformed model file is reported to the caller even when a preferred
    ///     provider was supplied: the candidate's failure only means "try the next candidate",
    ///     but the final CPU attempt still reports the genuine model error.
    /// </summary>
    [Fact]
    public void OnnxExecutionProviderSelector_Create_MalformedModelFileWithPreferredProvider_PropagatesOnnxRuntimeException()
    {
        // Arrange
        var malformedPath = Path.Combine(Path.GetTempPath(), $"malformed-{Guid.NewGuid():N}.onnx");
        File.WriteAllText(malformedPath, "this is not an ONNX model");

        try
        {
            // Act & Assert
            Assert.Throws<OnnxRuntimeException>(
                () => OnnxExecutionProviderSelector.Create(malformedPath, ["CPUExecutionProvider"]));
        }
        finally
        {
            File.Delete(malformedPath);
        }
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
    public void OnnxExecutionProviderSelector_Create_MultipleCandidates_DisposesEveryCandidateOptionsInstance()
    {
        // Arrange
        var preferredProviderNames = new[] { "NotARealExecutionProvider", "CPUExecutionProvider" };
        var capturedOptions = new List<SessionOptions>();
        using var scope = SelectorHookScope.Install(capturedOptions.Add);

        // Act
        using var session = OnnxExecutionProviderSelector.Create(ModelPath, preferredProviderNames);

        // Assert: both the failed "NotARealExecutionProvider" candidate and the winning
        // "CPUExecutionProvider" candidate created their own options instance, and every one
        // of them is disposed - none survives as a leaked native OrtSessionOptions handle.
        Assert.NotNull(session);
        Assert.Equal(2, capturedOptions.Count);
        Assert.All(capturedOptions, options => Assert.True(options.IsClosed));
    }

    /// <summary>
    ///     Proves that the hook scope helper restores the previously installed hook on dispose, so
    ///     a test can never leak its callback into another test.
    /// </summary>
    [Fact]
    public void SelectorHookScope_Dispose_RestoresPreviousHook()
    {
        // Arrange
        var previous = OnnxExecutionProviderSelector.OnCandidateOptionsCreated;
        Action<SessionOptions> replacement = _ => { };

        // Act
        var scope = SelectorHookScope.Install(replacement);
        var installed = OnnxExecutionProviderSelector.OnCandidateOptionsCreated;
        scope.Dispose();

        // Assert
        Assert.Same(replacement, installed);
        Assert.Same(previous, OnnxExecutionProviderSelector.OnCandidateOptionsCreated);
    }

    /// <summary>
    ///     Proves that the CPU fallback is never probed when no preferred providers are supplied
    ///     at all (an empty candidate list), confirming the probe is strictly scoped to the
    ///     caller-supplied candidate loop, never the final guaranteed-success fallback.
    /// </summary>
    [Fact]
    public void OnnxExecutionProviderSelector_Create_EmptyPreferredProviders_NeverInvokesProbe()
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

    /// <summary>
    ///     Proves that candidates are tried in the caller-supplied order: when the probe rejects
    ///     the first candidate's session, the second candidate is built and probed next, and its
    ///     session - not the first's - is the one returned.
    /// </summary>
    [Fact]
    public void OnnxExecutionProviderSelector_Create_FirstCandidateFailsProbe_ReturnsSecondCandidateSession()
    {
        // Arrange: two candidates, with a probe that rejects only the first session it sees
        var preferredProviderNames = new[] { "CPUExecutionProvider", "CPUExecutionProvider" };
        var probedSessions = new List<InferenceSession>();
        void RejectFirstProbe(InferenceSession probedSession)
        {
            probedSessions.Add(probedSession);
            if (probedSessions.Count == 1)
            {
                RunWithMismatchedInput(probedSession);
            }
        }

        // Act
        using var session = OnnxExecutionProviderSelector.Create(ModelPath, preferredProviderNames, RejectFirstProbe);

        // Assert: exactly two sessions were probed, in order, and the second one was returned
        Assert.Equal(2, probedSessions.Count);
        Assert.NotSame(probedSessions[0], probedSessions[1]);
        Assert.Same(probedSessions[1], session);
    }

    /// <summary>
    ///     Proves that a candidate whose <c>validateSession</c> probe succeeds is the session
    ///     returned to the caller, i.e. the very instance that was handed to the probe.
    /// </summary>
    [Fact]
    public void OnnxExecutionProviderSelector_Create_ValidateSessionProbeSucceeds_ReturnsProbedCandidateSession()
    {
        // Arrange
        var preferredProviderNames = new[] { "CPUExecutionProvider" };
        InferenceSession? probedSession = null;
        void AcceptingProbe(InferenceSession candidate) => probedSession = candidate;

        // Act
        using var session = OnnxExecutionProviderSelector.Create(ModelPath, preferredProviderNames, AcceptingProbe);

        // Assert
        Assert.NotNull(probedSession);
        Assert.Same(probedSession, session);
    }

    /// <summary>
    ///     Proves that the <see cref="InferenceSession"/> returned on success is not disposed by
    ///     <see cref="OnnxExecutionProviderSelector.Create"/>: the caller can still run the
    ///     model's single identity node and read back the value it fed in.
    /// </summary>
    [Fact]
    public void OnnxExecutionProviderSelector_Create_Success_ReturnedSessionRemainsUsableForInference()
    {
        // Arrange
        using var session = OnnxExecutionProviderSelector.Create(ModelPath);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", new DenseTensor<float>(new[] { 3.5f }, [1])),
        };

        // Act
        using var results = session.Run(inputs);

        // Assert: the identity node echoes its input
        Assert.Equal(3.5f, results[0].AsTensor<float>()[0]);
    }

    /// <summary>
    ///     Feeds a wrong-typed tensor (long instead of the graph's declared float "input") so
    ///     ONNX Runtime's own input validation throws a genuine <see cref="OnnxRuntimeException"/>,
    ///     standing in for a real accelerated-provider probe failure (for example DirectML's
    ///     <c>ConvTranspose</c>) without needing any accelerated hardware.
    /// </summary>
    /// <param name="probedSession">The session to run the mismatched input against.</param>
    private static void RunWithMismatchedInput(InferenceSession probedSession)
    {
        var mismatchedInput = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", new DenseTensor<long>(new long[] { 1 }, [1])),
        };
        probedSession.Run(mismatchedInput);
    }

    /// <summary>
    ///     Asserts that <paramref name="session"/> has been disposed, observed by a valid inference
    ///     failing on it (a live session would succeed).
    /// </summary>
    /// <param name="session">The session expected to be disposed.</param>
    private static void AssertSessionIsDisposed(InferenceSession? session)
    {
        Assert.NotNull(session);
        var validInput = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", new DenseTensor<float>(new[] { 1.0f }, [1])),
        };
        // A live session runs this valid input successfully, so any failure proves the native
        // session handle was released (ONNX Runtime surfaces a disposed session as an
        // ObjectDisposedException or NullReferenceException depending on its version).
        Assert.ThrowsAny<SystemException>(() => session.Run(validInput));
    }
}
