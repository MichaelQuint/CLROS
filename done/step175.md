# Step 175 - Virtio-blk used-ring completion reader

## Issue

- Gitea issue: `#194`
- Scope: parse one virtio-blk split used-ring completion and request status byte from caller-owned memory.

## What Changed

- Added `OS.Storage.VirtioBlockUsedRingCompletionReader`.
- Added status-coded completion results for:
  - invalid queue sizes,
  - undersized used rings,
  - too-small status buffers,
  - no completion,
  - unexpected used index advancement,
  - unexpected descriptor head,
  - unexpected used byte count,
  - device I/O error,
  - unsupported request status,
  - unknown status bytes,
  - arithmetic overflow.
- Added host tests for successful completion parsing, wrapped used-index advancement, descriptor-head mismatch, byte-count mismatch, device I/O error, undersized ring, and unknown status byte.
- Updated `docs/cstorage.md` to record the completion-reader contract and its evidence boundary.

## Why

The descriptor-chain writer can publish one planned read request into caller-owned split-ring memory. The next storage slice needs a deterministic contract for consuming the corresponding used-ring entry and status byte before live virtio-blk code introduces interrupts, MMIO polling, DMA ownership, or device notification.

## Files Changed

- `src/OS.Kernel/Storage/VirtioBlockUsedRingCompletionReader.cs`
- `tests/OS.HostTests/StorageVirtioBlockUsedRingCompletionReaderTests.cs`
- `docs/cstorage.md`
- `done/step175.md`

## Tests And Evidence

Commands run:

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

Evidence:

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo` passed with 0 warnings and 0 errors.
- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors after a rerun.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` passed and ended with `TEST_PASS host.tests`.
- `.\scripts\check-naming.ps1` passed `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` passed. Git reported only existing CRLF normalization warnings for modified text files.

Host-test markers:

```text
TEST_PASS storage.virtioblk completion parses successful used entry
TEST_PASS storage.virtioblk completion advances wrapped used index
TEST_PASS storage.virtioblk completion rejects unexpected descriptor head
TEST_PASS storage.virtioblk completion rejects unexpected byte count
TEST_PASS storage.virtioblk completion reports device io error
TEST_PASS storage.virtioblk completion rejects undersized used ring
TEST_PASS storage.virtioblk completion rejects unknown status byte
```

## Failures

- An initial full solution build failed in `tests/OS.HostTests/Ipv4NeighborRoutePlannerTests.cs`, which is outside this issue's ownership. The later rerun passed without changes to this issue's files.

## Deferred Work

- Consume the completion reader from the first live virtio-blk queue path after DMA-safe queue ownership, MMIO notification, and completion waiting are implemented.
- Add QEMU evidence only after virtio-blk can publish requests to a real device and observe completions without UEFI storage services.
