# Step 774 - Diagnostic stub invocation formatter shared byte appends

## Summary
Migrated `DiagnosticStubInvocationReadinessFormatter` marker byte appends to
`OS.Std.Text.AsciiTextBuilder.AppendBytes(...)` while preserving the existing
`DIAGNOSTIC_STUB_INVOCATION_READY` and
`DIAGNOSTIC_STUB_INVOCATION_NOT_READY ...` marker text, required-byte query
behavior, short-destination behavior, invalid-status handling, and
default-result rejection.

## Why
The diagnostic-stub invocation marker formatter sits on the future owned-COM1
diagnostics path. Keeping both byte appends and decimal appends on the shared
no-runtime ASCII text builder removes diagnostics-local span-copy formatting
before any live generated-stub invocation work starts.

## Files changed
- `src/OS.Kernel/Diagnostics/DiagnosticStubInvocationReadinessFormatter.cs`
- `tests/OS.HostTests/DiagnosticStubInvocationReadinessFormatterTests.cs`
- `docs/diagnostics.md`
- `done/step774.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue732-diagnostic-stub-invocation-formatter\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue732-host-build-20260525.log`
- `dotnet .\artifacts\issue732-diagnostic-stub-invocation-formatter\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue732-hosttests-20260525.log`
- `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue732-git-diff-check-20260525.log`
- `git diff --check -- .\src\OS.Kernel\Diagnostics\DiagnosticStubInvocationReadinessFormatter.cs .\tests\OS.HostTests\DiagnosticStubInvocationReadinessFormatterTests.cs .\docs\diagnostics.md .\done\step774.md`

## Evidence
- Build log: `artifacts/logs/issue732-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host test log: `artifacts/logs/issue732-hosttests-20260525.log`
  - `TEST_PASS diagnostics.stub_invocation formatter formats ready marker`
  - `TEST_PASS diagnostics.stub_invocation formatter formats not-ready marker`
  - `TEST_PASS diagnostics.stub_invocation formatter reports short destination`
  - `TEST_PASS diagnostics.stub_invocation formatter rejects invalid status`
  - `TEST_PASS diagnostics.stub_invocation formatter uses shared ascii text builder`
  - `TEST_PASS diagnostics.stub_invocation formatter default result is not success`
  - `TEST_PASS host.tests`
- Source guard evidence:
  - `DiagnosticStubInvocationReadinessFormatter.cs` uses
    `AsciiTextBuilder.AppendBytes(destination, 0, ReadyMarker)` for the ready
    marker.
  - It uses `AsciiTextBuilder.AppendBytes(destination, offset, source)` for
    not-ready marker byte fragments.
  - It uses `AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)`
    for decimal writes.
  - It keeps `AsciiDecimalFormatter.CountUInt64Digits(...)` for
    required-byte counts.
  - The guard rejects direct `.CopyTo(`, `destination.Slice(offset)`,
    `ReadyMarker.CopyTo(destination)`, `source.CopyTo(destination...)`,
    `AsciiDecimalFormatter.FormatUInt64(destination.Slice(offset), value)`,
    and diagnostics-local digit-write loops.
- `artifacts/logs/issue732-git-diff-check-20260525.log` contains only
  LF-to-CRLF working-copy warnings and no whitespace-error diagnostics.
- The scoped owned-file `git diff --check -- ...` command exited 0 with the
  same LF-to-CRLF warnings.

## Failures
- The exact repository-wide `git diff --check *>&1 | Tee-Object ...` command
  returned nonzero under PowerShell because the active multi-agent worktree
  emits existing LF-to-CRLF warnings on many modified files. No trailing
  whitespace, space-before-tab, or blank-at-EOF diagnostics were found.
- A requested parallel subagent review could not run because `claude` reported
  `Not logged in`; no subagent file edits were made.
- Gitea was not updated from this run because no Gitea username/password was
  visible in the task context or environment.

## Deferred work
- Post the evidence comment to Gitea #732 and close it once credentials are
  available.
- Wire live diagnostic-stub invocation only after generated stubs can be safely
  mapped executable, range checked, callable-binding metadata has been consumed
  by a real unmanaged binding path, cache flushed if required, and called
  through unmanaged function pointers.
- Keep QEMU and port-I/O claims deferred until real stub invocation and owned
  serial output are implemented and tested.

## Evidence boundary
This step proves host-tested caller-owned formatting of already-produced
diagnostic-stub invocation readiness results through shared ASCII text-builder
helpers. It does not invoke generated stubs, create delegates, perform CPU
port I/O, mutate page permissions, flush instruction caches, write serial
output, prove QEMU diagnostics behavior, or provide cloud-provider evidence.

