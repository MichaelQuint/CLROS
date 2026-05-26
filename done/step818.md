# Step 818 - HTTP Static TCP Response BootSequence Markers

## Summary

Added an opt-in `BootSequence` readiness boundary for the host-tested HTTP
static TCP response plan. Successful inputs now emit the already-formatted
`HTTP_STATIC_TCP_RESPONSE ...` diagnostic marker followed by
`HTTP_STATIC_TCP_RESPONSE_READY`; blocked or formatter-failure inputs emit
`HTTP_STATIC_TCP_RESPONSE_NOT_READY ...` with nested status metadata.

## Why

The QEMU marker preset from #775 needs a stable guest marker target, but the
default boot path must not claim a live HTTP server. This step exposes the
already-produced #773 formatter result through an explicit helper that callers
can invoke only after they have staged static TCP response plan evidence.

## Files changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/HttpStaticTcpResponseBootSequenceTests.cs`
- `docs/boot.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step818.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo *> .\artifacts\logs\issue774-host-build.log`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll *> .\artifacts\logs\issue774-host-tests.log`

## Evidence

- `artifacts/logs/issue774-host-build.log` ended with `Build succeeded.`,
  `0 Warning(s)`, `0 Error(s)`, and `issue774-host-build-exit=0`.
- `artifacts/logs/issue774-host-tests.log` ended with `TEST_PASS host.tests`
  and `issue774-host-tests-exit=0`.
- `artifacts/logs/issue774-host-tests.log` emitted:
  - `TEST_PASS boot.sequence http static tcp response emits ready marker`
  - `TEST_PASS boot.sequence http static tcp response emits blocked marker`
  - `TEST_PASS boot.sequence http static tcp response reports formatter failure`
  - `TEST_PASS boot.sequence http static tcp response preserves default markers`
- `artifacts/logs/issue774-diff-check.log` ended with
  `git-diff-check-exit=0`.
- `artifacts/logs/issue774-trailing-whitespace.log` emitted
  `TEST_PASS issue774.trailing_whitespace`.

## Failures

None in the saved #774 build or host-test logs.

## Deferred work

- This remains host-tested marker aggregation over synthetic plan metadata. It
  does not prove live virtio-net RX/TX, sockets/listeners, a TinyHttpServer
  loop, response descriptor publication, hosted .NET 10, ASP.NET Minimal API,
  Kestrel, cloud readiness, or production readiness.
