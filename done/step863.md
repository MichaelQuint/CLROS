# Step 863 - Default-off M01 live-probe policy

## Summary

Implemented M01-016 by extending the central diagnostics probe catalog with
three append-only, compile-time-disabled M01 live-memory stages:

- loaded-image capture and owned-stack integration;
- bounded live-allocator integration; and
- reversible owned-paging integration.

Added an allocation-free, status-coded prefix validator that allows future
consumers to reject a later stage whose prerequisite is disabled, and added
the sole host-harness aggregate for the `m01.016` marker contract.

## Why

M01-001 through M01-015 created host-tested leaf contracts but did not provide
a single central opt-in policy for later EFI integration. The new switches
keep every live boundary disabled while M01-017 through M01-026 serialize
coordination, EFI linkage, entrypoint wiring, and QEMU evidence. The validator
also prevents an allocator-only or paging-without-allocator configuration from
being treated as a coherent stage selection.

## Files changed

- `src/OS.Kernel/Diagnostics/Probes.cs`
- `tests/OS.HostTests/DiagnosticsTests.cs`
- `docs/diagnostics.md`
- `done/step863.md`

## Tests run

All commands were run from `C:\Projects\CLROS-M01-016`:

```text
dotnet build .\CLROS.slnx --nologo -m:1
  exit 0; Build succeeded; 0 warnings; 0 errors

dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
  exit 0; Build succeeded; 0 warnings; 0 errors

dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
  exit 0
  line 1060: TEST_START m01.016
  line 1069: CLROS_M01_016_READY
  line 1070: TEST_PASS m01.016
  line 6192: TEST_PASS host.tests
  no TEST_FAIL or OS_SMOKE_FAIL marker

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
  exit 0
  TEST_PASS naming.guard
  TEST_PASS source.firewall

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
  exit 0
  TEST_PASS workload.profile.tooling default single
  TEST_PASS qemu.runner.dryrun

git diff --check
git diff --cached --check
  exit 0; no whitespace errors

git status --short
  exactly the four paths listed under Files changed were staged
```

The complete host-harness output was captured under
`C:\Users\Roger\AppData\Local\Temp\clros-m01-016-final-20260726-165632`.
The process exit file contains `0`, and the standard-error log is empty. This
issue requires host evidence only. The QEMU command was a dry run that
validated command and marker planning; no EFI build, firmware boot, or real
QEMU/OVMF execution was used as evidence.

## Evidence

The three public switches are exact `const bool` fields and remain `false`:

```text
EnableM01LoadedImageStackProbe
EnableM01LiveAllocatorProbe
EnableM01OwnedPagingProbe
```

They are appended at catalog indexes 13 through 15 under
`m01.live-memory`, with `ProbeRisk.Destructive`; the preceding indexes 0
through 12 retain their existing order. The host tests reflect over the exact
fields to reject `static readonly` substitutions, require unique catalog
membership, and require the formatter to report all three as
`enabled=false`, `risk=destructive`, and
`requires=destructive-probe`.

`ValidateM01LiveProbePolicy` exhaustively covers the eight Boolean
combinations. The valid ordered prefixes are `000`, `100`, `110`, and `111`.
Configurations `010` and `011` return
`LiveAllocatorRequiresLoadedImageStack`; configurations `001` and `101`
return `OwnedPagingRequiresLiveAllocator`. The byte-valued status reserves
zero for `Uninitialized`, so a default result cannot be mistaken for
`Success`.

The aggregate owns these stable host-only markers:

```text
TEST_START m01.016
CLROS_M01_016_READY
TEST_PASS m01.016
```

The test scans C# production/test sources and executable script/project source
types below `src`, `std`, `apps`, `tools`, `tests`, and `scripts` to keep the
ready marker host-only. It requires one `[ModuleInitializer]` occurrence in
`DiagnosticsTests.cs` and one correctly ordered ready-marker write. It also
reasserts that the M01-015 supervisor null-read plan does not execute or claim
guest evidence and remains assigned to M02-014.

Reference influence:

- This slice adapts only the existing first-party CLROS default-off dangerous
  probe pattern in `src/OS.Kernel/Diagnostics/Probes.cs` from base commit
  `11cee5ce0b959482743f59d3275dc40bf37ecdfe`.
- No SharpOS or other external reference implementation was copied or
  materially adapted, so no external license grant was required for these
  changes.

## Failures

No implementation or verification failure occurred. The first focused build
and the first complete host-harness run both passed.

## Deferred work

- M01-017 through M01-019 own host-tested stage coordinators.
- M01-020 owns canonical EFI project linkage and build-switch plumbing.
- M01-021 through M01-026 own entrypoint integration and fresh cumulative
  QEMU/OVMF evidence.
- M02-014 retains ownership of the separately disabled live supervisor null
  read and captured page-fault evidence.

## Evidence boundary

- This proves only a host-tested central probe catalog and dependency policy.
- `Probes.cs` is not linked into or consumed by the canonical EFI project in
  this issue.
- No build option, `BootSequence`, or `EfiEntry` path reads these switches.
- No EFI artifact was built or inspected for this evidence, and QEMU/OVMF did
  not run.
- No loaded-image protocol call, stack allocation or transfer, allocator
  mutation, page allocation/free, page-table mutation, W^X/NX transition,
  TLB invalidation, CR3 load, null read, page fault, or guest marker occurred.
- No live page-zero absence is proven; M01-026 owns that cumulative guest
  evidence.
- `CLROS_M01_016_READY` is a host-harness policy marker only. It is not serial
  or guest evidence and changes no completion gate.
