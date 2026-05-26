# Step 747 - FetchApp HTTP TCP Exchange Probe Toggle

## Summary

Added a disabled-by-default diagnostics probe entry for future FetchApp
HTTP-over-TCP exchange boot wiring.

## Why

`BootSequence.RunFetchAppHttpTcpExchangeReadiness` now has host-tested marker
behavior, but future guest wiring needs an explicit `Probes.cs` gate before it
is enabled in any boot image. This keeps network/app probes auditable and
opt-in while the implementation remains host-tested metadata only.

## Files changed

- `src/OS.Kernel/Diagnostics/Probes.cs`
  - Added `EnableFetchAppHttpTcpExchangeProbe`, defaulting to `false`.
  - Added `GetFetchAppHttpTcpExchangeProbe()` under `network.smoke` with
    `ProbeRisk.DeviceIo`.
- `tests/OS.HostTests/DiagnosticsTests.cs`
  - Extended dangerous/future probe default coverage to require the new probe
    to stay disabled by default and grouped under network smoke.
- `docs/diagnostics.md`
  - Documented the new probe in the disabled-by-default probe list and stated
    the execution boundary.
- `docs/networking.md`
  - Connected the probe catalog entry to the FetchApp HTTP TCP exchange
    readiness marker boundary.
- `done/step747.md`
  - Recorded this implementation slice and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue708-fetch-http-tcp-probe\bin\Debug\net10.0`
- `dotnet .\artifacts\issue708-fetch-http-tcp-probe\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence

- `artifacts/logs/issue708-host-build-20260525.log`
  - Host-test project build succeeded with 0 warnings and 0 errors.
- `artifacts/logs/issue708-hosttests-20260525.log`
  - Full shaped host harness ended with `TEST_PASS host.tests`.
  - Focused diagnostics probe markers passed:
    - `TEST_PASS probes expose grouped safe defaults`
    - `TEST_PASS probes keep dangerous toggles disabled`
- `artifacts/logs/issue708-git-diff-check-final-20260525.log`
  - `git diff --check` exited 0.
  - Output contains existing LF/CRLF working-copy warnings only.

## Failures

No final build or host-test failures.

## Deferred work

- Wire the probe into an actual guest boot path only after manifest loading,
  DNS, ARP, TCP, HTTP, completion, and FetchApp exchange metadata can be
  produced in the guest environment.
- Keep the probe disabled by default until a dedicated smoke image explicitly
  opts in.

## Evidence boundary

Probe catalog and host-test evidence only. This does not execute DNS, ARP, TCP,
HTTP, virtio-net, PAL sockets, FetchApp, QEMU boot, hosted .NET 10, ASP.NET
Minimal API, Kestrel, cloud readiness, or production readiness.
