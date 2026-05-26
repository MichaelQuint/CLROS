# Step 580 - Native app socket listen bridge default-result guard

## Summary

`NativeAppSocketListenBridgeResult` now carries initialized-state metadata and
requires it for `Succeeded`. A default/zero listen-bridge result can no longer
be interpreted as successful readiness evidence.

## Why

The listen bridge composes manifest listen policy with a host-only mock socket
table create/bind/listen sequence. Its default struct values had
success-shaped status fields, which made zero values ambiguous as readiness
evidence. This step aligns the listen bridge with the adjacent bind bridge and
other guarded app-model result types.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppSocketListenBridge.cs`
- `tests/OS.HostTests/NativeAppSocketListenBridgeTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step580.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'appmodel\.socket\.listenbridge|appmodel\.socket\.policy|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build succeeded with `0 Warning(s)` and `0 Error(s)`.
- Filtered host-test markers:
  - `TEST_PASS appmodel.socket.listenbridge creates manifest listener`
  - `TEST_PASS appmodel.socket.listenbridge rejects policy mismatch`
  - `TEST_PASS appmodel.socket.listenbridge propagates create failure`
  - `TEST_PASS appmodel.socket.listenbridge propagates bind failure`
  - `TEST_PASS appmodel.socket.listenbridge propagates listen failure`
  - `TEST_PASS appmodel.socket.listenbridge preserves mock-only boundary`
  - `TEST_PASS appmodel.socket.listenbridge default result is not success`
  - `TEST_PASS appmodel.socket.policy default result is not success`
  - `TEST_PASS host.tests`
- Full solution build succeeded with `0 Warning(s)` and `0 Error(s)`.
- Naming/source firewall emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited `0`; output contained only existing LF-to-CRLF working-copy warnings.

## Failures

- None.

## Deferred work

- Listen bridge coverage remains host-tested mock-table evidence only. It does
  not export PAL socket functions, open live sockets, prove scheduler waits,
  prove TCP listener behavior, start hosted .NET, start Kestrel, prove cloud
  readiness, or prove production readiness.
