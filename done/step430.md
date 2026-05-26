# Step 430 - TCP receive default-result guard

## Summary

Implemented Gitea issue #415 by making `TcpEstablishedStreamReceiveResult`
reject default/zero values as successful receive evidence and proving the
socket bridge rejects default receive results before mock socket mutation.

## Why

`TcpEstablishedStreamStatus.Success` is zero. `default(TcpEstablishedStreamReceiveResult)`
previously reported success because `Succeeded` only checked the status field.
That default value could pass the first established-stream-to-mock-socket bridge
guard when paired with an empty payload.

## Files changed

- `src/OS.Kernel/Net/TcpEstablishedStream.cs`
- `tests/OS.HostTests/TcpEstablishedStreamTests.cs`
- `tests/OS.HostTests/TcpEstablishedStreamSocketBridgeTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step430.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.tcp stream receive default result is not success|net\.tcp\.stream\.socketbridge rejects default receive result|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- TCP default-result marker evidence:

```text
TEST_START net.tcp.stream.socketbridge rejects default receive result
TEST_PASS net.tcp.stream.socketbridge rejects default receive result
TEST_START net.tcp stream receive default result is not success
TEST_PASS net.tcp stream receive default result is not success
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

`TcpEstablishedStreamReadResult`, `TcpEstablishedStreamSendResult`, and broader
networking/storage result structs still need separate small default-result
hardening slices. This step is host-tested TCP/mock-socket contract evidence
only; it does not implement live NIC I/O, live sockets, hosted .NET, Kestrel,
cloud readiness, or production readiness.
