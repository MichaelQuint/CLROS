# Step 793 - Executable stub materializer shared buffer copy

## Summary

Migrated `ExecutableStubBundleMaterializer` slot byte copies onto
`OS.Std.Memory.BufferOperations.MemoryCopy` and added a host source-guard marker
for the contract.

## Why

Diagnostic stub materialization is caller-owned byte staging for future
executable diagnostic payloads. Routing the copy path through the shared
no-runtime buffer helper keeps the diagnostics/runtime boundary aligned with the
CloudReady std migration while preserving existing validation and status-coded
failure behavior.

## Files changed

- `src/OS.Kernel/Runtime/ExecutableStubBundleMaterializer.cs`
  - uses `BufferOperations.MemoryCopy` for per-slot source-to-destination
    copies;
  - returns an existing non-success status if the helper unexpectedly fails
    after prevalidation.
- `tests/OS.HostTests/ExecutableStubBundleMaterializerTests.cs`
  - adds `TEST_PASS executable stub bundle materializer uses shared buffer
    operations`.
- `docs/diagnostics.md`
  - records the source-guard marker and shared buffer helper boundary.
- `done/step793.md`
  - records this milestone and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
- direct repro for the transient QEMU workload-profile marker failure:
  `scripts/run-qemu-smoke.ps1 -DryRun -WorkloadProfile MultiMinimalApi`
- scoped `git diff --check`
- scoped trailing whitespace scan

## Evidence

- `artifacts/logs/issue751-host-build-default-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- `artifacts/logs/issue751-hosttests-default-r2-20260525.log`
  - `TEST_PASS executable stub bundle materializer uses shared buffer operations`
  - `TEST_PASS host.tests`
- `artifacts/logs/issue751-qemu-workload-profile-repro-20260525.log`
  - `WORKLOAD_PROFILE MultiMinimalApi`
  - `TEST_PASS workload.profile.tooling propagates multi`
  - `TEST_PASS qemu.runner.dryrun`
  - `SUMMARY_EXISTS=False`
- `artifacts/logs/issue751-git-diff-check-scoped-20260525.log`
  - scoped diff check exited `0`
- `artifacts/logs/issue751-rg-trailing-whitespace-20260525.log`
  - `TEST_PASS issue751.scoped_whitespace_source`

## Failures

- The first full host-test run in
  `artifacts/logs/issue751-hosttests-default-20260525.log` failed the unrelated
  `qemu smoke script dry-run propagates workload profile` marker. A direct
  script repro passed and confirmed no dry-run summary file was written; the
  second full host-test run passed with `TEST_PASS host.tests`.

## Deferred work

- Live IDT installation, generated-stub execution, page permission transitions,
  QEMU fault evidence, hosted .NET, ASP.NET Minimal API, and Kestrel remain
  deferred.

## Evidence boundary

This step is intended to prove only the host-tested diagnostic stub byte-copy
materializer contract. It is not executable-memory, live CPU descriptor-table,
QEMU fault, hosted .NET, or Kestrel evidence.
