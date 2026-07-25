# Step 049 - Boot Memory Map Acceptance Boundary

## What Changed

- Added `BootMemoryMapAcceptance` as a no-stdlib-safe normalized memory-map precondition.
- Added a handoff-and-memory-map-aware `BootSequence.Run(BootHandoffRecord, EfiMemoryMapSummary, ...)` overload.
- Added `BOOT_MEMORY_MAP_ACCEPTED` and `BOOT_MEMORY_MAP_REJECTED <status>` managed markers.
- Wired the live EFI path to emit `BOOT_MEMORY_MAP_ACCEPTED` after `UEFI_MEMORY_MAP_NORMALIZED` and before the ordered `boot.sequence` marker plan.
- Added `BOOT_MEMORY_MAP_ACCEPTED` to the script-level default QEMU smoke marker set.
- Added host tests for accepted, missing, and invalid normalized summaries.
- Updated boot, boot-order, QEMU, and completion-gate docs.

## Why It Changed

The live firmware path already copied and normalized owned UEFI memory-map bytes, but the ordered boot boundary did not yet model the normalized summary as a precondition. This step makes acceptance explicit before later allocator, paging, or `ExitBootServices` work can depend on the memory map.

## Files Changed

- `src/OS.Kernel/Boot/BootMemoryMapAcceptance.cs`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `done/step049.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\run-qemu-smoke.ps1 -DryRun -RequireMarker UEFI_GONE_SIM_PASS
.\scripts\run-qemu-smoke.ps1 -BuildEfi -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
.\scripts\check-naming.ps1
git diff --check
```

## Evidence

- Solution build passed with 0 warnings and 0 errors.
- Full host test run passed with `TEST_PASS host.tests`.
- Host test marker: `TEST_PASS boot.sequence memory map accepted`
- Host test marker: `TEST_PASS boot.sequence memory map missing rejected`
- Host test marker: `TEST_PASS boot.sequence memory map invalid rejected`
- Default QEMU dry-run emitted `REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED`.
- Explicit override dry-run emitted `REQUIRED_MARKERS UEFI_GONE_SIM_PASS`, proving targeted probe overrides still replace the defaults.
- `.\scripts\run-qemu-smoke.ps1 -BuildEfi ...` emitted `TEST_PASS efi.shell.publish`, `TEST_PASS imagebuilder.raw`, and `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.summary.log` reported `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED`.
- `artifacts/logs/qemu-smoke.log` contains `BOOT_MEMORY_MAP_ACCEPTED` before `TEST_START boot.sequence`.
- Naming/source firewall guard passed with `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with only existing LF-to-CRLF warnings.

## Failures

- An initial solution build attempt failed with `MSB3027`/`MSB3021` because another process held `tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe`. The kernel and EFI projects built before the apphost copy failure. Re-run after the host-test process exits.
- An initial `.\scripts\run-qemu-smoke.ps1 -BuildEfi ...` attempt exposed a wrapper bug: the script passed `-EfiOutputPath` through an array splat that resulted in the output path being used as the `build-efi-shell.ps1` runtime identifier. The wrapper now uses hashtable splatting for the build script, and the rebuild/boot path passes.

## Deferred Work

- This does not allocate pages, build a page allocator, choose usable memory regions, or transition through `ExitBootServices`.
- Future memory-management work must consume this accepted summary through explicit allocator/page-table contracts.

## Evidence Boundary

This step proves a marker and status-coded precondition boundary for a normalized UEFI memory-map summary. It does not prove real memory ownership, paging, allocator initialization, owned drivers, storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
