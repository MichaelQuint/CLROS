# Step 133 - Native App Executable Memory Permission Plan

## What Changed

- Added `NativeAppExecutableMemoryPlan` and app-specific status/result/protection contracts.
- The planner consumes `NativeAppPayloadMappingPlan` output and computes deterministic future executable-memory requirements:
  - required writable staging bytes;
  - executable byte count;
  - page-aligned executable map offset and length;
  - executable-content offset inside the mapping;
  - entry offset relative to the executable mapping.
- Added host tests for successful page-aligned planning, accidental RWX rejection, page/alignment validation, zero executable content rejection, and overflow rejection.
- Updated app-model, security, completion-gate, and NativeAOT/no-stdlib docs with the host-tested evidence boundary.

## Why It Changed

Gitea issue #152 needed a W^X/NX planning contract after native payload range planning and before any future app launcher maps executable pages or jumps into native app bytes.

## Files Changed

- `src/OS.Kernel/Runtime/NativeAppExecutableMemoryPlan.cs`
- `tests/OS.HostTests/NativeAppExecutableMemoryPlanTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/security.md`
- `done/step133.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

## Logs And Evidence

Parent integrated verification after the overlapping ImageBuilder conflict was resolved emitted:

- `TEST_PASS appmodel executable memory plan aligns payload mapping`
- `TEST_PASS appmodel executable memory plan rejects rwx policy`
- `TEST_PASS appmodel executable memory plan rejects invalid alignment`
- `TEST_PASS appmodel executable memory plan rejects zero executable content`
- `TEST_PASS appmodel executable memory plan rejects overflow`
- `TEST_PASS host.tests`

- `dotnet build .\OS.slnx --nologo -m:1` succeeded with 0 warnings and 0 errors.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `.\scripts\run-qemu-smoke.ps1 -DryRun` emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited 0 with existing line-ending warnings.

## Failures

- A concurrent ImageBuilder worker temporarily left duplicate `NativeAppPackageBuilder` definitions and an incomplete test helper. Parent integration resolved that out-of-scope conflict, then reran the solution build and host tests successfully.

## Deferred Work

- No executable page allocation was added.
- No real page permission transition was added.
- No payload byte copy, instruction-cache flush, relocation, or app entry invocation was added.
- No hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior was added.

## Evidence Boundary

This step is host-tested planning only. It does not prove QEMU boot-time package loading, live W^X/NX enforcement, executable mapping, native-tier app execution, hosted .NET 10 startup, ASP.NET Minimal API execution, or Kestrel support.
