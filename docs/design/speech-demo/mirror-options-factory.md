## MirrorOptionsFactory

**Purpose**: Validate mirror settings and build the library's `DownloadMirror`, shared by the
launch options and the model catalog panel so both entry points apply identical rules. Callers wrap
the result into `SpeechModelDownloaderOptions`.

**Data Model**: Stateless static class. Input is a mirror URL, an optional user and password, and
an optional bearer token, all as strings. The result is a `DownloadMirror?`: a mirror, or `null`
when no mirror is requested.

**Key Methods**: `Create(url, user, password, bearerToken)`:

- A null or empty URL with no credential or token returns `null`, meaning each model uses its own
  public download address. A whitespace-only URL counts as supplied and is rejected as invalid.
- A blank URL with any credential or token is rejected, so a stray credential is never silently
  discarded.
- A URL that is not a valid absolute URI is rejected.
- A user without a password, or a password without a user, is rejected.
- Otherwise it builds a `DownloadMirror` from the base URI, the credentials and the bearer token.
  The `DownloadMirror` constructor rejects non-HTTP(S) schemes, query strings, fragments, embedded
  user info, and credentials sent over plain HTTP to a non-loopback host; that failure surfaces as
  an `ArgumentException` too.

**Error Handling**: Throws `ArgumentException` with an explanatory message for every rejected
combination; the message never echoes a secret or an invalid URL value. The callers present the
message to the user.

**Dependencies**: The library's `DownloadMirror`.

**Callers**: `AppLaunchOptions.CreateDownloaderOptions` and `ModelCatalogViewModel` when applying
mirror settings.
