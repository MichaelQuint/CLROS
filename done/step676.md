# Step 676 - PAL Export Catalog Boot Readiness Marker

## Summary
Added a host-tested `BootSequence` marker boundary for PAL export catalog
readiness.

## Why
The PAL export catalog now records the required future `OSHost_*` ABI surface,
but boot-order evidence also needs an explicit readiness marker that can fail
closed when the catalog is default/uninitialized or structurally invalid.

## Files changed
- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/PalExportCatalogTests.cs`
- `docs/boot.md`
- `docs/pal.md`
- `docs/completion-gates.md`
- `done/step676.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `.\scripts\build-efi-shell.ps1`
- `git diff --check`

## Evidence
- Host-test build passed with 0 warnings and 0 errors.
- Full host tests ended with `TEST_PASS host.tests`.
- Solution build passed with 0 warnings and 0 errors.
- Naming/source guard emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- EFI shell publish emitted `TEST_PASS efi.shell.publish` and
  `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.
- `git diff --check` returned exit code 0 with only LF-to-CRLF warnings.
- New boot/PAL markers emitted:
  - `TEST_PASS boot.sequence pal export catalog ready`
  - `TEST_PASS boot.sequence pal export catalog rejects duplicate`
  - `TEST_PASS boot.sequence pal export catalog blocks default result`
  - `TEST_PASS boot.sequence pal export catalog preserves default markers`
- Ready marker shape:
  - `PAL_EXPORT_CATALOG_READY status=Ready initialized=true catalog=Success entryCount=56 firstFailingIndex=-1 duplicateIndex=-1 domain=unavailable signature=unavailable`
- Failure marker shape:
  - `PAL_EXPORT_CATALOG_NOT_READY status=<reason> initialized=<true|false> catalog=<status> entryCount=<n> firstFailingIndex=<n|-1> duplicateIndex=<n|-1> domain=<domain|unavailable> signature=<signature|unavailable>`

## Failures
- None in the final integrated verification.

## Deferred work
- This marker does not create unmanaged `OSHost_*` exports, link hosted
  CoreCLR, run .NET 10 apps, or prove ASP.NET Minimal API/Kestrel behavior.
- Future hosted-tier work still needs real exported symbols with POD-only ABI
  entry points, compile-time dependency firewalls, and guest smoke evidence.

## Evidence Boundary
This is host-tested boot-order marker evidence for the PAL catalog declaration
boundary. It does not change any completion gate to pass.
