# Step 432 - TCP send default-result guard

## Summary

Implemented Gitea issue #417 by making `TcpEstablishedStreamSendResult` reject
default/zero values as successful outbound-payload evidence.

## Why

`TcpEstablishedStreamStatus.Success` is zero. `default(TcpEstablishedStreamSendResult)`
previously reported success because `Succeeded` only checked the status field.
Future callers must not treat zero/default send metadata as a completed
established-stream send.

## Files changed

- `src/OS.Kernel/Net/TcpEstablishedStream.cs`
- `tests/OS.HostTests/TcpEstablishedStreamTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step432.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.tcp stream send default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- TCP send default-result marker evidence:

```text
TEST_START net.tcp stream send default result is not success
TEST_PASS net.tcp stream send default result is not success
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.
- Naming and source firewall guard emitted:

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

## Failures

None.

## Deferred work

Broader networking/storage result structs still need separate small
default-result hardening slices. This step is host-tested TCP stream contract
evidence only; it does not implement live NIC I/O, live sockets, hosted .NET,
Kestrel, cloud readiness, or production readiness.
