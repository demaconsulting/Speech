## CommandDispatch

**Purpose**: Define the fixed, ordered set of subcommands the CLI recognizes, and resolve a name to
its descriptor. The table is the single source of truth for which subcommands exist, their `--help`
usage summaries and their handlers.

**Data Model**:

| Type | Member | Purpose |
| --- | --- | --- |
| `CommandDescriptor` (sealed record) | `Name` | The canonical kebab-case subcommand name |
| | `UsageSummary` | One-line flag summary shown by `--help` |
| | `Handler` | `Func<Context, Task>` that runs the subcommand |
| `CommandDispatch` (static class) | `Commands` | Read-only list of descriptors, in `--help` order |

The table holds thirteen entries, in this order: `list-models`, `model-info`, `download`,
`uninstall`, `clean`, `list-voices`, `voices`, `list-devices`, `devices`, `doctor`, `speak`,
`recognize`, `ask`. Each handler is the real implementation in the matching command subsystem.

**Key Methods**:

- `Find(name)` returns the descriptor whose name equals `name` by ordinal (case-sensitive)
  comparison, or `null` when there is none. A null `name` is rejected with `ArgumentNullException`.
- `Sync(handler)` (private) adapts a synchronous `Action<Context>` command to the asynchronous
  handler signature. The adapted handler runs the command inline and returns a completed task, so an
  exception from a synchronous command is thrown from the handler call itself rather than faulting
  the returned task; handlers of asynchronous commands fault the returned task instead. A caller
  that awaits the handler observes both the same way.

**Error Handling**: `Find` never throws for an unknown name; it returns `null`, and the caller
decides how to report it. `Context` turns a `null` result for the first non-global argument into an
`ArgumentException`, so an unknown subcommand is rejected before any handler runs. The table is
static and immutable after type initialization; `Validation` checks that it is well formed (see
_Validation_).

**Dependencies**: The command implementations of the five command subsystems (as handler targets)
and `Context` (the handler parameter type).

**Callers**: `Context` (name validation while parsing), `Program` (handler lookup for dispatch and
the command list in the usage text), and `Validation` (well-formed check and the help
self-test).
