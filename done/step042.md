# Step 042 - BootSequence Handoff Validation

## What Changed

- Added a handoff-aware `BootSequence.Run(BootHandoffRecord, ...)` overload.
- Added `BootSequenceHandoffValidator`, `BootSequenceHandoffStatus`, and `BootSequenceHandoffValidationResult`.
- Added the stable `BOOT_HANDOFF_READY` marker for validated handoffs.
- Added `BOOT_HANDOFF_FAIL code=<status-code> status=<status-name>` for rejected handoffs.
- Validated boot mode, required capabilities, unknown capability bits, firmware-services mode, owned memory-map flag, source-buffer provenance, descriptor size/version/count, map-size arithmetic, and descriptor-count limits.
- Added host tests for valid owned-map handoff ordering, invalid handoff short-circuit behavior, missing required capabilities, source/owned-buffer aliasing, and descriptor count limits.
- Updated boot and boot-order docs with the current host-tested boundary.

## Why It Changed

Later boot phases must not run against an unvalidated firmware handoff. This step places the validation boundary inside `BootSequence`, which keeps boot ordering centralized and gives later EFI/PAL work a stable marker to prove that the handoff was accepted before phase execution.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS.Kernel/Boot/BootSequenceHandoff.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `done/step042.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED
git diff --check
```

## Evidence

- Solution build passed with 0 warnings and 0 errors.
- Host test marker: `TEST_PASS boot.sequence validates handoff and emits ready marker`
- Host test marker: `TEST_PASS boot.sequence rejects invalid handoff before phases`
- Host test marker: `TEST_PASS boot.sequence handoff validator rejects source capability and count issues`
- Full host test marker: `TEST_PASS host.tests`
- Naming guard marker: `TEST_PASS naming.guard`
- Source firewall marker: `TEST_PASS source.firewall`
- EFI publish marker: `TEST_PASS efi.shell.publish`
- QEMU marker: `TEST_PASS qemu.smoke`
- QEMU summary artifact: `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED`
- `git diff --check` exited 0 and reported only existing LF-to-CRLF warnings.

## Failures

- No code or test failures remain for this slice.

## Deferred Work

- Route the live no-stdlib EFI `BootHandoffRecord` through a handoff-aware validation path so QEMU can require `BOOT_HANDOFF_READY`.
- Wire the raw descriptor reader into the live owned-map path and normalize descriptors into `MemoryMapInfo`.
- Preserve a map-key retry contract before real `ExitBootServices`.

## Evidence Boundary

This step proves handoff validation in host-tested managed `BootSequence` execution. It does not yet prove `BOOT_HANDOFF_READY` in QEMU because the live EFI artifact still emits the allocation-free marker plan directly.
