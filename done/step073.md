# Step 073 - Executable Stub Memory Policy Plan

## What Changed

- Added `ExecutableStubMemoryPlan`, a host-tested C# policy and layout contract for future generated executable stubs.
- Added explicit protection flags and plan flags.
- Added default write-then-execute planning:
  - write phase: read/write;
  - execute phase: read/execute;
  - final phase: read/execute.
- Rejected accidental RWX policy by default.
- Calculated caller-owned payload offset and page-aligned required byte length.
- Added host tests for accepted policy, RWX rejection, aligned layout, and invalid inputs.

## Why It Changed

Issue #91 is a prerequisite for executing any C#-emitted low-level stubs. The project now has port-I/O, descriptor-table, and fault-probe byte plans; before those bytes can ever run, the repository needs a documented W^X gate and layout contract.

## Files Changed

- `src/OS.Kernel/Runtime/ExecutableStubMemoryPlan.cs`
- `tests/OS.HostTests/ExecutableStubMemoryPlanTests.cs`
- `docs/security.md`
- `docs/diagnostics.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS executable stub memory plan accepts write-then-execute policy`
  - `TEST_PASS executable stub memory plan rejects accidental rwx policy`
  - `TEST_PASS executable stub memory plan calculates aligned layout`
  - `TEST_PASS executable stub memory plan rejects invalid inputs`
  - `TEST_PASS host.tests`

## Failures

- No build failures after this change.

## Deferred Work

- This does not allocate executable memory.
- This does not change page protections.
- This does not flush instruction caches.
- This does not execute generated stubs.

## Evidence Boundary

This step proves host-side policy and layout behavior only. It is not W^X enforcement, executable-memory, or privileged-stub execution evidence.
