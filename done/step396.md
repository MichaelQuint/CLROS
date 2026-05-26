# Step 396 - Manifest Listen Bind Policy

## Summary
Added a host-tested app-model policy helper that validates a future PAL IPv4 socket-bind endpoint against the kernel-facing native manifest listen endpoint carried by `NativeAppLaunchPlan`.

## Why
The hosted .NET/Kestrel path eventually needs manifest-approved socket binds, but the project is not ready to open sockets or export PAL functions. This slice keeps the boundary small: source-backed manifest metadata can approve or reject a proposed PAL endpoint before any live socket behavior exists.

## Files changed
- `src/OS.Kernel/Manifest/NativeAppSocketBindPolicy.cs`
- `tests/OS.HostTests/NativeAppSocketBindPolicyTests.cs`
- `docs/manifest.md`
- `docs/networking.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step396.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host-test build completed with 0 warnings and 0 errors.
- Host-test marker scan emitted:
  - `TEST_PASS appmodel.socket.policy accepts manifest listen endpoint`
  - `TEST_PASS appmodel.socket.policy accepts wildcard manifest endpoint`
  - `TEST_PASS appmodel.socket.policy rejects missing listen endpoint`
  - `TEST_PASS appmodel.socket.policy rejects wrong port`
  - `TEST_PASS appmodel.socket.policy rejects wrong address`
  - `TEST_PASS appmodel.socket.policy preserves no-socket-io boundary`
  - `TEST_PASS host.tests`
- Full solution build completed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
- None.

## Deferred work
- Wire the policy into the future PAL socket bind/listen path.
- Implement real socket bind/listen, TCP listener state, exported PAL functions, hosted .NET 10 startup, and Kestrel guest evidence.

## Evidence boundary
- This is host-tested manifest bind policy only. It does not call `OSHostSocketTable.Bind`, open sockets, export PAL functions, create a TCP listener, start hosted .NET 10, start Kestrel, prove cloud readiness, or prove production readiness.

