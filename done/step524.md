# Step 524 - Nitro NVMe plan default guard

## Summary

Implemented Gitea issue #509 by making the Nitro NVMe plan result reject
default/zero values as successful evidence.

## Why

`NitroNvmePlanStatus.Success` is zero, so a default `NitroNvmePlanResult`
previously reported success even though no PCI/register BAR validation,
identify-buffer parsing, namespace validation, or plan composition had occurred.

## Files changed

- `src/OS.Kernel/Drivers/NitroNvmeCandidate.cs`
- `tests/OS.HostTests/NitroNvmeCandidateTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step524.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.nvme.nitro rejects default plan result` and
  `TEST_PASS host.tests`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START drivers.nvme.nitro rejects default plan result
TEST_PASS drivers.nvme.nitro rejects default plan result
TEST_PASS host.tests

dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall

git diff --check
exit 0; existing LF-to-CRLF working-copy warnings only
```

## Failures

- None.

## Deferred work

- This is host-tested Nitro NVMe metadata planning evidence only. It does not
  discover a PCI NVMe controller, size or map BARs, perform live MMIO, submit
  admin commands, allocate DMA queues, ring doorbells, poll completions, read
  blocks, support ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove
  production readiness.

