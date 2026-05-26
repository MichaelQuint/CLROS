# Step 254 - QEMU Runner HTTP Probe

## Summary

Implemented real-mode host HTTP probing in `OS.QemuRunner` and added
local-loopback host tests for pass, status mismatch, body mismatch, and timeout
behavior.

## Why

Issue #254 needed the QEMU runner to verify an HTTP response after required
serial markers are observed, so future Kestrel smoke runs can fail on missing
or wrong HTTP evidence. This step proves only the host runner probe logic; it
does not prove guest networking or Kestrel.

## Files Changed

- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/QemuRunnerHttpProbeTests.cs`
- `docs/qemu.md`
- `docs/kestrel.md`
- `done/step254.md`

## Tests Run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'qemu\.runner\.http_probe|HTTP_PROBE|QEMU_SMOKE_STATUS|TEST_PASS host\.tests'`
- `git diff --check`

## Evidence

- Solution build passed with `Build succeeded.`, `0 Warning(s)`, and
  `0 Error(s)`.
- The host test command ran the full host harness and exited 0. The filtered
  output included:
  - `TEST_PASS qemu.runner.http_probe accepts expected response`
  - `TEST_PASS qemu.runner.http_probe rejects status mismatch`
  - `TEST_PASS qemu.runner.http_probe rejects body mismatch`
  - `TEST_PASS qemu.runner.http_probe times out deterministically`
  - `TEST_PASS host.tests`
- `git diff --check` exited 0 with line-ending normalization warnings only.

## Behavior

- `OS.QemuRunner` still keeps HTTP probing disabled unless
  `--http-probe-url` is supplied.
- Real mode now runs the probe after all required serial markers are observed.
- Probe output records endpoint, response status, response byte count,
  response body SHA-256, elapsed time, and failure reason.
- The runner never prints the expected or actual response body.
- The probe uses a raw HTTP/1.1 GET over `TcpClient` instead of `HttpClient`
  so local-loopback and future guest-endpoint checks do not use host proxy
  configuration.

## Failures

- The first implementation used `HttpClient`, and local-loopback probe tests
  timed out because the host networking stack did not reach the loopback test
  listener through that path. The runner was changed to raw TCP HTTP probing.
- An in-process loopback listener also timed out under the module-initializer
  host harness. The tests now use a temporary PowerShell listener process so
  the server is independent of host-test module initialization.

## Deferred Work

- No guest HTTP endpoint exists yet.
- No virtio-net, DHCP, DNS, TCP listener, hosted .NET 10 runtime, or Kestrel
  behavior is proven by this step.
- Future Kestrel smoke work must point the probe at a real guest endpoint and
  require the guest serial markers documented in `docs/kestrel.md`.
