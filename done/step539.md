# Step 539 - ENA Data TX Completion Default Guard

## Summary
- Added an explicit initialized marker to `EnaDataQueueTransmitCompletion`.
- Updated ENA TX completion validation so `default(EnaDataQueueTransmitCompletion)`
  cannot validate as a successful completion for request ID 0 / phase false /
  submission queue ID 0.
- Added host marker coverage for the default TX completion guard and confirmed a
  parsed request ID 0 completion still validates through the initialized path.
- Updated ENA driver and completion-gate docs.

## Why
ENA TX completion status uses zero for success and request ID 0 is valid. A
default completion value also carries all-zero fields, so validation could
mistake uninitialized caller state for a real successful completion. The
initialized marker separates parsed completion entries from default values
without rejecting legitimate request ID 0 completions.

## Files changed
- `src/OS.Kernel/Drivers/EnaDataQueueDescriptors.cs`
- `tests/OS.HostTests/EnaDataQueueDescriptorsTests.cs`
- `docs/drivers.md`
- `docs/ena-driver-plan.md`
- `docs/completion-gates.md`
- `done/step539.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.ena.data.desc rejects default tx completion`,
  `drivers.ena.data.desc parses completions`, `TEST_PASS host.tests`, and
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
TEST_START drivers.ena.data.desc parses completions
TEST_PASS drivers.ena.data.desc parses completions
TEST_START drivers.ena.data.desc rejects default tx completion
TEST_PASS drivers.ena.data.desc rejects default tx completion
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
- This is host-tested ENA data queue descriptor/caller-buffer metadata only.
- No live ENA PCI discovery, BAR mapping, DMA allocation/mapping, queue
  creation, doorbell writes, completion polling, interrupts, packet I/O, cloud
  readiness, or production readiness is claimed.
