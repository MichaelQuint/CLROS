# Step 618 - Reserved

## Summary

Added a host-tested PAL status-name formatter that writes canonical
`OSHOST_STATUS_*` ASCII names into caller-owned buffers and reports status-coded
formatting metadata.

## Why

The `OSHostStatus` enum is the shared status-code vocabulary for future PAL
exports, but there was no no-allocation caller-buffer formatter for stable
diagnostic names. This slice adds a small value-type boundary that can support
future machine-readable PAL diagnostics without exporting PAL functions,
allocating return buffers, or crossing the ABI with managed references.

## Files changed

- `src/OS.Kernel/Pal/OSHostStatusNameFormatter.cs`
- `tests/OS.HostTests/PalStatusNameFormatterTests.cs`
- `docs/pal.md`
- `work/PAL/pal-design.md`
- `docs/completion-gates.md`
- `done/step618.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe *> .\artifacts\logs\host-tests-step618.log`
- `dotnet build .\OS.slnx --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

The host-test project build passed with 0 warnings and 0 errors.

`artifacts/logs/host-tests-step618.log` emitted:

```text
TEST_PASS pal.status_names formats all canonical statuses
TEST_PASS pal.status_names reports required bytes
TEST_PASS pal.status_names rejects short buffer
TEST_PASS pal.status_names rejects unknown status
TEST_PASS pal.status_names preserves caller buffer on failure
TEST_PASS pal.status_names default result is not success
TEST_PASS host.tests
```

The solution build passed with 0 warnings and 0 errors.

`scripts/check-naming.ps1` emitted:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
```

`git diff --check` passed with line-ending normalization warnings only.

## Failures

- A first rebuild after the initial warning cleanup failed because another
  `OS.HostTests` process still held
  `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll`. The lock cleared and the
  normal host-test build then passed.
- An isolated `OutDir` host-test run was attempted to avoid the lock, but the
  existing image-builder tests assume the normal output/tool layout and failed
  from that custom layout. The normal output-layout host test run passed and is
  the evidence for this step.

## Deferred work

- No `OSHost_*` diagnostic export is implemented.
- No hosted runtime consumes these status names.
- No serial, app-log, or panic path is wired to this helper yet.
- Unknown status values intentionally return `OSHostStatus.InvalidArgument`
  instead of synthesizing numeric fallback text.

## Evidence Boundary

This is host-tested PAL status/diagnostic contract evidence only. It does not
prove exported PAL behavior, scheduler integration, firmware shutdown, hosted
.NET 10, Minimal API, Kestrel, live serial output, or cloud readiness.

