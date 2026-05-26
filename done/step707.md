# Step 707 - FetchApp Endpoint QEMU Marker Preset

## Summary

Added the `fetch-http-endpoint` QEMU marker preset so host tooling can require
the FetchApp endpoint readiness marker (`FETCHAPP_HTTP_ENDPOINT_READY`) from
future probe images without changing the default smoke path.

## Why

The FetchApp endpoint BootSequence marker is now host-tested, but QEMU tooling
needed an explicit opt-in preset before future smoke lanes can require that
marker in a stable, machine-readable way. This is marker-contract tooling only;
it does not prove live FetchApp execution, disk-backed manifest loading, live
networking, HTTP GET, hosted .NET, Minimal API, or Kestrel.

## Files changed

- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `tools/OS.QemuRunner/Program.cs`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `done/step707.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- qemu.runner`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset fetch-http-endpoint`

## Evidence

- Build rerun exited 0 with `Build succeeded`, 0 warnings, and 0 errors.
- Focused host-test command exited 0 and emitted:
  - `TEST_PASS qemu.smoke.preset fetch http endpoint adds endpoint marker`
  - `TEST_PASS qemu.smoke.preset fetch http endpoint aliases normalize`
  - `TEST_PASS qemu.smoke.preset script dry-run includes fetch http endpoint preset`
  - `TEST_PASS host.tests`
- Script dry-run exited 0 and emitted:
  - `QEMU_MARKER_PRESETS fetch-http-endpoint`
  - `MARKER_PRESETS fetch-http-endpoint`
  - `REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;FETCHAPP_HTTP_ENDPOINT_READY`
  - `ORDERED_MARKERS none`
  - `FORBIDDEN_MARKERS OS_SMOKE_FAIL`
  - `TEST_PASS qemu.runner.dryrun`

## Failures

- The first build attempt failed because an unrelated parallel
  `OS.HostTests.exe` process held `tests/OS.HostTests/bin/Debug/net10.0`
  output DLLs. After that process exited, the same build command passed.

## Deferred work

- Add a CI dry-run lane for `fetch-http-endpoint` only as a separate CI-owned
  slice.
- Use this preset in real QEMU only after a guest image genuinely emits
  `FETCHAPP_HTTP_ENDPOINT_READY` from the tested boot path.
