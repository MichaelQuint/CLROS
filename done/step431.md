# Step 431 - TCP read default-result guard

## Summary

Implemented Gitea issue #416 by making `TcpEstablishedStreamReadResult` reject
default/zero values as successful buffered-read evidence.

## Why

`TcpEstablishedStreamStatus.Success` is zero. `default(TcpEstablishedStreamReadResult)`
previously reported success because `Succeeded` only checked the status field.
Future callers must not treat zero/default read metadata as a completed
established-stream read.

## Files changed

- `src/OS.Kernel/Net/TcpEstablishedStream.cs`
- `tests/OS.HostTests/TcpEstablishedStreamTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step431.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.tcp stream read default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- TCP read default-result marker evidence:

```text
TEST_START net.tcp stream read default result is not success
TEST_PASS net.tcp stream read default result is not success
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

`TcpEstablishedStreamSendResult` and broader networking/storage result structs
still need separate small default-result hardening slices. This step is
host-tested TCP stream contract evidence only; it does not implement live NIC
I/O, live sockets, hosted .NET, Kestrel, cloud readiness, or production
readiness.
