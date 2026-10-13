## ConsoleCancellation

**Purpose**: Turn `Ctrl+C` (`Console.CancelKeyPress`) into cooperative cancellation for the duration
of a command, so every long-running subcommand shares one subscribe and unsubscribe implementation
instead of repeating the pattern. It is the only place in the CLI that subscribes to
`Console.CancelKeyPress`.

**Data Model**: Static class with nested `Registration`, an `IDisposable` that represents one live
subscription.

| Member | Purpose |
| --- | --- |
| `Registration` private state | Whether the handler suppresses process termination, and the cancellation work to run |
| `Registration.Handler` | The delegate subscribed to `Console.CancelKeyPress` |
| `Registration.Raise()` | Test seam: runs the signal's work; returns whether termination is suppressed |

**Key Methods**:

- `Register(source, onCancel = null)` subscribes a handler for a command that owns cancellation. When
  `Ctrl+C` arrives the handler sets `ConsoleCancelEventArgs.Cancel` so the runtime does not
  terminate the process, cancels `source`, and then runs `onCancel` (when given). The order is
  fixed: `onCancel` always runs after `source` has been canceled.
- `Observe(onCancel)` subscribes a handler that only runs `onCancel` and leaves `Cancel` untouched, so
  the termination decision stays with any other handler, typically one from an enclosing `Register`.
  A nested listener (for example one that stops a live recognition session) uses `Observe` so it
  cannot override the enclosing command's decision.
- Both methods return a `Registration`; disposing it unsubscribes the handler, so a command holds the
  subscription with a single `using` declaration around its blocking call. `Dispose` can be called
  more than once.
- `Raise()` exists because `ConsoleCancelEventArgs` has no public constructor, so tests cannot raise
  `Console.CancelKeyPress` themselves. It runs the same cancellation work as the real handler and
  reports whether the handler would suppress termination. The tests therefore prove the
  cancellation work and the termination flag, not the `e.Cancel = true` assignment inside the event
  handler.

**Threading**: The handler runs on the console's signal thread (not the thread that registered it),
possibly while the command is blocked. `onCancel` therefore runs on that thread, must not block, and
must be safe to call concurrently with the command. `source.Cancel()` runs the token's own callbacks
synchronously on the same thread. Unsubscription is not synchronized with a signal already in
flight, so a command should unsubscribe (dispose the registration) before it disposes the
cancellation source the handler calls, which is the order that results when the registration is
declared after the source in the same scope. The class holds no state of its own beyond each
registration, and the underlying event is process-global, so tests that subscribe run sequentially.

**Error Handling**: `Register` throws `ArgumentNullException` for a null `source` and `Observe`
throws it for a null `onCancel`; no subscription is made in either case. An exception thrown by
`source.Cancel()` (a token callback that throws surfaces as an `AggregateException`) or by `onCancel`
propagates on the signal thread; when `source.Cancel()` throws, `onCancel` does not run. `Dispose`
never throws.

**Dependencies**: `Console.CancelKeyPress` and `CancellationTokenSource` from the runtime only.

**Callers**: The commands that own cancellation call `Register`: `download`, `clean`, `uninstall`,
`voices`, `speak`, `ask` and `recognize`. `ask` passes an `onCancel` callback to stop its live session,
and `recognize` uses `Observe` for the nested listener that stops the recognition session.
