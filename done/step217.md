# Step 217 - ENA Driver Research ADR

## Summary

Added `docs/ena-driver-plan.md` for OS issue #241. The ADR defines the ENA
driver boundary for AWS Nitro, keeps ENA separate from virtio-net and Azure
NetVSC, and scopes the first host-testable admin-queue work before live EC2
packet I/O.

## Why

AWS Nitro networking needs an ENA PCIe driver, but OS currently has only
host-tested packet primitives, PCI metadata parsers, and virtio-net planning
contracts. The ADR records the prerequisites and implementation order so later
agents add constants, parsers, and planners before touching BARs, MSI-X, DMA,
or live queues.

## Files changed

- `docs/ena-driver-plan.md`
- `done/step217.md`

## Tests run

- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- `docs/ena-driver-plan.md` covers ENA PCI discovery prerequisites, admin
  queue versus TX/RX queue responsibilities, BAR/MSI-X/DMA prerequisites,
  MAC/config discovery, and the Ethernet frame boundary into the existing
  network stack.
- The ADR explicitly states that ENA is not virtio-net, not Azure NetVSC, and
  not a Linux compatibility layer.
- The ADR records host-testable first slices for PCI candidate constants,
  admin queue descriptors, admin queue memory layout planning, completion
  parsing for device attributes, and register-map/admin-readiness planning.
- Created Gitea follow-up issues #243, #244, #245, #246, and #247 for those
  first ENA code slices.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0.

## Failures

- None for this documentation/backlog slice.
- `git diff --check` printed CRLF conversion warnings for unrelated dirty
  files already present in the worktree, but no whitespace errors.

## Deferred work

- ENA PCI constants, admin descriptors, admin queue layout planner, completion
  parser, and register-map code remain deferred to follow-up issues.
- Live ECAM reads, BAR mapping, MSI-X, DMA-safe allocation, ENA admin command
  submission, TX/RX queue setup, Ethernet frame I/O, DHCP, TCP, hosted .NET 10,
  and Kestrel remain unsupported.

