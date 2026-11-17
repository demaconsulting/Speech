namespace DemaConsulting.Speech.Text.En.HeteronymSubsystem;

/// <summary>
///     What happened when a model was requested from a <see cref="HeteronymModelProvider"/>.
/// </summary>
internal enum ModelLoadStatus
{
    /// <summary>No model exists (none is embedded in this build, or the source supplied none); not an error.</summary>
    NotShipped = 0,

    /// <summary>The model loaded and a classifier is available.</summary>
    Loaded = 1,

    /// <summary>A model exists but could not be read: the file is corrupt, truncated or otherwise invalid.</summary>
    Invalid = 2,

    /// <summary>A model exists and is well-formed but does not fit this build (feature set or inventory revision).</summary>
    Incompatible = 3,
}

/// <summary>
///     Supplies the heteronym classifier lazily and once: the model is opened and loaded the first time
///     a classifier is requested, never before, and the result (a classifier or a failure status) is
///     shared by every caller.
/// </summary>
/// <remarks>
///     <para>
///     <b>Laziness.</b> <see cref="IsConfigured"/> answers whether a model source exists without opening or
///     loading it, so an analyzer can decide whether the reading family is worth enabling while
///     constructing; an analyzer that is never invoked, a text without a candidate word and a selection
///     without the reading family never reach <see cref="GetClassifier"/>.
///     </para>
///     <para>
///     <b>Failures are values.</b> The load never throws: a missing model is <see cref="ModelLoadStatus.NotShipped"/>,
///     an unreadable one <see cref="ModelLoadStatus.Invalid"/> and an unfit one
///     <see cref="ModelLoadStatus.Incompatible"/>, and the status is cached like a success, so a corrupt model is
///     opened once, not on every request. (The one exception to "never throws" is a defective embedded
///     inventory table, which is a broken build with nothing to degrade to.)
///     </para>
///     <para>
///     <see cref="Embedded"/> is the process-wide provider over the model embedded in this assembly; this
///     build embeds none, so it is never configured. Instances are thread-safe.
///     </para>
/// </remarks>
internal sealed class HeteronymModelProvider
{
    /// <summary>The manifest name under which a later increment embeds the model.</summary>
    internal const string EmbeddedResourceName = "DemaConsulting.Speech.Text.En.Resources.heteronyms.bin";

    /// <summary>Opens the model's bytes, or returns null when there is none; null for a provider with no source.</summary>
    private readonly Func<Stream?>? _open;

    /// <summary>The inventory to classify against, or null for <see cref="ReadingInventory.Default"/> (resolved on first use).</summary>
    private readonly ReadingInventory? _inventory;

    /// <summary>An optional action run each time a load starts, for tests.</summary>
    private readonly Action? _loadObserver;

    /// <summary>An optional observer handed to the classifier, for tests.</summary>
    private readonly Action<int>? _classificationObserver;

    /// <summary>The embedded-resource probe, or null when this provider has an explicit source.</summary>
    private readonly Func<bool>? _probe;

    /// <summary>The once-only load; its value never throws and holds the classifier or the failure status.</summary>
    private readonly Lazy<LoadResult> _result;

    /// <summary>
    ///     Initializes a new instance over an explicit model source.
    /// </summary>
    /// <param name="open">
    ///     Opens the model file, or returns <see langword="null"/> when there is none; invoked at most once, on
    ///     the first <see cref="GetClassifier"/> call. The stream is read to its end and disposed. May be null
    ///     for a provider with no model.
    /// </param>
    /// <param name="inventory">The inventory to classify against; <see langword="null"/> for <see cref="ReadingInventory.Default"/>.</param>
    /// <param name="loadObserver">An optional action invoked when the load starts; tests count loads with it.</param>
    /// <param name="classificationObserver">
    ///     An optional observer given to the classifier, called with the running classification count; tests
    ///     use it to cancel an analysis at an exact word.
    /// </param>
    internal HeteronymModelProvider(Func<Stream?>? open, ReadingInventory? inventory = null, Action? loadObserver = null, Action<int>? classificationObserver = null)
    {
        _open = open;
        _inventory = inventory;
        _loadObserver = loadObserver;
        _classificationObserver = classificationObserver;
        _result = new Lazy<LoadResult>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    ///     Initializes the provider over the model embedded in this assembly.
    /// </summary>
    /// <param name="probe">Tells whether the embedded model exists, without reading it.</param>
    /// <param name="open">Opens the embedded model.</param>
    private HeteronymModelProvider(Func<bool> probe, Func<Stream?> open)
        : this(open)
    {
        _probe = probe;
    }

    /// <summary>
    ///     Gets the process-wide provider over the embedded model; this build embeds none, so it is not configured.
    /// </summary>
    internal static HeteronymModelProvider Embedded { get; } = new(
        static () => typeof(HeteronymModelProvider).Assembly.GetManifestResourceInfo(EmbeddedResourceName) is not null,
        static () => typeof(HeteronymModelProvider).Assembly.GetManifestResourceStream(EmbeddedResourceName));

    /// <summary>
    ///     Gets a provider that never has a model.
    /// </summary>
    internal static HeteronymModelProvider None { get; } = new((Func<Stream?>?)null);

    /// <summary>
    ///     Gets a value indicating whether a model source exists; cheap, and never opens or loads the model.
    /// </summary>
    internal bool IsConfigured => _probe is not null ? _probe() : _open is not null;

    /// <summary>
    ///     Gets the inventory the classifier works against (the supplied one or <see cref="ReadingInventory.Default"/>).
    /// </summary>
    internal ReadingInventory Inventory => _inventory ?? ReadingInventory.Default;

    /// <summary>
    ///     Gets the classifier, loading the model on the first call.
    /// </summary>
    /// <param name="status">The load status, the same for every call.</param>
    /// <returns>The shared classifier when <paramref name="status"/> is <see cref="ModelLoadStatus.Loaded"/>; otherwise <see langword="null"/>.</returns>
    /// <remarks>Thread-safe: concurrent first callers load once; the failure is cached as a status, not as an exception.</remarks>
    internal HeteronymClassifier? GetClassifier(out ModelLoadStatus status)
    {
        var result = _result.Value;
        status = result.Status;
        return result.Classifier;
    }

    /// <summary>
    ///     Opens, loads and checks the model; maps every failure to a status.
    /// </summary>
    /// <returns>The result to cache.</returns>
    private LoadResult Load()
    {
        _loadObserver?.Invoke();
        var inventory = Inventory;

        // Open the source: none or null means "not shipped"; an I/O failure is an unusable model.
        Stream? stream;
        try
        {
            stream = _open?.Invoke();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            return new LoadResult(null, ModelLoadStatus.Invalid);
        }

        if (stream is null)
        {
            return new LoadResult(null, ModelLoadStatus.NotShipped);
        }

        // Load strictly; any defect of the file is Invalid, any mismatch with this build is Incompatible.
        SparsePerceptronModel model;
        try
        {
            using (stream)
            {
                model = SparsePerceptronModel.Load(stream);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new LoadResult(null, ModelLoadStatus.Invalid);
        }

        try
        {
            return new LoadResult(new HeteronymClassifier(inventory, model, _classificationObserver), ModelLoadStatus.Loaded);
        }
        catch (InvalidDataException)
        {
            return new LoadResult(null, ModelLoadStatus.Incompatible);
        }
    }

    /// <summary>
    ///     The cached outcome of a load.
    /// </summary>
    /// <param name="Classifier">The classifier, or null on any failure.</param>
    /// <param name="Status">What happened.</param>
    private sealed record LoadResult(HeteronymClassifier? Classifier, ModelLoadStatus Status);
}
