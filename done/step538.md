# Step 538 - ENA Admin Completion Default Entry Guard

## Summary
- Added an explicit initialized marker to `EnaAdminAcqEntry`.
- Updated ENA admin completion validation so `default(EnaAdminAcqEntry)` cannot
  validate as a successful completion for command ID 0 / phase false.
- Added host marker coverage for the default completion entry guard.
- Updated ENA driver and completion-gate docs.

## Why
The ENA admin completion parser supports command ID 0 as a valid hardware
command identifier, but a default completion entry also carries all-zero fields.
Without an explicit initialized marker, a caller could accidentally validate
zeroed state as a successful completion. The new guard distinguishes parsed ACQ
entries from default values without banning valid command ID 0 completions.

## Files changed
- `src/OS.Kernel/Drivers/EnaAdminDescriptors.cs`
- `tests/OS.HostTests/EnaAdminDescriptorsTests.cs`
- `docs/drivers.md`
- `docs/ena-driver-plan.md`
- `docs/completion-gates.md`
- `done/step538.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.ena.admin.desc rejects default completion entry`,
  `drivers.ena.admin.desc parses successful completion`, `TEST_PASS host.tests`,
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
TEST_START drivers.ena.admin.desc parses successful completion
TEST_PASS drivers.ena.admin.desc parses successful completion
TEST_START drivers.ena.admin.desc rejects default completion entry
TEST_PASS drivers.ena.admin.desc rejects default completion entry
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
- This is host-tested ENA admin descriptor/caller-buffer metadata only.
- No live ENA PCI discovery, BAR mapping, DMA allocation/mapping, admin queue
  programming, doorbell writes, completion polling, packet I/O, cloud readiness,
  or production readiness is claimed.
