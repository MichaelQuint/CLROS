# Step 761 - Cctor State Formatter Shared ASCII Builder

## Summary

Migrated the `NATIVEAOT_CCTOR_STATE` diagnostic formatter from
formatter-local decimal byte counting, byte copy, and digit-write helpers to the
shared `OS.Std.Text` ASCII formatting surface.

## Why

Class-constructor state diagnostics are kernel-facing runtime evidence. Their
required-byte calculation and caller-owned write path should use the shared
no-runtime text helpers so cctor formatter code does not keep its own decimal
loops or direct copy/write paths.

## Files changed

- `src/OS.Kernel/Runtime/NativeAotClassConstructorStateDiagnosticFormatter.cs`
- `tests/OS.HostTests/NativeAotClassConstructorStateTableTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step761.md`
- `artifacts/logs/issue720-host-build-20260525.log`
- `artifacts/logs/issue720-hosttests-20260525.log`
- `artifacts/logs/issue720-git-diff-check-20260525.log`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue720-cctor-state-formatter\bin\Debug\net10.0`
- `dotnet .\artifacts\issue720-cctor-state-formatter\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence

- Build log: `artifacts/logs/issue720-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host-test log: `artifacts/logs/issue720-hosttests-20260525.log`
  - `TEST_PASS runtime.cctor state formatter formats success marker`
  - `TEST_PASS runtime.cctor state formatter reports short destination`
  - `TEST_PASS runtime.cctor state formatter rejects default result`
  - `TEST_PASS runtime.cctor state formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- Diff-check log: `artifacts/logs/issue720-git-diff-check-20260525.log`
  - `git diff --check` exited 0.
  - The log contains existing Git CRLF normalization warnings for the dirty
    worktree, with no whitespace-error output.

## Failures

- A first PowerShell `Tee-Object` wrapper surfaced Git CRLF warnings as a
  native-command error while capturing `git diff --check`. The check was rerun
  through command redirection and exited 0 with the requested log path.

## Deferred work

- This is host-tested diagnostic-formatting evidence only. It does not execute
  class constructors, discover NativeAOT metadata, invoke delegates, run module
  initializers, support lazy static reference fields, implement exception
  unwinding, change runtime helper status, emit QEMU runtime markers, or prove
  hosted .NET, ASP.NET Minimal API, Kestrel, cloud, or production readiness.
