### DownloadProgressReporter

**Purpose**: Provide the throttled live progress reporter shared by `download` and `voices install`, so that
both commands print progress the same way from one implementation instead of two copies.

**Data Model**: A static class with no state. Each reporter it creates holds, under a lock, the key of the
last progress item (file index and file count, plus the label) and the last value it printed (a whole percent
or a whole kilobyte count), in order to suppress repeats. Throttling is per item: when the item changes (the
next file of a multi-file download), the first report of the new item is always printed even if its value
equals the previous file's last value.

**Key Methods**:

- **Create(context, describe)**: Returns a `Progress<SpeechModelDownloadProgress>` bound to the context's
  output. When the report carries a fraction complete, it prints `{label}: {percent}%` once per whole percent.
  When the total size is unknown, it prints `{label}: {n} KB` once per whole kilobyte transferred. Repeats of
  the same value within one item are suppressed, so it never prints one line per transferred chunk. The caller supplies
  the
  label through `describe` (`'id' file i/n` for `download`, `'id'` for `voices install`).

**Error Handling**: A `null` context or `null` describe delegate is an `ArgumentNullException`. The reporter
itself never throws for any progress value.

**Dependencies**: `Context` and the library's `SpeechModelDownloadProgress`.

**Callers**: `DownloadCommand` and `VoicesCommand`.
