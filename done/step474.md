# Step 474 - Network receive dispatch default guard

## Summary

Implemented Gitea issue #459 by making `NetworkReceiveDispatchResult` reject
default/zero values as successful receive-dispatch evidence.

## Why

`NetworkReceiveDispatchStatus.Success` is zero. The receive-dispatch result
previously reported success for `default(NetworkReceiveDispatchResult)` because
`Succeeded` only checked status. Receive dispatch consumers use classifier,
payload kind/range, ARP, IPv4, and payload parse metadata, so uninitialized
all-zero state must not count as a parsed ARP or IPv4 packet.

## Files changed

- `src/OS.Kernel/Net/NetworkReceiveDispatcher.cs`
- `tests/OS.HostTests/NetworkReceiveDispatcherTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step474.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.rx\.dispatch default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Network receive dispatcher default-result marker evidence:

```text
TEST_START net.rx.dispatch default result is not success
TEST_PASS net.rx.dispatch default result is not success
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

- `git diff --check` exited 0. Git reported existing LF-to-CRLF working-copy
  warnings only.

## Failures

- None.

## Deferred work

- This is host-tested network receive-dispatch evidence only. It does not
  implement NIC receive polling, live virtio-net packet I/O, queue interrupts,
  sockets, hosted .NET 10, ASP.NET Minimal API, Kestrel, cloud readiness, or
  production readiness.
