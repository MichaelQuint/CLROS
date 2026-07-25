# Step 292 - Disabled ExitBootServices Fresh-Map Probe

## What Changed

- Confirmed the default-disabled `EnableExitBootServicesFreshMapProbe` path.
- The explicit probe build reacquires and validates a fresh UEFI memory map near
  the future shutdown point, emits `UEFI_EBS_FRESH_MAP_READY`, then emits
  `UEFI_EBS_CALL_SKIPPED` instead of calling firmware.
- The normal QEMU smoke path can forbid the probe markers so the disabled probe
  cannot leak into default boot evidence.
- The probe stays separate from the real `ExitBootServices` path and does not
  mutate firmware-services state.

## Why It Changed

The next real firmware-shutdown milestone needs proof that the OS can recapture
a fresh memory map and stop safely before the dangerous call. This probe keeps
that evidence QEMU-visible while preventing accidental `ExitBootServices`
execution.

## Files Changed

- `scripts/build-efi-shell.ps1`
- `scripts/run-qemu-smoke.ps1`
- `src/OS/OS.csproj`
- `src/OS/Boot/EfiEntry.cs`
- `src/OS.Kernel/Diagnostics/Probes.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/SerialMarkerSummaryTests.cs`
- `tests/OS.HostTests/DiagnosticsTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `docs/pal.md`
- `docs/qemu.md`
- `done/step292.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -ForbidMarker UEFI_EBS_FRESH_MAP_PROBE_START,UEFI_EBS_CALL_SKIPPED -SerialLog artifacts/logs/qemu-smoke-forbidden.log -SummaryLog artifacts/logs/qemu-smoke-forbidden.summary.log
.\scripts\run-qemu-smoke.ps1 -BuildEfi -EnableExitBootServicesFreshMapProbe -EfiArtifact artifacts/efi/BOOTX64-EBS-PROBE.EFI -Image artifacts/images/os-ebs-probe.raw -SerialLog artifacts/logs/qemu-smoke-ebs-probe.log -SummaryLog artifacts/logs/qemu-smoke-ebs-probe.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_EBS_FRESH_MAP_PROBE_START,UEFI_EBS_FRESH_MAP_READY,UEFI_EBS_CALL_SKIPPED
```

## Logs And Evidence

The solution build passed with 0 warnings and 0 errors.

The full host-test run emitted:

```text
TEST_PASS probes expose grouped safe defaults
TEST_PASS qemu runner accepts forbidden markers
TEST_PASS qemu dry-run emits marker requirements
TEST_PASS qemu real mode writes missing marker summary artifact
TEST_PASS qemu runner keeps required marker failure distinct
TEST_PASS host.tests
```

Normal QEMU smoke with forbidden probe markers emitted:

```text
FORBIDDEN_MARKERS UEFI_EBS_FRESH_MAP_PROBE_START;UEFI_EBS_CALL_SKIPPED
TEST_PASS qemu.smoke
```

Explicit probe QEMU smoke emitted:

```text
EFI_PROBE EXIT_BOOT_SERVICES_FRESH_MAP
REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_EBS_FRESH_MAP_PROBE_START;UEFI_EBS_FRESH_MAP_READY;UEFI_EBS_CALL_SKIPPED
TEST_PASS qemu.smoke
```

## Failures

No failures were observed in the final verification pass.

## Evidence Boundary

This step proves a disabled, QEMU-visible pre-exit probe that recaptures a fresh
memory map and stops before the firmware call. It does not call
`ExitBootServices`, does not mark firmware services exited, does not prove
post-firmware diagnostics, and does not prove owned drivers, storage,
networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

## Deferred Work

- Implement the real firmware call only after owned diagnostics are proven after
  the boundary.
- Add a retry loop around stale map-key rejection.
- Mark firmware services exited only after a successful real call.
- Reject all firmware-backed adapters after the real transition.
