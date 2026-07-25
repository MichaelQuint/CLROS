# Step 422 - Virtio-net RX dispatch default-result guard

## Summary
Added an initialized-result guard to virtio-net RX register-window dispatch results so default/zero values do not report success.

## Why
`VirtioNetReceiveRegisterWindowDispatchStatus.Success` is the zero enum value, so `default(VirtioNetReceiveRegisterWindowDispatchResult).Succeeded` could read as successful even though no slab completion or network dispatch had run.

## Files changed
- `src/OS.Kernel/Drivers/VirtioNetReceiveRegisterWindowDispatchPlan.cs`
  - Added `IsInitialized` metadata and required it for `Succeeded`.
- `tests/OS.HostTests/VirtioNetReceiveRegisterWindowDispatchPlanTests.cs`
  - Added the default-result regression test and marker.
- `docs/networking.md`
  - Documented the default/zero-result guard and marker.
- `docs/drivers.md`
  - Documented the default/zero-result guard and marker.
- `docs/completion-gates.md`
  - Added the default/zero-result guard to host-test evidence and marker lists.
- `done/step422.md`
  - Recorded this milestone.

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence
- Host test build succeeded with 0 warnings and 0 errors.
- Host test run completed with `TEST_PASS host.tests`.
- Full solution build succeeded with 0 warnings and 0 errors after integrating
  the concurrent #407 and #408 changes.
- Naming/source firewall succeeded:
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`
- Added marker:
  - `TEST_PASS drivers.virtionet.rx.dispatch.windowexec default result is not success`
- Existing RX dispatch markers continued passing:
  - `TEST_PASS drivers.virtionet.rx.dispatch.windowexec dispatches arp slab completion`
  - `TEST_PASS drivers.virtionet.rx.dispatch.windowexec dispatches ipv4 slab completion`
  - `TEST_PASS drivers.virtionet.rx.dispatch.windowexec rejects failed slab completion`
  - `TEST_PASS drivers.virtionet.rx.dispatch.windowexec propagates dispatcher failure`
  - `TEST_PASS drivers.virtionet.rx.dispatch.windowexec preserves no-live-mmio boundary`
- `git diff --check` exited 0; it printed existing LF-to-CRLF normalization warnings for dirty tracked files.

## Failures
None.

## Deferred work
Live virtio-net RX remains unimplemented. This step is host-tested caller-owned register-window, slab, and dispatch metadata only.

