using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DemaConsulting.Speech.Proofreader.Session;

/// <summary>
///     Reads and writes <see cref="ProofreadSession"/> JSON files (format version 1).
/// </summary>
/// <remarks>
///     The format is plain indented JSON with camelCase names and string-valued categories so a
///     session can be read and diffed by a human. Parameter values deserialize from JSON strings,
///     numbers, and booleans back to <see cref="string"/>, <see cref="double"/>, and
///     <see cref="bool"/>, the three kinds a model parameter can have. Stateless and thread-safe.
/// </remarks>
internal static class SessionFile
{
    /// <summary>The serializer settings shared by every read and write.</summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    ///     Computes the SHA-256 of a text in UTF-8.
    /// </summary>
    /// <param name="text">The text to hash. Must not be <see langword="null"/>.</param>
    /// <returns>The lower-case hexadecimal hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public static string ComputeHash(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>
    ///     Serializes a session.
    /// </summary>
    /// <param name="session">The session. Must not be <see langword="null"/>.</param>
    /// <returns>The JSON text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> is <see langword="null"/>.</exception>
    public static string ToJson(ProofreadSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return JsonSerializer.Serialize(session, Options);
    }

    /// <summary>
    ///     Parses and validates a session.
    /// </summary>
    /// <param name="json">The JSON text. Must not be <see langword="null"/>.</param>
    /// <returns>The session, with typed parameter values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the text is not valid session JSON, the version is not supported, required
    ///     parts of the session or its flags are missing (JSON <c>null</c>), or the recorded text
    ///     hash does not match the stored text.
    /// </exception>
    public static ProofreadSession FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        // Malformed JSON and wrong shapes surface as one clear error type for the caller to show.
        ProofreadSession? session;
        try
        {
            session = JsonSerializer.Deserialize<ProofreadSession>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The file is not a valid proofreading session: {ex.Message}", ex);
        }

        if (session is null)
        {
            throw new InvalidDataException("The file is not a valid proofreading session: it is empty.");
        }

        if (session.Version != ProofreadSession.CurrentVersion)
        {
            throw new InvalidDataException(
                $"Unsupported session version {session.Version}; this tool reads version {ProofreadSession.CurrentVersion}.");
        }

        ValidateShape(session);

        // The hash guards against a hand-edited or truncated text silently shifting every flag.
        if (!string.Equals(session.SourceSha256, ComputeHash(session.Text), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The session's text does not match its recorded SHA-256; the file was modified or damaged.");
        }

        NormalizeParameters(session.SlotA);
        NormalizeParameters(session.SlotB);
        foreach (var flag in session.Flags)
        {
            NormalizeParameters(flag.Model);
        }

        return session;
    }

    /// <summary>
    ///     Rejects a session in which JSON <c>null</c> left a required part unset, so later code never faults on it.
    /// </summary>
    /// <param name="session">The deserialized session.</param>
    /// <exception cref="InvalidDataException">Thrown when a required part is missing.</exception>
    private static void ValidateShape(ProofreadSession session)
    {
        if (session.Text is null)
        {
            throw new InvalidDataException("The session has no text.");
        }

        if (session.SourceName is null || session.Flags is null)
        {
            throw new InvalidDataException("The session is incomplete: its source name or flags list is missing.");
        }

        if (session.SlotA is { Parameters: null } || session.SlotB is { Parameters: null })
        {
            throw new InvalidDataException("The session is incomplete: a slot has no parameters.");
        }

        for (var i = 0; i < session.Flags.Count; i++)
        {
            var flag = session.Flags[i];
            if (flag is null
                || flag.Model is null
                || flag.Model.Parameters is null
                || flag.Model.ModelId is null
                || flag.Model.DisplayName is null
                || flag.SentenceText is null
                || flag.Word is null
                || flag.Note is null
                || flag.Slot is null
                || flag.Readings is null
                || flag.Phonemes is null)
            {
                throw new InvalidDataException($"Flag {i + 1} is incomplete (a required value is missing).");
            }
        }
    }

    /// <summary>
    ///     Writes a session to a file.
    /// </summary>
    /// <param name="session">The session to write.</param>
    /// <param name="path">The destination path; an existing file is replaced.</param>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    public static void Save(ProofreadSession session, string path) =>
        File.WriteAllText(path, ToJson(session), new UTF8Encoding(false));

    /// <summary>
    ///     Reads a session from a file.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The session.</returns>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="InvalidDataException">Thrown as described for <see cref="FromJson"/>.</exception>
    public static ProofreadSession Load(string path) => FromJson(File.ReadAllText(path, Encoding.UTF8));

    /// <summary>
    ///     Replaces <see cref="JsonElement"/> parameter values with string, double, or bool values.
    /// </summary>
    /// <param name="model">The model whose parameters are converted; <see langword="null"/> is ignored.</param>
    private static void NormalizeParameters(SessionModel? model)
    {
        if (model is null)
        {
            return;
        }

        var typed = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, value) in model.Parameters)
        {
            object? converted = value is JsonElement element
                ? element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null,
                }
                : value;

            // A value of any other JSON kind is not a model parameter value and is dropped.
            if (converted is not null)
            {
                typed[key] = converted;
            }
        }

        model.Parameters = typed;
    }
}
