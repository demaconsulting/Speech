### DownloadProgressReporter

**Purpose**: Provide the throttled live progress reporter shared by `download` and `voices install`, so that
both commands print progress the same way from one implementation instead of two copies.

**Data Model**: A static class with no state. Each reporter it creates holds the last percent and the last
kilobyte count it printed, in order to suppress repeats.

**Key Methods**:

- **Create(context, describe)**: Returns a `Progress<SpeechModelDownloadProgress>` bound to the context's
  output. When the report carries a fraction complete, it prints `{label}: {percent}%` once per whole percent.
  When the total size is unknown, it prints `{label}: {n} KB` once per whole kilobyte transferred. Repeats of
  the same value are suppressed, so it never prints one line per transferred chunk. The caller supplies the
  label through `describe` (`'id' file i/n` for `download`, `'id'` for `voices install`).

**Error Handling**: A `null` context or `null` describe delegate is an `ArgumentNullException`. The reporter
itself never throws for any progress value.

**Dependencies**: `Context` and the library's `SpeechModelDownloadProgress`.

**Callers**: `DownloadCommand` and `VoicesCommand`.
