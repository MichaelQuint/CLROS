# Step 051 - ExitBootServices Fresh-Map Probe Contract

## What Changed

- Added `Probes.EnableExitBootServicesFreshMapProbe`, disabled by default.
- Added a stable `uefi.exitbootservices` probe group and `GetExitBootServicesFreshMapProbe()` definition.
- Extended diagnostics host tests to prove the probe name, group, default-disabled state, boundary-simulation risk, and non-empty description.
- Extended the `UefiExitBootServicesPlan` contract with an explicit skipped-call result for probe builds.
- Added `scripts/build-efi-shell.ps1 -EnableExitBootServicesFreshMapProbe` and `scripts/run-qemu-smoke.ps1 -EnableExitBootServicesFreshMapProbe`.
- Wired the no-stdlib EFI probe build to reacquire and validate a fresh memory map, emit `UEFI_EBS_FRESH_MAP_READY`, emit `UEFI_EBS_CALL_SKIPPED`, and stop short of calling `ExitBootServices`.
- Documented that this is default-disabled pre-exit probe evidence only.

## Why It Changed

Gitea #69 needs a narrow contract for future `ExitBootServices` work before the live EFI path attempts firmware shutdown. The safe slice is a default-disabled probe build that proves fresh-map reacquisition and then explicitly skips the firmware shutdown call.

## Files Changed

- `src/OS.Kernel/Diagnostics/Probes.cs`
- `src/OS.Kernel/Boot/UefiExitBootServicesPlan.cs`
- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/DiagnosticsTests.cs`
- `tests/OS.HostTests/UefiExitBootServicesPlanTests.cs`
- `docs/boot.md`
- `docs/pal.md`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `done/step051.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\run-qemu-smoke.ps1 -BuildEfi -EnableExitBootServicesFreshMapProbe -EfiArtifact artifacts/efi/BOOTX64-EBS-PROBE.EFI -Image artifacts/images/os-ebs-probe.raw -SerialLog artifacts/logs/qemu-smoke-ebs-probe.log -SummaryLog artifacts/logs/qemu-smoke-ebs-probe.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_EBS_FRESH_MAP_PROBE_START,UEFI_EBS_FRESH_MAP_READY,UEFI_EBS_CALL_SKIPPED
git diff --check
```

## Evidence

- Host test marker: `TEST_PASS probes expose grouped safe defaults`
- Host test marker: `TEST_PASS uefi.exitboot fresh map plan accepts current key`
- Host test marker: `TEST_PASS uefi.exitboot fresh map plan rejects stale key`
- Existing ExitBootServices plan markers remain the host-side evidence for fresh-map required and stale map-key retry behavior.
- EFI probe publish emitted `EFI_PROBE EXIT_BOOT_SERVICES_FRESH_MAP` and `TEST_PASS efi.shell.publish`.
- QEMU probe smoke emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke-ebs-probe.summary.log` reported `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_EBS_FRESH_MAP_PROBE_START;UEFI_EBS_FRESH_MAP_READY;UEFI_EBS_CALL_SKIPPED`.
- Normal QEMU smoke did not emit `UEFI_EBS_*` markers by default.

## Deferred Work

- Add the actual `ExitBootServices` call only after post-firmware diagnostics and owned output paths are ready.
- Retry on stale map-key rejection in live firmware once the real call exists.
- Mark `FirmwareServicesGuard` exited only after the real firmware call succeeds.

## Evidence Boundary

This step proves a default-disabled pre-exit probe. The explicit EFI probe build reacquires and validates a fresh memory map and then emits `UEFI_EBS_CALL_SKIPPED`. It does not call `ExitBootServices`, does not transition firmware services off, and does not prove post-firmware execution.
