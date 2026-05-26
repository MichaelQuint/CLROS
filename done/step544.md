# Step 544 - Virtio PCI Candidate Selector Default Guard

## Summary
- Added explicit initialized-state gating to
  `VirtioPciCandidateSelectionResult`.
- Updated its success predicate so default/zero candidate-selection results
  cannot report success.
- Added host marker coverage for default candidate-selection result rejection
  while preserving block/network candidate selection, PCI location, skip, and
  destination-exhaustion coverage.
- Updated driver and completion-gate docs.

## Why
`VirtioPciCandidateSelectionStatus.Success` is zero, so a default result could
previously satisfy status-only success checks without selector execution. The
initialized-state guard keeps empty caller state from becoming virtio PCI
candidate metadata evidence.

## Files changed
- `src/OS.Kernel/Drivers/VirtioPciCandidateSelector.cs`
- `tests/OS.HostTests/VirtioPciCandidateSelectorTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step544.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.virtio.pci.candidate`, `TEST_PASS host.tests`, and
  failures.
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence
```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START drivers.virtio.pci.candidate selects block and network devices
TEST_PASS drivers.virtio.pci.candidate selects block and network devices
TEST_START drivers.virtio.pci.candidate preserves pci location
TEST_PASS drivers.virtio.pci.candidate preserves pci location
TEST_START drivers.virtio.pci.candidate skips unsupported vendor
TEST_PASS drivers.virtio.pci.candidate skips unsupported vendor
TEST_START drivers.virtio.pci.candidate skips unsupported virtio device id
TEST_PASS drivers.virtio.pci.candidate skips unsupported virtio device id
TEST_START drivers.virtio.pci.candidate requires capability pointer
TEST_PASS drivers.virtio.pci.candidate requires capability pointer
TEST_START drivers.virtio.pci.candidate reports destination exhaustion
TEST_PASS drivers.virtio.pci.candidate reports destination exhaustion
TEST_START drivers.virtio.pci.candidate default result is not success
TEST_PASS drivers.virtio.pci.candidate default result is not success
TEST_PASS host.tests
```

```text
dotnet build .\OS.slnx --nologo -m:1

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

```text
git diff --check
ExitCode: 0; existing LF-to-CRLF working-copy warnings only.
```

## Failures
- None for this slice.

## Deferred work
- This is host-tested candidate metadata evidence only.
- No live PCI enumeration, BAR mapping, binding, QEMU hardware proof, cloud
  readiness, Kestrel support, or production readiness is claimed.

