## MirrorOptionsFactory

**Purpose**: Validate mirror settings and build the library's `DownloaderOptions`, shared by the
launch options and the model catalog panel so both entry points apply identical rules.

**Data Model**: Stateless static class. Input is a mirror URL, an optional user and password, and
an optional bearer token, all as strings. The result is a `DownloaderOptions` carrying a
`DownloadMirror`, or `null` when no mirror is requested.

**Key Methods**: `Create(url, user, password, bearerToken)`:

- A blank URL with no credential or token returns `null`, meaning each model uses its own public
  download address.
- A blank URL with any credential or token is rejected, so a stray credential is never silently
  discarded.
- A URL that is not a valid absolute URI is rejected.
- A user without a password, or a password without a user, is rejected.
- Otherwise it builds a `DownloadMirror` from the base URI, the credentials and the bearer token.

**Error Handling**: Throws `ArgumentException` with an explanatory message for every rejected
combination; the callers present the message to the user.

**Dependencies**: The library's `DownloaderOptions`, `DownloadMirror` and credential types.

**Callers**: `AppLaunchOptions.CreateDownloaderOptions` and `ModelCatalogViewModel` when applying
mirror settings.
