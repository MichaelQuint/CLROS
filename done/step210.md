# Step 210 - Virtio-Net MMIO Notification Boundary Plan

## What Changed

- Added `docs/virtio-net-mmio-boundary.md` to define the first live virtio-net PCI MMIO boundary after the queue bring-up planner.
- Identified the exact modern virtio PCI common-configuration offsets needed for feature negotiation, device status, queue selection, queue sizing, queue address installation, queue enable, and `queue_notify_off`.
- Identified the notify aperture address calculation, 16-bit no-notification-data write value, ISR read/clear semantics, and virtio-net device-config read offsets.
- Defined the ownership split between packet planners, queue lifecycle code, memory/platform ownership, virtio PCI register access, and driver coordination.

## Why It Changed

Issue #229 needed an implementation-ready boundary before any worker writes live MMIO code. The existing host-tested contracts already separate packet parsing, queue publication, BAR aperture validation, and queue bring-up metadata. This step keeps that separation intact and narrows the next implementation tasks to status-coded register maps, queue setup plans, notify address planning, and ISR handling.

## Files Changed

- `docs/virtio-net-mmio-boundary.md`
- `done/step210.md`

## Tests Run

No build or host-test run was required for this documentation-only slice.

Validation still required before check-in:

- `git diff --check -- docs/virtio-net-mmio-boundary.md done/step210.md`
- `.\scripts\check-naming.ps1`

## Logs And Evidence

- Gitea issue #229 was reviewed.
- Existing open issues were reviewed before creating follow-up work.
- The boundary was checked against the modern virtio PCI common, notification, ISR, and device-specific configuration model from the OASIS virtio 1.3 specification.

## Failures

None observed in this docs-only slice.

## Deferred Work

- Add a host-tested virtio PCI register-map constants contract.
- Add a host-tested status-coded fake register accessor boundary.
- Add queue setup planning over `VirtioNetDriverQueueBringUpPlan` metadata and caller-owned physical queue addresses.
- Add notify address/value planning for RX/TX queues.
- Add ISR status read/parse planning.
- Wire the plans to real BAR-backed volatile MMIO only after BAR mapping, DMA-safe memory ownership, and memory barriers exist.

## Evidence Boundary

This step is design evidence only. It does not map BARs, access MMIO, enable queues, notify a device, handle interrupts, transmit packets, receive packets, run FetchApp, host .NET 10, run ASP.NET Minimal APIs, or prove Kestrel support.
