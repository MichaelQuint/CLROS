# Step 044 - Live Owned Memory Map Normalization Marker

## What Changed

- Added `EfiMemoryMapNormalizer`, a no-stdlib-safe live normalizer for owned UEFI descriptor bytes.
- Source-linked the live normalizer into the no-stdlib EFI project.
- Updated `EfiMain` to normalize a compact owned-map summary after `BOOT_HANDOFF_READY`.
- Added firmware-visible `UEFI_MEMORY_MAP_NORMALIZED` before `TEST_START boot.sequence`.
- Added firmware-visible `UEFI_MEMORY_MAP_NORMALIZE_FAIL <status-code>` if live normalization fails.
- Enabled unsafe code explicitly in `OS.Kernel` and `OS.HostTests` for this localized pointer-based EFI boundary contract.
- Added host tests for summary normalization, invalid metadata, and descriptor arithmetic failures.
- Updated boot, boot-order, EFI build, and completion-gate docs with QEMU evidence.

## Why It Changed

The EFI artifact already copied raw descriptors into OS-owned storage and validated the handoff. The next `ExitBootServices` prerequisite is proving the live owned bytes are internally usable without firmware-owned memory, heap allocation, exceptions, or normal framework APIs.

## Files Changed

- `src/OS.Kernel/Boot/EfiMemoryMapNormalizer.cs`
- `src/OS.Kernel/OS.Kernel.csproj`
- `src/OS/OS.csproj`
- `src/OS/Boot/EfiEntry.cs`
- `tests/OS.HostTests/EfiMemoryMapNormalizerTests.cs`
- `tests/OS.HostTests/OS.HostTests.csproj`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/efi-build.md`
- `docs/completion-gates.md`
- `done/step044.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED
.\scripts\check-naming.ps1
git diff --check
```

## Evidence

- Solution build passed with 0 warnings and 0 errors.
- Host test marker: `TEST_PASS efi.memorymap.normalizer summarizes owned descriptors`
- Host test marker: `TEST_PASS efi.memorymap.normalizer rejects invalid metadata`
- Host test marker: `TEST_PASS efi.memorymap.normalizer rejects descriptor arithmetic issues`
- Full host test marker: `TEST_PASS host.tests`
- EFI publish marker: `TEST_PASS efi.shell.publish`
- QEMU marker: `TEST_PASS qemu.smoke`
- Serial log marker: `UEFI_MEMORY_MAP_NORMALIZED`
- Summary artifact: `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED`
- Naming guard marker: `TEST_PASS naming.guard`
- Source firewall marker: `TEST_PASS source.firewall`
- `git diff --check` exited 0 and reported only existing LF-to-CRLF warnings.

## Failures

- The first solution build failed because `OS.Kernel` did not allow unsafe code. This was fixed by enabling `AllowUnsafeBlocks` on the kernel project.
- The second solution build failed because `OS.HostTests` did not allow unsafe code. This was fixed by enabling `AllowUnsafeBlocks` on the host-test project.

## Deferred Work

- Add the `ExitBootServices` retry-plan contract.
- Preserve map-key retry behavior before a real firmware shutdown call.
- Keep real `ExitBootServices` blocked until post-firmware diagnostics and owned drivers are ready.

## Evidence Boundary

This step proves the live EFI artifact can summarize owned raw UEFI descriptor bytes and emit `UEFI_MEMORY_MAP_NORMALIZED` in QEMU. It does not prove real `ExitBootServices`, page allocator ownership, owned drivers, storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
