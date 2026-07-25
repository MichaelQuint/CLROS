# Step 578 - Native socket bind-policy default-result guard

## Summary

Added explicit initialized-state gating to `NativeAppSocketBindPolicyResult` so
a default manifest socket bind-policy result no longer satisfies `Succeeded`.

## Why

The socket bind policy is the manifest-to-PAL endpoint gate that future bind and
listen bridges consume before mutating mock or eventual PAL socket state.
Because `NativeAppSocketBindPolicyStatus.Success` is zero-valued, a default
result could otherwise look like a validated manifest-approved bind endpoint.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppSocketBindPolicy.cs`
- `tests/OS.HostTests/NativeAppSocketBindPolicyTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step578.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered to `appmodel.socket.policy`, `TEST_PASS host.tests`, and
  `TEST_FAIL`.
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
TEST_START appmodel.socket.policy accepts manifest listen endpoint
TEST_PASS appmodel.socket.policy accepts manifest listen endpoint
TEST_START appmodel.socket.policy accepts wildcard manifest endpoint
TEST_PASS appmodel.socket.policy accepts wildcard manifest endpoint
TEST_START appmodel.socket.policy rejects missing listen endpoint
TEST_PASS appmodel.socket.policy rejects missing listen endpoint
TEST_START appmodel.socket.policy rejects wrong port
TEST_PASS appmodel.socket.policy rejects wrong port
TEST_START appmodel.socket.policy rejects wrong address
TEST_PASS appmodel.socket.policy rejects wrong address
TEST_START appmodel.socket.policy preserves no-socket-io boundary
TEST_PASS appmodel.socket.policy preserves no-socket-io boundary
TEST_START appmodel.socket.policy default result is not success
TEST_PASS appmodel.socket.policy default result is not success
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

- None.

## Deferred work

- This is host-tested manifest-to-socket bind policy only. It does not open
  live sockets, export PAL functions, listen for TCP, start hosted .NET 10,
  prove Minimal API/Kestrel, cloud readiness, or production readiness.
