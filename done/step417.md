# Step 417 - Physical MMIO ownership plan for ECAM

## Summary

Added a host-tested physical MMIO ownership planner for parsed PCI ECAM
segments. The planner writes caller-owned ownership range metadata and rejects
invalid bus ranges, unaligned ECAM bases, physical-address overflow,
destination-capacity exhaustion, overlapping ECAM windows, and overlap with
allocator-usable conventional memory.

## Why

Issue #402 needed a small post-firmware MMIO ownership planning slice before
any live ECAM mapping or PCI enumeration exists. This gives later platform and
driver work a status-coded metadata boundary for ECAM apertures while keeping
the evidence honest: no page mappings, volatile MMIO, or hardware access.

## Files changed

- `src/OS.Kernel/Platform/PhysicalMmioOwnershipPlan.cs`
- `tests/OS.HostTests/PhysicalMmioOwnershipPlanTests.cs`
- `docs/pal.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step417.md`

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
  - `TEST_PASS platform.mmio ownership accepts ecam`
  - `TEST_PASS platform.mmio ownership rejects usable-memory overlap`
  - `TEST_PASS platform.mmio ownership reports capacity`
  - `TEST_PASS platform.mmio ownership rejects arithmetic overflow`

## Failures

- None.

## Deferred work

- This is host-tested planning metadata only. It does not reserve ECAM ranges
  in live boot-memory ownership state, configure page tables, set cacheability
  attributes, map ECAM, read or write PCI configuration space, enumerate PCI,
  bind virtio devices, or prove cloud-provider hardware support.

