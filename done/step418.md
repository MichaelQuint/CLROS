# Step 418 - PCI virtio descriptor catalog planner

## Summary

Added a host-tested PCI-to-virtio descriptor catalog planner that composes
injected ECAM scan metadata into modern virtio candidate selection,
fake-derived BAR-size binding, feature/queue metadata, and block/network
descriptor cataloging.

## Why

Issue #403 needed a small bridge from caller-provided PCI scan metadata to the
existing virtio descriptor catalog without installing live PCI enumeration or
virtio drivers. This gives storage and networking a shared descriptor metadata
boundary while preserving fake/injected provenance and failure status detail.

## Files changed

- `src/OS.Kernel/Drivers/PciVirtioDescriptorCatalogPlanner.cs`
- `tests/OS.HostTests/PciVirtioDescriptorCatalogPlannerTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step418.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Host test build passed with 0 warnings and 0 errors.
- Host test run ended with `TEST_PASS host.tests`.
- Full solution build passed with 0 warnings and 0 errors.
- Naming/source firewall emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0; Git reported CRLF normalization warnings only.
- New host markers:
  - `TEST_PASS drivers.pci virtio catalog composes block`
  - `TEST_PASS drivers.pci virtio catalog composes network`
  - `TEST_PASS drivers.pci virtio catalog reports capacity`
  - `TEST_PASS drivers.pci virtio catalog preserves fake provenance`
  - `TEST_PASS drivers.pci virtio catalog skips unsupported candidate`
  - `TEST_PASS drivers.pci virtio catalog propagates nested scan failure`
  - `TEST_PASS drivers.pci virtio catalog propagates binding failure`

## Failures

- None.

## Deferred work

- This is host-tested metadata composition only. It does not enumerate live PCI,
  execute live config-space reads or writes, restore hardware BARs, map ECAM or
  BARs, allocate or map DMA, configure queues, bind a live virtio driver,
  transfer packets, read sectors, launch hosted .NET, prove Kestrel, or prove
  cloud/production readiness.

