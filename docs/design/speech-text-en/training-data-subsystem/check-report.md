### CheckReport

**Purpose**: Collect everything the checks have to say, in one place, and decide the exit code. No check writes to the
console and none decides success: they add findings and information lines, and the report writes them and answers
whether the run passed.

**Data Model**: `CheckReport` holds the `Findings` (`Finding` read-only records of the check name, the location and the
message, in the order added), the information lines (`Info`) and the number of findings printed per check (a positive
number given at construction, 25 by default). The location is the file name, with the one-based line when the finding
belongs to a line. `FindingCount` counts every finding, including those not printed. A report belongs to one run and
is not thread-safe.

**Key Methods**:

- **Add(check, location, message)**: records one finding.
- **AddInfo(line)**: records one information line, which is printed before the findings and never fails the run.
- **WriteTo(writer)**: writes the information lines, then the findings, each as
  `check-data: <check>: <location>: <message>` in the order added. For each check only the first stated number of
  findings is written; the others are counted, and a line `check-data: <check>: (<n> more findings not shown)` follows
  for each check that had more. Finally it writes the summary.
- **Summary**: `check-data: OK (0 findings)` when nothing was recorded and `check-data: FAILED (<n> findings)`
  otherwise, where the count includes the findings not shown.
- **ExitCode**: 0 when no finding was recorded and 1 otherwise. The exit code 2 for usage and input errors is decided by
  `CheckDataCommand`, which does not use the report for them.

**Error Handling**: a cap below one is a programming error and throws `ArgumentOutOfRangeException`; a finding never
throws. An output writer that fails propagates its exception.

**Dependencies**: the .NET base class library only.

**Callers**: every other unit of the subsystem and the unit tests.
