# Step 542 - Virtio PCI Capability Parser Default Result Guard

## Summary
- Added explicit initialized-state gating to
  `VirtioPciCapabilityListParseResult`.
- Updated the capability parser success predicate so a default/zero result
  cannot report success.
- Added host marker coverage for default parser result rejection while
  preserving required capability parsing and discovery-feed coverage.
- Updated driver and completion-gate docs.

## Why
`VirtioPciCapabilityListParseStatus.Success` is zero, so a default result could
previously satisfy status-only success checks without the capability-list parser
running. The initialized-state guard keeps empty caller state from becoming
virtio PCI capability evidence.

## Files changed
- `src/OS.Kernel/Drivers/VirtioPciCapabilityListParser.cs`
- `tests/OS.HostTests/VirtioPciCapabilityListParserTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step542.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.virtiopci.capability parser`, `TEST_PASS host.tests`,
  and failures.
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
TEST_START drivers.virtiopci.capability parser records required capabilities
TEST_PASS drivers.virtiopci.capability parser records required capabilities
TEST_START drivers.virtiopci.capability parser rejects truncated capability
TEST_PASS drivers.virtiopci.capability parser rejects truncated capability
TEST_START drivers.virtiopci.capability parser rejects capability loop
TEST_PASS drivers.virtiopci.capability parser rejects capability loop
TEST_START drivers.virtiopci.capability parser feeds discovery contract
TEST_PASS drivers.virtiopci.capability parser feeds discovery contract
TEST_START drivers.virtiopci.capability parser default result is not success
TEST_PASS drivers.virtiopci.capability parser default result is not success
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
- This is host-tested PCI configuration-space parser evidence only.
- No live PCI enumeration, BAR mapping, virtio binding, feature negotiation,
  queue setup, interrupts, packet/block I/O, cloud readiness, Kestrel support,
  or production readiness is claimed.

