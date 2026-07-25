# Step 170 - Virtio-net Header Metadata Contract

## What Changed

- Added `VirtioNetHeader` for the baseline 10-byte no-mergeable-buffers virtio-net header.
- Added host tests for no-offload TX header writing, RX frame metadata parsing, truncation, missing Ethernet header, and unsupported offload/GSO rejection.
- Updated networking, driver, and completion-gate docs.

## Why

Gitea issue #188 needed the metadata boundary between future virtio-net queue buffers and Ethernet frame dispatch. This step lets RX/TX buffers carry explicit virtio-net header status before any live queue publication or packet I/O exists.

## Files Changed

- `src/OS.Kernel/Drivers/VirtioNetHeader.cs`
- `tests/OS.HostTests/VirtioNetHeaderTests.cs`
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed in the worker run. A transient apphost lock produced retry warnings first, then the build completed.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- Observed markers:
  - `TEST_PASS drivers.virtionet.header writes zero tx header`
  - `TEST_PASS drivers.virtionet.header parses rx frame metadata`
  - `TEST_PASS drivers.virtionet.header rejects truncated header`
  - `TEST_PASS drivers.virtionet.header rejects missing ethernet header`
  - `TEST_PASS drivers.virtionet.header rejects offload and gso`
- `git diff --check` exited 0 with CRLF normalization warnings only.

## Evidence Boundary

This is caller-buffer metadata parsing/writing only. It does not publish descriptors, notify devices, process interrupts, read from a live virtio-net queue, negotiate offloads, feed protocols, or prove live packet I/O.
