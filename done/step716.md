# Step 716 - HTTP Parser Shared ASCII Helpers And App Args QEMU Preset

## Summary

Closed issue slices #647 through #650. HTTP request parsing, HTTP response
parsing, and FetchApp completion expectation parsing now use the shared
`OS.Std.Text.AsciiString` equality helpers instead of local ASCII comparison
helpers. QEMU smoke tooling also has an opt-in `app-args` marker preset for
future native app argument-vector smoke images.

## Why

The networking and app-model code should depend on the no-runtime shared
utility surface before adding more parser behavior. The app-args preset gives
the host runner and wrapper a stable machine-readable contract for later guest
images that emit `APP_ARGS_VECTOR_READY`, without changing default smoke
requirements or claiming live app execution.

## Files changed

- `src/OS.Kernel/Net/HttpRequestParser.cs`
- `src/OS.Kernel/Net/HttpResponseParser.cs`
- `src/OS.Kernel/Net/FetchAppHttpCompletionExpectation.cs`
- `tests/OS.HostTests/HttpRequestParserTests.cs`
- `tests/OS.HostTests/HttpResponseParserTests.cs`
- `tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs`
- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `done/step716.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset app-args`
- `git diff --check`

## Evidence

- `artifacts/logs/issues647-650-host-build-20260525.log` records a successful
  host-test build with zero warnings and zero errors.
- `artifacts/logs/issues647-650-hosttests-20260525.log` records
  `TEST_PASS host.tests`, including:
  - `TEST_PASS net.http.request uses shared ascii equality helper`
  - `TEST_PASS net.http.response uses shared ascii equality helper`
  - `TEST_PASS fetch.http expectation uses shared ascii equality helper`
  - `TEST_PASS qemu.smoke.preset app args adds argument-vector marker`
  - `TEST_PASS qemu.smoke.preset app args aliases normalize`
  - `TEST_PASS qemu.smoke.preset script dry-run includes app args preset`
- `artifacts/logs/issue650-qemu-app-args-dryrun-20260525.log` records
  `QEMU_MARKER_PRESETS app-args`, `MARKER_PRESETS app-args`,
  `APP_ARGS_VECTOR_READY`, and `TEST_PASS qemu.runner.dryrun`.
- `artifacts/logs/issues647-650-git-diff-check-20260525.log` records
  `EXIT_CODE 0`.

## Failures

No validation failures remained after the combined build/test run. The
`git diff --check` log still contains existing line-ending warnings about LF
files that Git will convert to CRLF on touch; it reported no whitespace errors.

## Deferred work

- The `app-args` preset is required-marker tooling only. It does not prove
  live native app argument-vector materialization, app ABI execution, hosted
  .NET, Minimal API, or Kestrel.
- The parser migrations are host-tested utility migrations only. They do not
  prove live virtio-net, DHCP, DNS, TCP, HTTP GET, or guest FetchApp execution.

