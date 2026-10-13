### DownloadMirror

**Purpose**: Describe an internal HTTP(S) mirror that `SpeechModelDownloader` should fetch every
model's declared files from instead of each model's own hardcoded source URI, plus the single
authentication mechanism (if any) that mirror requires. Lets a host whose network policy blocks
public model hosts (for example a TLS-interception policy breaking `huggingface.co`) redirect
every model download to an internal mirror it controls, without any change to the models'
declared download descriptors.

**Data Model**: `BaseUri` (the absolute `http`/`https` base location of the mirror),
`Credentials` (an optional `NetworkCredential` for preemptive HTTP Basic authentication),
`BearerToken` (an optional bearer token string). A `sealed record` with an explicit constructor;
`Credentials` and `BearerToken` are mutually exclusive.

**Key Methods**:

- **DownloadMirror(Uri, NetworkCredential?, string?)**: Validates every field eagerly - `BaseUri`
  must be absolute with scheme `http` or `https` (a `file://`/UNC mirror is explicitly out of
  scope, since no code path in this library ever dereferences one) and must not carry a query
  string, fragment, or user-info component (which would misroute every mirrored request's path,
  or disclose a credential through this record's own `ToString`, respectively); at most one of
  `Credentials`/`BearerToken` may be supplied; `BearerToken`, if supplied, must not be empty or
  whitespace-only (otherwise `ApplyMirrorAuthentication` would still send it as a literal
  `Authorization: Bearer` header, deferring a misconfiguration to an opaque failure on the first
  download); and either, if supplied, requires `BaseUri` to use `https` rather than plain `http` -
  unless `BaseUri`'s host is a loopback address (the literal name `localhost` or a loopback IP
  literal such as `127.0.0.1` or `::1`), since that traffic never leaves the local machine and
  both are sent preemptively (no challenge/response handshake) by `HttpModelDownloadClient`.

`Credentials` is applied by `HttpModelDownloadClient` strictly as a preemptive HTTP Basic
`Authorization` header; a true NTLM (or other challenge/response) negotiation is deliberately not
supported, because it would require a dedicated `HttpClientHandler` that bypasses the host's own
`HttpClient` configuration (proxy, timeouts, certificate validation). `BearerToken` is applied as a
single `Authorization: Bearer` header. Mutual exclusivity is enforced by the constructor rather than
resolved with a silent precedence, so a host never wonders why one credential is ignored.

The compiler-generated `ToString` of the record is customized through a private `PrintMembers`
override: `BaseUri` and `Credentials` print as the record printer would (a `NetworkCredential` prints
only its type name, never the password), while `BearerToken` prints as `null` when unset and as the
fixed placeholder `***` when set, so the secret can never leak through logging, exception messages,
or debugger display of the mirror or of a containing `SpeechModelDownloaderOptions`. Rejecting a
user-info component in `BaseUri` closes the only other path by which a secret could reach `ToString`.

The query string/fragment rule exists because `SpeechModelDownloader.ResolveEffectiveUri` appends
path segments directly onto `BaseUri`'s string form: a query or fragment delimiter would end up
before the appended model path (for example `https://mirror/models?x=1` becoming
`.../models?x=1/model-a/model.bin`), whose real request path never reaches the intended file. The
`http`/`https`-only rule exists because `HttpModelDownloadClient` has no implementation path for a
`file://`/UNC mirror, and the absolute-URI rule because a relative base cannot combine with a model's
relative install path.

**Requirement mapping**: the unit requirements state only the observable behavior (what mirror
configurations are accepted or rejected, and that bearer-token secrets are not disclosed); the
record shape, member names and enforcement order described above are design detail.

**Error Handling**: Throws `ArgumentNullException` when `BaseUri` is null, and `ArgumentException`
when `BaseUri` is not absolute, its scheme is neither `http` nor `https`, it carries a query
string, fragment, or user-info component, both `Credentials` and `BearerToken` are supplied,
`BearerToken` is empty or whitespace-only, or either is supplied alongside a non-loopback `http`
(rather than `https`) `BaseUri`. Validation happens eagerly at construction so a misconfigured
mirror is rejected the moment it is created, not silently accepted and only discovered
mid-download.

**Dependencies**: `System.Net.NetworkCredential`.

**Callers**: `SpeechModelDownloaderOptions` (as its `Mirror` property);
`SpeechModelDownloader.ResolveEffectiveUri` (reads `BaseUri`); `HttpModelDownloadClient.DownloadAsync`
(reads `Credentials`/`BearerToken` to apply mirror authentication).
