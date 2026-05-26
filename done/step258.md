# Step 258 - ENA Data Queue Descriptor Layout Contract

## What Changed

- Added `EnaDataQueueDescriptors`, a status-coded caller-buffer contract for ENA
  TX submission descriptors, RX submission descriptors, TX completions, and RX
  completions.
- Added host tests for TX descriptor writing, RX descriptor writing, completion
  parsing, malformed descriptor buffers, and completion failure validation.
- Updated the ENA driver plan with the data-path descriptor boundary and current
  evidence limits.

## Why It Changed

Issue #258 needed the ENA data-path descriptor equivalent of the existing admin
descriptor work. This gives future TX/RX queue bring-up a deterministic C#
layout contract before any live ENA queues, DMA allocation, BAR access,
doorbells, interrupts, or packet I/O exist.

## Files Changed

- `src/OS.Kernel/Drivers/EnaDataQueueDescriptors.cs`
- `tests/OS.HostTests/EnaDataQueueDescriptorsTests.cs`
- `docs/ena-driver-plan.md`
- `done/step258.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

`dotnet build .\OS.slnx --nologo -m:1` passed with:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The full host harness emitted the #258 markers:

```text
TEST_PASS drivers.ena.data.desc writes tx descriptor
TEST_PASS drivers.ena.data.desc writes rx descriptor
TEST_PASS drivers.ena.data.desc parses completions
TEST_PASS drivers.ena.data.desc rejects malformed buffers
TEST_PASS drivers.ena.data.desc rejects completion failures
TEST_PASS host.tests
```

The descriptor code preserves the current evidence boundary:

- no DMA allocation;
- no page mapping;
- no MMIO or BAR access;
- no doorbells;
- no MSI/MSI-X setup;
- no interrupts;
- no Ethernet/IP/TCP parsing;
- no packet transmit or receive;
- no DHCP, HTTP, hosted .NET 10, or Kestrel evidence.

## Failures

No current verification failures for this descriptor-contract slice. A
previous unrelated QEMU HTTP-probe host-test failure was fixed and the full
host harness now exits 0.

## Deferred Work

- Create live ENA I/O queues only after BAR mapping, DMA-safe memory ownership,
  MSI-X, and admin queue feature discovery are implemented.
- Publish TX/RX queue memory to hardware through a future queue bring-up
  boundary.
- Feed ENA RX completions into the Ethernet receive path only after a live
  queue can return device-owned buffers safely.
- Keep checksum offload, RSS, multi-queue, LLQ, and advanced ENA features out
  of the first single-buffer baseline until negotiated and tested.
