# Step 746 - FetchApp HTTP TCP Exchange Boot Readiness Marker

## Summary

Added an opt-in boot-sequence readiness gate for the FetchApp HTTP-over-TCP
exchange contract.

## Why

Issue #707 needed the host-tested `FetchAppHttpTcpExchangePlan` evidence to
become a stable boot diagnostic boundary. The exchange diagnostic formatter
already emitted `FETCH_HTTP_TCP_EXCHANGE`; this step lets future probe paths
turn that result into ordered serial markers without changing the default boot
sequence or claiming live network I/O.

## Files changed

- `src/OS.Kernel/Boot/BootSequence.cs`
  - Added `FetchAppHttpTcpExchangeReadinessStatus`.
  - Added `RunFetchAppHttpTcpExchangeReadiness`.
  - Added ready/not-ready marker helpers using
    `FETCH_APP_HTTP_TCP_EXCHANGE_READY` and
    `FETCH_APP_HTTP_TCP_EXCHANGE_NOT_READY`.
  - Added bounded formatting through
    `FetchAppHttpTcpExchangePlanDiagnosticFormatter` so ready markers carry
    the existing `FETCH_HTTP_TCP_EXCHANGE` payload without dumping host, path,
    or expected marker bytes.
- `tests/OS.HostTests/FetchAppHttpTcpExchangePlanTests.cs`
  - Added boot-sequence tests for ready, failed-result, default-result, and
    default boot-marker preservation paths.
- `docs/networking.md`
  - Documented the readiness marker contract, host-test markers, and evidence
    boundary.
- `done/step746.md`
  - Recorded this implementation slice and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue707-fetch-http-tcp-exchange-boot-marker\bin\Debug\net10.0`
- `dotnet .\artifacts\issue707-fetch-http-tcp-exchange-boot-marker\bin\Debug\net10.0\OS.HostTests.dll`

## Evidence

- `artifacts/logs/issue707-host-build-initial-20260525.log`
  - Host-test project build succeeded with 0 warnings and 0 errors.
- `artifacts/logs/issue707-hosttests-initial-20260525.log`
  - Full shaped host harness ended with `TEST_PASS host.tests`.
  - Focused boot-readiness markers passed:
    - `TEST_PASS boot.sequence fetch http tcp exchange ready`
    - `TEST_PASS boot.sequence fetch http tcp exchange rejects failed result`
    - `TEST_PASS boot.sequence fetch http tcp exchange rejects default result`
    - `TEST_PASS boot.sequence fetch http tcp exchange preserves default markers`

## Failures

No final verification failures for this slice.

## Deferred work

- Wire this readiness gate into a real guest probe only after the preceding
  manifest, DNS, ARP, TCP, HTTP, and FetchApp completion staging paths can
  provide exchange metadata inside the boot environment.
- Prove the marker through QEMU only after the guest path can produce it.
- Keep live virtio-net, timers, retransmission, DNS, sockets, FetchApp
  execution, hosted .NET 10, ASP.NET Minimal API, and Kestrel claims blocked
  until they have direct evidence.

## Evidence boundary

Host-tested boot-marker evidence only. This does not perform DNS, ARP, TCP, or
HTTP over a NIC, publish virtio-net descriptors, open sockets, execute FetchApp,
run QEMU, call `ExitBootServices`, host .NET 10, start Kestrel, prove cloud
readiness, or prove production readiness.
