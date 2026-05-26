# Step 671 - QEMU Smoke Marker Hardening And Live Allocator Boundary Marker

## Summary
Tightened QEMU smoke pass/fail defaults and added a live EFI marker for the
current page allocator boundary. The live boot path now validates compact
conventional-memory region records into static free-list metadata and emits a
firmware-visible allocator-readiness marker with live free node/page/byte
counts derived from the UEFI memory map.

## Why
The previous QEMU runner default could treat `OS_SMOKE_PASS` alone as success.
That made false-positive boot evidence easier to miss. The allocator boundary
also lacked a live firmware-visible marker between memory region materialization
and later allocator work.

## Files changed
- `scripts/run-qemu-smoke.ps1`
- `tools/OS.QemuRunner/Program.cs`
- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/QemuRunnerHttpProbeTests.cs`
- `tests/OS.HostTests/WorkloadProfileTests.cs`
- `src/OS/Boot/EfiEntry.cs`
- `docs/boot.md`
- `docs/completion-gates.md`
- `docs/cloud-provider-requirements.md`
- `docs/kestrel.md`
- `docs/workload-profiles.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\build-efi-shell.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15`
- `git diff --check`

## Evidence
- Host tests ended with `TEST_PASS host.tests`.
- Naming/source firewall emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- Dry-run QEMU smoke emitted `TEST_PASS qemu.runner.dryrun` and listed the
  current default required marker chain including `BOOT_PAGE_ALLOCATOR_READY`;
  forbidden markers included `OS_SMOKE_FAIL`.
- EFI publish emitted `TEST_PASS efi.shell.publish` and produced
  `artifacts/efi/BOOTX64.EFI`.
- Default real QEMU smoke emitted `TEST_PASS qemu.smoke`.
- The serial log contained:
  `BOOT_PAGE_ALLOCATOR_READY status=Ready regions=102 conventionalPages=52245 conventionalBytes=213995520 freeNodes=9 freePages=52245 freeBytes=213995520 pageSize=4096 statusCode=0`.
- The QEMU summary reported `QEMU_SMOKE_STATUS pass`,
  `MISSING_MARKERS none`, `FORBIDDEN_MARKER_HIT none`, and
  `FAIL_REASON none`.

## Failures
- The first host-test rerun during development exposed stale QEMU fake markers
  and stale Gate 10 provider-split expectations. The tests/docs were updated
  and the full host suite passed afterward.
- `git diff --check` reported only LF-to-CRLF working-copy warnings.

## Deferred work
- `BOOT_PAGE_ALLOCATOR_READY` proves live metadata readiness only. It does not
  claim a live mutable page allocator, page allocation/free APIs, owned paging,
  NX/W^X enforcement, managed heap reservation, or `ExitBootServices`.
- The next concrete subsystem is turning the metadata chain into a real
  page-allocation/free API with explicit owned-range reservation before any
  runtime heap or driver allocation depends on it.

