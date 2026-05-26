# Step 489 - COM1 backend contract default guard

## Summary

Implemented Gitea issue #474 by making `Com1PortIoBackendContractResult`
reject default/zero values as successful COM1 backend address-contract evidence.

## Why

`Com1PortIoBackendContractStatus.Success` is zero. The contract result
previously reported success for `default(Com1PortIoBackendContractResult)` even
though no COM1 base port, planned backend metadata, executable stub slots, or
absolute future backend addresses had been initialized. The backend contract is
address metadata for future port-I/O stubs, so successful evidence must be
explicitly constructed.

## Files changed

- `src/OS.Kernel/Diagnostics/Com1PortIoBackendContract.cs`
- `tests/OS.HostTests/Com1PortIoBackendContractTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step489.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `com1 port io backend contract default result is not success`
  and `TEST_PASS host.tests`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START com1 port io backend contract default result is not success
TEST_PASS com1 port io backend contract default result is not success
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

- Claude Code subagent retry failed in non-interactive mode with `Not logged in
  - Please run /login`; no Claude edits or Gitea updates were performed.

## Deferred work

- This is host-tested metadata evidence only. It does not invoke generated
  port-I/O stubs, perform real CPU port I/O, install an owned COM1 backend,
  prove post-`ExitBootServices` serial output, support ASP.NET Minimal
  API/Kestrel, prove cloud readiness, or prove production readiness.
