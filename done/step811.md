# Step 811 - Virtio-Net RX TCP Dispatch Boot Readiness

## Summary

Added an opt-in `BootSequence` readiness wrapper for host-tested virtio-net RX
TCP dispatch results. Successful inputs now emit `VIRTIO_NET_RX_TCP_DISPATCH_READY`
followed by the existing `VIRTIO_NET_RX_TCP_DISPATCH status=...` diagnostic
payload; failed/default inputs emit status-coded
`VIRTIO_NET_RX_TCP_DISPATCH_NOT_READY` markers.

## Why

The virtio-net RX TCP dispatch formatter made the result metadata serializable,
but boot/QEMU plumbing needs an explicit readiness marker before a future marker
preset can require it. This keeps the boundary honest: it reports already-made
metadata and does not attempt live NIC work.

## Files changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/VirtioNetReceiveRegisterWindowTcpDispatchPlanTests.cs`
- `docs/boot.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step811.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo *> .\artifacts\logs\issue767-host-build-r3.log`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll *> .\artifacts\logs\issue767-host-tests-r3.log`
- Scoped `git diff --check` for the changed files.
- Scoped trailing-whitespace scan for the changed files.

## Evidence

- `artifacts/logs/issue767-host-build-r3.log` ended with `Build succeeded.`,
  `0 Warning(s)`, and `0 Error(s)`.
- `artifacts/logs/issue767-host-tests-r3.log` ended with `TEST_PASS host.tests`.
- `artifacts/logs/issue767-host-tests-r3.log` emitted:
  - `TEST_PASS boot.sequence virtio rx tcp dispatch emits ready marker`
  - `TEST_PASS boot.sequence virtio rx tcp dispatch rejects failed result`
  - `TEST_PASS boot.sequence virtio rx tcp dispatch rejects default result`
  - `TEST_PASS boot.sequence virtio rx tcp dispatch preserves default markers`
- `artifacts/logs/issue767-diff-check.log` ended with
  `git-diff-check-exit=0`.
- `artifacts/logs/issue767-trailing-whitespace.log` emitted
  `TEST_PASS issue767.trailing_whitespace`.

## Failures

- An earlier host-test run through `Tee-Object` left a stale `dotnet` test
  process holding host-test output assemblies. The stale process was confirmed
  as `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`,
  stopped, and the unchanged build/test commands were rerun successfully with
  direct artifact redirection.

## Deferred work

- Wire this marker into an opt-in QEMU marker preset and, later, a real boot path
  only after RX/TCP prerequisites are staged.
- This is host-tested marker wrapping only. It does not poll a live virtio-net
  queue, process interrupts, transfer packets, expose sockets/PAL receive, run
  DHCP/DNS/HTTP, start hosted .NET, start ASP.NET Minimal API, start Kestrel,
  or prove cloud readiness.
