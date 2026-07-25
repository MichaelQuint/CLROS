# Step 161 - Virtio Block Descriptor Chain Publication Metadata

## What Changed

- Added `VirtioBlockDescriptorChainPlan` as a pure read-only virtio-blk descriptor-chain metadata planner.
- Combined successful `VirtioBlockReadTransactionPlan` and `VirtqueueSplitRingPlan` results into exactly three descriptors: request header, data buffer, and status byte.
- Recorded descriptor indexes, `NEXT`/`WRITE` flags, caller-provided physical address placeholders, byte lengths, descriptor-table offsets, available-ring index/slot/entry offset, and required memory ranges.
- Added host tests for successful chain planning, device-writable data/status descriptors, insufficient descriptor rejection, and failed transaction-plan rejection.

## Why It Changed

Gitea issue #180 needs the next narrow boundary between virtio-blk read transaction sizing and live queue publication. This step makes descriptor-chain metadata deterministic before any code writes queue memory, maps DMA buffers, notifies a device, handles interrupts, or claims a real disk read.

## Files Changed

- `src/OS.Kernel/Storage/VirtioBlockDescriptorChainPlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockDescriptorChainPlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step161.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -p:OutputPath=C:\Projects\OS\artifacts\issue180\hosttests\
dotnet run --project <temp issue180 focused harness>
git diff --check -- src/OS.Kernel/Storage/VirtioBlockDescriptorChainPlan.cs tests/OS.HostTests/StorageVirtioBlockDescriptorChainPlanTests.cs docs/cstorage.md docs/drivers.md docs/completion-gates.md done/step161.md
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- The normal host-test build was blocked by an unrelated running `OS.HostTests.exe` process holding `tests\OS.HostTests\bin\Debug\net10.0\OS.Kernel.dll`.
- The isolated host-test build using `-p:OutputPath=C:\Projects\OS\artifacts\issue180\hosttests\` passed with 0 warnings and 0 errors.
- After the unrelated process exited, `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `artifacts/logs/host-tests-issue180-focused.log` emitted:
  - `TEST_PASS storage.virtioblk descriptor chain plans three descriptors`
  - `TEST_PASS storage.virtioblk descriptor chain marks data writable`
  - `TEST_PASS storage.virtioblk descriptor chain rejects insufficient descriptors`
  - `TEST_PASS storage.virtioblk descriptor chain rejects failed transaction plan`
- Scoped `git diff --check` exited 0 with only existing LF-to-CRLF normalization warnings in docs.

## Failures

- The first normal host-test project build failed with MSB3027/MSB3021 because another agent-owned host-test process had the shared output DLL locked.
- An initial attempt to isolate both output and intermediate paths failed because all referenced projects shared one `BaseIntermediateOutputPath`, causing generated assembly attribute collisions. The successful isolated build used only a custom output path.

## Deferred Work

- No queue-memory write path is implemented.
- No DMA-safe allocation or physical address translation is implemented; physical addresses are caller-provided placeholders.
- No virtqueue notify, interrupt, status-byte completion polling, retry, or timeout path is implemented.
- No real virtio-blk sector read is performed.

## Evidence Boundary

This step is host-tested descriptor-chain publication metadata only. It proves deterministic three-descriptor layout metadata and status-coded rejection for invalid prerequisites. It does not prove live virtio feature negotiation, queue-memory publication, DMA mapping, MMIO, device notification, interrupt handling, storage reads, manifest loading, or app launch.
