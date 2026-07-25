# Step 821 - Guarded Platform Filesystem UEFI-Gone Denial

## Summary

Added the minimal guarded `IPlatformFileSystem` contract for read-only
firmware-backed file reads. The contract validates safe relative ASCII paths,
uses caller-owned destination buffers, reports destination capacity through
status/result structs, and denies filesystem reads after UEFI-gone simulation
or marked `ExitBootServices` without touching the injected source.

## Why

Parent #4 still requires UEFI-encapsulation interfaces before any real
firmware shutdown work. Filesystem access must be represented as a guarded
platform boundary before a future UEFI Simple File System Protocol adapter can
exist, and it must be proven unavailable after the UEFI-gone boundary.

## Files changed

- `src/OS.Kernel/Platform/IPlatformFileSystem.cs`
- `src/OS.Kernel/Platform/GuardedPlatformFileSystem.cs`
- `src/OS.Kernel/Platform/PlatformUefiGoneSimulation.cs`
- `tests/OS.HostTests/PlatformFileSystemGuardTests.cs`
- `tests/OS.HostTests/PlatformUefiGoneSimulationTests.cs`
- `docs/pal.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step821.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo *> .\artifacts\logs\issue778-host-build.log`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll *> .\artifacts\logs\issue778-host-tests.log`
- Scoped `git diff --check` over #778 files to `artifacts/logs/issue778-diff-check.log`
- Scoped trailing-whitespace scan over #778 files to `artifacts/logs/issue778-trailing-whitespace.log`

## Evidence

- `artifacts/logs/issue778-host-build.log` ended with `Build succeeded.`,
  `0 Warning(s)`, `0 Error(s)`, and `issue778-host-build-exit=0`.
- `artifacts/logs/issue778-host-tests.log` emitted:
  - `TEST_PASS platform.filesystem allows read before UEFI gone`
  - `TEST_PASS platform.filesystem rejects read in UEFI-gone simulation`
  - `TEST_PASS platform.filesystem rejects read after ExitBootServices`
  - `TEST_PASS platform.filesystem rejects unsafe paths`
  - `TEST_PASS platform.filesystem reports destination capacity`
  - `TEST_PASS platform.filesystem result contains no managed references`
  - `TEST_PASS platform.uefi gone simulation denies filesystem adapter`
- `artifacts/logs/issue778-diff-check.log` ended with `git-diff-check-exit=0`.
- `artifacts/logs/issue778-trailing-whitespace.log` emitted
  `TEST_PASS issue778.trailing_whitespace`.
- Later final active-issue verification in
  `artifacts/logs/issue777-repro-host-tests.log` reached
  `TEST_PASS host.tests` after the unrelated TCP connection table blocker was
  resolved.

## Failures

- The original #778 full host run did not reach `TEST_PASS host.tests` because
  an unrelated in-progress `TcpConnectionTableTests` case failed later:
  `TEST_FAIL net.tcp.connection table closes removes and reuses slot`, followed
  by `issue778-host-tests-exit=-532462766`. That blocker was resolved later;
  `artifacts/logs/issue777-repro-host-tests.log` now contains
  `TEST_PASS host.tests`.

## Deferred work

- This is host-tested guard evidence only. It does not implement UEFI Simple
  File System Protocol wiring, directory enumeration, filesystem writes, live
  disk reads, package loading, app execution, hosted .NET 10, Minimal API,
  Kestrel, real `ExitBootServices`, or cloud readiness.
