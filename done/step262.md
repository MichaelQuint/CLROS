# Step 262 - AWS Nitro NVMe PCI Candidate And Namespace Identity Planner

## What Changed

- Added `NitroNvmePlanner`, a host-testable metadata planner for AWS Nitro NVMe
  discovery over caller-provided PCI, BAR, MSI-X, Identify Controller, and
  Identify Namespace facts.
- Added host tests for NVMe PCI identity acceptance, namespace identity parsing,
  malformed identity rejection, non-NVMe rejection, and missing/invalid MMIO BAR
  rejection.
- Updated the AWS Nitro device plan with the current NVMe metadata evidence and
  explicit no-I/O boundary.

## Why It Changed

Issue #262 needed deterministic NVMe PCI/resource/namespace identity parsing
before any live Nitro storage initialization. The planner preserves PCI source,
vendor, device, subsystem, BAR, MSI-X, namespace, serial/model, LBA geometry,
and capacity metadata while keeping all inputs synthetic and caller-owned.

## Files Changed

- `src/OS.Kernel/Drivers/NitroNvmeCandidate.cs`
- `tests/OS.HostTests/NitroNvmeCandidateTests.cs`
- `docs/aws-nitro-device-plan.md`
- `done/step262.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet build .\OS.slnx --nologo -m:1
git diff --check
```

## Logs And Evidence

The focused builds passed with:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The full host harness emitted:

```text
TEST_PASS drivers.nvme.nitro identifies controller
TEST_PASS drivers.nvme.nitro parses namespace identity
TEST_PASS drivers.nvme.nitro rejects malformed identity
TEST_PASS drivers.nvme.nitro rejects non nvme device
TEST_PASS drivers.nvme.nitro rejects missing mmio bar
TEST_PASS host.tests
```

`git diff --check` exited `0`. It printed line-ending normalization warnings
for pre-existing modified files in the dirty worktree, but reported no
whitespace errors.

## Evidence Boundary

This is parser/planner evidence only. It does not perform live PCI config-space
reads or writes, BAR sizing writes, BAR mapping, MMIO access, admin queue
creation, DMA allocation, interrupt setup, NVMe command submission, sector
reads or writes, filesystem reads, EBS root-volume selection, EC2 boot, Minimal
API hosting, or Kestrel.

## Failures

No current verification failures for this parser/planner slice. An earlier
unrelated parent-tree ImageBuilder test edit temporarily blocked the full
solution build, but the integrated tree now builds with `0 Warning(s)` and
`0 Error(s)`.

## Deferred Work

- Add live PCI config-space reads and BAR sizing only after the platform ECAM
  reader and config-write safety contracts exist.
- Add NVMe admin queue memory/layout planning before controller enablement.
- Submit Identify commands to a live controller only after BAR mapping,
  DMA-safe memory, doorbells, completion handling, and timeout policy exist.
- Treat AWS EBS root-volume mapping as a later step that validates real
  controller/namespace identity and observed AWS block-device metadata.
