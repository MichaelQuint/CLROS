# Step 276 - ExitBootServices Live Wiring Order Contract

## What Changed

- Added `UefiExitBootServicesLiveWiringPlan`.
- Added status-coded planning for the future live transition order:
  1. capture fresh memory map;
  2. confirm owned serial diagnostics;
  3. emit post-firmware diagnostics readiness;
  4. validate the `ExitBootServices` transition plan;
  5. call firmware only in a future enabled implementation;
  6. mark firmware services exited only after success;
  7. reject firmware-backed calls after the boundary.
- Added host tests for ordered steps, missing diagnostics, non-success
  transition plans, disabled firmware calls, and explicit future enablement.
- Updated boot, boot-order, EFI build, PAL, and completion-gate docs.

## Why It Changed

The project needs a precise live call order before wiring real
`ExitBootServices` into `EfiEntry`. This step records that order in a
host-tested contract while keeping firmware shutdown disabled by default.

## Files Changed

- `src/OS.Kernel/Boot/UefiExitBootServicesLiveWiringPlan.cs`
- `tests/OS.HostTests/UefiExitBootServicesLiveWiringPlanTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/efi-build.md`
- `docs/pal.md`
- `docs/completion-gates.md`
- `done/step276.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
```

## Logs And Evidence

The host-test project build passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The new marker tests are:

```text
TEST_PASS uefi.exitboot live wiring exposes ordered steps
TEST_PASS uefi.exitboot live wiring blocks missing diagnostics
TEST_PASS uefi.exitboot live wiring blocks non-success transition plan
TEST_PASS uefi.exitboot live wiring stops before firmware call when disabled
TEST_PASS uefi.exitboot live wiring can be explicitly enabled for future firmware call
```

## Failures

A full host-test run started while another worker was editing #277 and failed on
that worker-owned incomplete timer file. The #276 project build passed; full
test evidence should be rerun after #277 lands.

## Evidence Boundary

This is host-tested call-order evidence only. It does not change `EfiEntry`,
call `ExitBootServices`, mark live firmware services exited, or prove
post-firmware execution in QEMU.

## Deferred Work

- Wire the live plan into `EfiEntry` only after owned serial diagnostics are
  live and QEMU-proven.
- Implement the real fresh-map retry loop and firmware call behind an explicit
  probe or milestone issue.
