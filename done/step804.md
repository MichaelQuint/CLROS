# Step 804 - QEMU Runner Stable Default Workload Profile

## Summary

Stabilized `OS.QemuRunner` so its direct CLI default workload profile is
always `SingleMinimalApi` unless `--workload-profile` is provided.

## Why

The host-test suite can execute workload-profile slices that run tools with
`MultiMinimalApi`. When a previously compiled `OS.QemuRunner.dll` was left in
that profile, the QEMU default-profile dry-run test could observe
`WORKLOAD_PROFILE MultiMinimalApi` before later script tests rebuilt the tool
back to `SingleMinimalApi`. The runner default is now deterministic and no
longer depends on whichever profile last compiled the host tool.

## Files changed

- `tools/OS.QemuRunner/Program.cs`
- `done/step804.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: pass
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
  - Result: pass

## Evidence

- `TEST_PASS qemu dry-run emits default workload profile`
- `TEST_PASS qemu dry-run propagates multi workload profile`
- `TEST_PASS qemu rejects invalid workload profile`
- `TEST_PASS host.tests`

## Failures

An earlier full host-test run failed at
`TEST_FAIL qemu dry-run emits default workload profile` because the existing
tool output had been compiled with `MultiMinimalApi`. The final verification
passed after making the default deterministic.

## Deferred work

This is host-tool CLI determinism only. It does not prove a live QEMU boot,
cloud image import, live guest workload selection, hosted .NET, ASP.NET Minimal
API, Kestrel, cloud readiness, or production readiness.
