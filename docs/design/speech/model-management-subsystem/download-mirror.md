### DownloadMirror

**Purpose**: Describe an internal HTTP(S) mirror that `SpeechModelDownloader` should fetch every
model's declared files from instead of each model's own hardcoded source URI, plus the single
authentication mechanism (if any) that mirror requires. Lets a host whose network policy blocks
public model hosts (for example a TLS-interception policy breaking `huggingface.co`) redirect
every model download to an internal mirror it controls, without any change to the models'
declared download descriptors.

**Data Model**: `BaseUri` (the absolute `http`/`https` base location of the mirror),
`Credentials` (an optional `NetworkCredential` for Basic/NTLM authentication), `BearerToken` (an
optional bearer token string). A `sealed record` with an explicit constructor; `Credentials` and
`BearerToken` are mutually exclusive.

**Key Methods**:

- **DownloadMirror(Uri, NetworkCredential?, string?)**: Validates every field eagerly - `BaseUri`
  must be absolute with scheme `http` or `https` (a `file://`/UNC mirror is explicitly out of
  scope, since no code path in this library ever dereferences one), and at most one of
  `Credentials`/`BearerToken` may be supplied.

**Error Handling**: Throws `ArgumentNullException` when `BaseUri` is null, and `ArgumentException`
when `BaseUri` is not absolute, its scheme is neither `http` nor `https`, or both `Credentials`
and `BearerToken` are supplied. Validation happens eagerly at construction so a misconfigured
mirror is rejected the moment it is created, not silently accepted and only discovered mid-download.

**Dependencies**: `System.Net.NetworkCredential`.

**Callers**: `SpeechModelDownloaderOptions` (as its `Mirror` property);
`SpeechModelDownloader.ResolveEffectiveUri` (reads `BaseUri`); `HttpModelDownloadClient.DownloadAsync`
(reads `Credentials`/`BearerToken` to apply mirror authentication).
