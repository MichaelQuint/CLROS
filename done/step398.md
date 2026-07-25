# Step 398 - Manifest-Approved Mock Socket Bind Bridge

## Summary
Added a host-tested `NativeAppSocketBindBridge` that composes manifest listen-endpoint policy with the in-process `OSHostSocketTable.Bind` mock.

## Why
The future hosted runtime needs manifest-approved bind behavior, but exported PAL functions and real sockets are not ready. This slice proves policy failures do not mutate mock socket state and policy success can flow into the existing mock socket table while preserving table failures.

## Files changed
- `src/OS.Kernel/Manifest/NativeAppSocketBindBridge.cs`
- `tests/OS.HostTests/NativeAppSocketBindBridgeTests.cs`
- `docs/manifest.md`
- `docs/networking.md`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step398.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host-test build completed with 0 warnings and 0 errors.
- Host-test marker scan emitted:
  - `TEST_PASS appmodel.socket.bindbridge binds manifest-approved endpoint`
  - `TEST_PASS appmodel.socket.bindbridge rejects policy mismatch`
  - `TEST_PASS appmodel.socket.bindbridge propagates mock bind conflict`
  - `TEST_PASS appmodel.socket.bindbridge rejects missing table`
  - `TEST_PASS appmodel.socket.bindbridge preserves mock-only boundary`
  - `TEST_PASS host.tests`
- Full solution build completed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
- None.

## Deferred work
- Export real `OSHost_SocketBind` only after the PAL export/runtime boundary exists.
- Wire manifest-approved bind/listen into the future app supervisor and live TCP listener path.
- Keep hosted .NET 10, Minimal API, and Kestrel claims blocked until in-guest runtime/socket evidence exists.

## Evidence boundary
- This is host-tested composition over `OSHostSocketTable`, an in-process mock. It does not export PAL functions, open live sockets, create real TCP listener state, accept live connections, start hosted .NET 10, start Kestrel, prove cloud readiness, or prove production readiness.

