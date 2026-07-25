# Step 070 - ExitBootServices Transition Marker Boundary

## What Changed

- Added `BootSequence.RunExitBootServicesTransition`, a host-tested transition marker helper for future `ExitBootServices` work.
- Added stable marker constants:
  - `UEFI_EBS_TRANSITION_READY`
  - `UEFI_EBS_TRANSITION_BLOCKED status=<status>`
- Added host tests proving:
  - successful plans emit the ready marker and mark `FirmwareServicesGuard` as `BootServicesExited`;
  - blocked plans emit the status marker and leave firmware mode unchanged;
  - the default boot marker order remains unchanged.

## Why It Changed

Issue #90 needed an ordered boot/runtime boundary for the future firmware shutdown step without calling real firmware prematurely. This keeps the transition decision tied to `BootSequence` and `UefiExitBootServicesPlan` while preserving the current safe QEMU smoke path.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `docs/boot-order.md`
- `docs/pal.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors after stale host-test processes were stopped.
- Host tests emitted:
  - `TEST_PASS boot.sequence exitboot transition emits ready marker`
  - `TEST_PASS boot.sequence exitboot transition blocks non-success plan`
  - `TEST_PASS boot.sequence exitboot transition leaves default markers unchanged`
  - `TEST_PASS host.tests`

## Failures

- An initial solution build failed because stale `OS.HostTests.exe` processes were locking `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll`.
- The stale processes were stopped and the solution build then passed.

## Deferred Work

- This helper is not called from the live EFI path.
- This does not call firmware `ExitBootServices`.
- This does not prove diagnostics survive after firmware shutdown.

## Evidence Boundary

This step proves host-side transition marker and firmware guard state behavior only. It is not live `ExitBootServices` evidence.
